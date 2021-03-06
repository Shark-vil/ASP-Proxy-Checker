using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using ProxyChecker.Core;
using ProxyChecker.Core.Models;
using ProxyChecker.Core.Models.FlareSolverr;
using ProxyChecker.Database;
using ProxyChecker.Database.Models;
using System.Text;
using System.Text.RegularExpressions;
using DbFlareSolverrProxy = ProxyChecker.Database.Models.FlareSolverrProxy;

namespace ProxyChecker.Controllers.API
{
    /// <summary>
    /// Контроллер для сканирования прокси
    /// </summary>
    public class CheckProxyController : Controller
    {
        /// <summary>
        /// Исключение для вызова в случае превышения количества обращений к серверу через парсинг
        /// </summary>
        private class MaxLookupsException : Exception
        {
            public MaxLookupsException() { }

            public MaxLookupsException(string message) : base(message) { }

            public MaxLookupsException(string message, Exception inner) : base(message, inner) { }
        }

        /// <summary>
        /// Очередь прокси для сканирования 
        /// </summary>
        private static Queue<Proxy> _proxiesCheckedQueue = new Queue<Proxy>();

        /// <summary>
        /// Общее число элементов в очереди прокси для сканирования
        /// </summary>
        private static int _proxiesCheckedTotal = 0;

        /// <summary>
        /// Очередь прокси для обхода в случае превышения числа запросов при парсинге
        /// </summary>
        private static Queue<DbFlareSolverrProxy> _flareSolverrProxies = new Queue<DbFlareSolverrProxy>();

        /// <summary>
        /// Текущий прокси для обхода при привышении числа запросов
        /// </summary>
        private static DbFlareSolverrProxy _currentFlareSolverrProxy = null;

        /// <summary>
        /// Текущий идентификатор активного процесса
        /// </summary>
        private static string _currentCheckIdentifier = string.Empty;

        /// <summary>
        /// Адрес для обращения к API сканера прокси
        /// </summary>
        private static string _apiUrl = "https://ipqualityscore.com/api/json/ip/";

        /// <summary>
        /// Адрес для обращения к API FlareSolverr для обхода CloudFlare (Устанавливается динамически)
        /// </summary>
        private static string _flareSolverrUrl = String.Empty;

        /// <summary>
        /// Параметры запроса при обращении к APO сканера прокси
        /// </summary>
        private static Dictionary<string, string> _apiGetOptions = new Dictionary<string, string>()
        {
            ["strictness"] = "1",
            ["allow_public_access_points"] = "false",
            ["fast"] = "false",
            ["lighter_penalties"] = "true",
            ["mobile"] = "true",
        };

        /// <summary>
        /// Название сесии для <see href="https://github.com/FlareSolverr/FlareSolverr">FlareSolverr</see> по умолчанию
        /// </summary>
        private static string _sessionName = "proxychecker";

        /// <summary>
        /// Логгер компонента
        /// </summary>
        private readonly ILogger<CheckProxyController> _logger;

        public CheckProxyController()
        {
            _logger = LogService.LoggerFactory.CreateLogger<CheckProxyController>();
        }


        /// <summary>
        /// Останавливает активный в данный момент процесс сканирования
        /// </summary>
        /// <returns>IActionResult</returns>
        [HttpPost("Api/[controller]/Stop")]
        public IActionResult Stop()
        {
            Task.Run(async () => await StopProcess());

            return Ok();
        }

        private async Task StopProcess()
        {
            _logger.LogInformation("Остановка процессов чекера");

            _flareSolverrProxies.Clear();
            _proxiesCheckedQueue.Clear();
            _currentCheckIdentifier = string.Empty;

            if (CheckFlareSolverrUrl())
                await StopBrowsers();
        }

        /// <summary>
        /// Запускает новый процесс сканирования
        /// </summary>
        /// <param name="checkType">Тип сканера</param>
        /// <param name="threadCount">Число потоков</param>
        /// <returns></returns>
        [HttpPost("Api/[controller]/Start")]
        public IActionResult Start([FromForm] string checkType, [FromForm] uint threadCount)
        {
            if (string.IsNullOrEmpty(checkType) || (checkType != "api" && checkType != "proxy"))
            {
                _logger.LogWarning("Передан некорректный тип чекера: {0}", checkType);
                return StatusCode(400);
            }

            _logger.LogInformation("Инициализация запуска чекера");

            _flareSolverrProxies.Clear();
            _proxiesCheckedQueue.Clear();

            _currentCheckIdentifier = Guid.NewGuid().ToString();

            using (var db = new DatabaseContext())
            {
                var proxiesCheckedTempList = new List<Proxy>(db.Proxies);
                proxiesCheckedTempList.RemoveAll(x => DB.ProxyController.ProxyIsBlocked(db, x));

                _proxiesCheckedQueue = new Queue<Proxy>(proxiesCheckedTempList);
                _proxiesCheckedTotal = _proxiesCheckedQueue.Count;

                _flareSolverrProxies = new Queue<DbFlareSolverrProxy>(db.FlareSolverrProxies);

                if (checkType == "api")
                {
                    if (db.IPQualityScoreTokens.Count() == 0)
                    {
                        _logger.LogWarning("Нету доступных токенов для взаимодействия с API");
                        return StatusCode(500);
                    }
                }
            }

            _logger.LogInformation("Подготовка к запуску мультипотока");

            MultiThread multiThread;

            Task.Run(async () =>
            {
                _logger.LogInformation("Очистка таблицы \"IPQualityScoreChecks\" от результатов последнего сканирования");

                using (var db = new DatabaseContext())
                {
                    var ipQualityScoreChecksFromRemoves = new List<IPQualityScore>(db.IPQualityScoreChecks);

                    foreach (var entry in ipQualityScoreChecksFromRemoves)
                        db.IPQualityScoreChecks.Remove(entry);

                    await db.SaveChangesAsync();
                }

                if (checkType == "api")
                    multiThread = new MultiThread(CheckByApi);
                else
                {
                    if (!CheckFlareSolverrUrl())
                    {
                        _logger.LogWarning("Адрес FlareSolverr: \"{0}\", не установлен или задан некорректно, процесс прерван!", _flareSolverrUrl);
                        await StopProcess();
                        return;
                    }

                    await RebuildBrowsers();
                    await CloudFlareBypass();

                    multiThread = new MultiThread(CheckByParse);
                }

                if (_proxiesCheckedQueue.Count > 0)
                {
                    multiThread.SetLimit(threadCount);
                    multiThread.Start();
                }
            });

            return Ok();
        }

        private bool CheckFlareSolverrUrl()
        {
            string webUrlPattern = @"^(ht|f)tp(s?)\:\/\/[0-9a-zA-Z]([-.\w]*[0-9a-zA-Z])*(:(0-9)*)*(\/?)([a-zA-Z0-9\-\.\?\,\'\/\\\+&%\$#_]*)?$";

            using (var db = new DatabaseContext())
            {
                Config? config = db.Configuration.FirstOrDefault();
                if (config != null)
                    _flareSolverrUrl = config.FlareSolverrUrl;
            }

            if (string.IsNullOrEmpty(_flareSolverrUrl) || !Regex.IsMatch(_flareSolverrUrl, webUrlPattern))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Задача, выполняемая перед запуском сканирования с использованием парсинга.
        /// <br></br>
        /// Потоки не будут запущены до тех пор, пока система не пробётся через блокировщик.
        /// </summary>
        /// <returns>Task</returns>
        private async Task CloudFlareBypass()
        {
            bool isBypass = false;
            var flareSolverrProxies = new Queue<DbFlareSolverrProxy>();
            var flareSolverrProxiesBlocked = new List<DbFlareSolverrProxy>();
            DbFlareSolverrProxy currentFlareSolverrProxy = null;

            using (var db = new DatabaseContext())
                _logger.LogInformation("Число обходных прокси: {0}", db.FlareSolverrProxies.Count());

            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:90.0) Gecko/20100101 Firefox/90.0");

                while (!isBypass && _proxiesCheckedQueue.Count > 0)
                {
                    try
                    {
                        _logger.LogInformation("Попытка пробить CloudFlare");

                        Core.Models.FlareSolverr.FlareSolverrProxy flareSolverrProxy = null;

                        if (currentFlareSolverrProxy != null && !flareSolverrProxiesBlocked.Exists(x => x == currentFlareSolverrProxy))
                        {
                            string proxyIp = currentFlareSolverrProxy.Ip;
                            uint proxyPort = currentFlareSolverrProxy.Port;

                            HttpClientProxyModel? httpClientProxyModel = await HttpClientProxyChecker.GetHttpClient(proxyIp, proxyPort);

                            if (httpClientProxyModel != null)
                            {
                                flareSolverrProxy = new Core.Models.FlareSolverr.FlareSolverrProxy
                                {
                                    url = $"{httpClientProxyModel.ProxyType}://{proxyIp}:{proxyPort}"
                                };

                                _logger.LogInformation("Определён прокси: ", flareSolverrProxy.url);
                            }
                            else
                            {
                                flareSolverrProxiesBlocked.Add(currentFlareSolverrProxy);
                                _logger.LogWarning("Прокси \"{0}:{1}\" не определён", proxyIp, proxyPort);
                            }

                            currentFlareSolverrProxy = null;
                        }

                        //_logger.LogInformation("Отправка запроса на: {0}", _flareSolverrUrl);

                        HttpResponseMessage httpResponse = await httpClient.SendAsync(new HttpRequestMessage
                        {
                            RequestUri = new Uri(_flareSolverrUrl),
                            Method = new HttpMethod("POST"),
                            Content = new StringContent(JsonConvert.SerializeObject(new FlareSolverrRequestGet
                            {
                                url = "https://www.ipqualityscore.com/",
                                session = _sessionName,
                                proxy = flareSolverrProxy
                            }), Encoding.UTF8, "application/json")
                        });

                        string jsonResponse = await httpResponse.Content.ReadAsStringAsync();
                        var flareSolverrResponse = JsonConvert.DeserializeObject<FlareSolverrResponseGet>(jsonResponse);

                        //_logger.LogInformation("Ответ от {0}:\n{1}", _flareSolverrUrl, jsonResponse);

                        if (
                            flareSolverrResponse != null &&
                            flareSolverrResponse.status != "error" &&
                            flareSolverrResponse.solution != null &&
                            flareSolverrResponse.solution.status != null &&
                            flareSolverrResponse.solution.status == 200
                        )
                        {
                            _logger.LogInformation("CloudFlare успешно пробит");
                            isBypass = true;
                            break;
                        }

                        _logger.LogInformation("CloudFlare не пробит. Ответ от {0}:\n{1}", _flareSolverrUrl, jsonResponse);

                        if (flareSolverrResponse == null || flareSolverrResponse.solution == null)
                        {
                            if (flareSolverrProxies.Count > 0)
                            {
                                currentFlareSolverrProxy = flareSolverrProxies.Dequeue();

                                _logger.LogInformation("Попытка подключится через прокси: {0}:{1}",
                                    currentFlareSolverrProxy.Ip,
                                    currentFlareSolverrProxy.Port);
                            }
                            else
                            {
                                using (var db = new DatabaseContext())
                                    flareSolverrProxies = new Queue<DbFlareSolverrProxy>(db.FlareSolverrProxies);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex.ToString());
                    }

                    await Task.Delay(6000);
                }
            }
        }

        /// <summary>
        /// Останавливает активный браузер <see href="https://github.com/FlareSolverr/FlareSolverr">FlareSolverr</see>.
        /// </summary>
        /// <returns>Task</returns>
        private async Task StopBrowsers()
        {
            _logger.LogInformation("Подготовка к остановке браузера");

            using (var httpClient = new HttpClient())
            {
                try
                {
                    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:90.0) Gecko/20100101 Firefox/90.0");

                    await httpClient.SendAsync(new HttpRequestMessage
                    {
                        RequestUri = new Uri(_flareSolverrUrl),
                        Method = new HttpMethod("POST"),
                        Content = new StringContent(JsonConvert.SerializeObject(new FlareSolverrRequestDestroy
                        {
                            session = _sessionName
                        }), Encoding.UTF8, "application/json")
                    });

                    _logger.LogInformation("Браузер остановлен");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.ToString());
                }
            }
        }

        /// <summary>
        /// Запускает новый браузер <see href="https://github.com/FlareSolverr/FlareSolverr">FlareSolverr</see>.
        /// </summary>
        /// <returns>Task</returns>
        private async Task StartBrowsers()
        {
            _logger.LogInformation("Подготовка к запуску браузера");

            using (var httpClient = new HttpClient())
            {
                try
                {
                    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:90.0) Gecko/20100101 Firefox/90.0");

                    await httpClient.SendAsync(new HttpRequestMessage
                    {
                        RequestUri = new Uri(_flareSolverrUrl),
                        Method = new HttpMethod("POST"),
                        Content = new StringContent(JsonConvert.SerializeObject(new FlareSolverrRequestCreate
                        {
                            session = _sessionName
                        }), Encoding.UTF8, "application/json")
                    });

                    _logger.LogInformation("Браузер запущен");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.ToString());
                }
            }
        }

        /// <summary>
        /// Перезапускает браузер <see href="https://github.com/FlareSolverr/FlareSolverr">FlareSolverr</see>.
        /// </summary>
        /// <returns>Task</returns>
        private async Task RebuildBrowsers()
        {
            _logger.LogInformation("Перезапуск браузера");

            await StopBrowsers();
            await StartBrowsers();
        }

        /// <summary>
        /// Процесс проверки прокси через метод парсинга.
        /// </summary>
        /// <param name="locker">Объект для синхронизации потоков</param>
        /// <param name="threadIndex">ID текущего потока</param>
        /// <returns>Task</returns>
        /// <exception cref="MaxLookupsException">Исключение в случае превышения числа обращений к сайту</exception>
        private async Task CheckByParse(object locker, int threadIndex)
        {
            string checkIdentifier = _currentCheckIdentifier;

            while (_proxiesCheckedQueue.Count > 0 && checkIdentifier == _currentCheckIdentifier)
            {
                Proxy? proxy = null;
                bool backProxyToQueue = false;

                lock (locker)
                {
                    if (_proxiesCheckedQueue.Count != 0)
                        proxy = _proxiesCheckedQueue.Dequeue();
                }

                if (proxy == null)
                    break;

                try
                {
                    Uri webUrl = GetProxyCheckerLink(proxy.RealAddress);
                    string normalUrl = webUrl.AbsoluteUri + "?" + Guid.NewGuid().ToString();
                    HttpResponseMessage httpResponse = null;
                    string jsonResponse = null;

                    _logger.LogInformation("Получаю информацию по адресу:\n{0}", normalUrl);

                    using (var httpClient = new HttpClient())
                    {
                        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:90.0) Gecko/20100101 Firefox/90.0");

                        Core.Models.FlareSolverr.FlareSolverrProxy flareSolverrProxy = null;

                        if (_currentFlareSolverrProxy != null)
                        {
                            string proxyIp = _currentFlareSolverrProxy.Ip;
                            uint proxyPort = _currentFlareSolverrProxy.Port;

                            HttpClientProxyModel? httpClientProxyModel = await HttpClientProxyChecker.GetHttpClient(proxyIp, proxyPort);

                            if (httpClientProxyModel != null)
                            {
                                flareSolverrProxy = new Core.Models.FlareSolverr.FlareSolverrProxy
                                {
                                    url = $"{httpClientProxyModel.ProxyType}://{proxyIp}:{proxyPort}"
                                };

                                _logger.LogInformation("Определён прокси: {0}", flareSolverrProxy.url);
                            }
                            else
                            {
                                _logger.LogWarning("Прокси {0}:{1} не определён", proxyIp, proxyPort);
                            }
                        }

                        httpResponse = await httpClient.SendAsync(new HttpRequestMessage
                        {
                            RequestUri = new Uri(_flareSolverrUrl),
                            Method = new HttpMethod("POST"),
                            Content = new StringContent(JsonConvert.SerializeObject(new FlareSolverrRequestGet
                            {
                                url = normalUrl,
                                session = _sessionName,
                                maxTimeout = 10000,
                                proxy = flareSolverrProxy
                            }), Encoding.UTF8, "application/json")
                        });

                        jsonResponse = await httpResponse.Content.ReadAsStringAsync();

                        Console.WriteLine(jsonResponse);
                    }

                    var flareSolverrResponse = JsonConvert.DeserializeObject<FlareSolverrResponseGet>(jsonResponse);
                    if (flareSolverrResponse == null)
                        throw new Exception($"Не удалось преобразовать ответ в C# объект");

                    if (flareSolverrResponse.status == "error")
                        throw new Exception($"Серер вернул сообщение с ошибкой:\n{ flareSolverrResponse}");
                    
                    if (flareSolverrResponse.solution != null)
                    {
                        if (flareSolverrResponse.solution.status != 200)
                            throw new Exception($"Серер вернул сообщение с ошибкой:\n{ flareSolverrResponse.solution}");

                        var htmlDocument = new HtmlDocument();
                        htmlDocument.LoadHtml(flareSolverrResponse.solution.response);;

                        if (htmlDocument.DocumentNode == null)
                            throw new Exception("Не удалось преобразовать ответ в HTML");

                        var getFormValues = new Dictionary<string, string>();

                        HtmlNode htmlDangerNode = htmlDocument.DocumentNode.SelectSingleNode("//span[@class='label label-danger']");
                        if (htmlDangerNode != null)
                            throw new MaxLookupsException("Ошибка \"Oops, You Reached Your Max Lookups!\"");

                        HtmlNodeCollection htmlFormNodes = htmlDocument.DocumentNode.SelectNodes("//table[@class='ip-lookup-report clearfix']//tr");
                        if (htmlFormNodes == null || htmlFormNodes.Count == 0)
                            throw new Exception($"Не удалось получить форму с данными");

                        foreach (HtmlNode row in htmlFormNodes)
                        {
                            HtmlNodeCollection tableCells = row.SelectNodes("td");
                            if (tableCells != null && tableCells.Count >= 2)
                            {
                                HtmlNode firstCell = tableCells[0];
                                HtmlNode secondCell = tableCells[1];

                                string[] brFirstSplit = firstCell.InnerHtml.Split("<br>");
                                string[] brSecondSplit = secondCell.InnerHtml.Split("<br>");

                                if (brFirstSplit.Length == 2)
                                    firstCell = HtmlNode.CreateNode(brFirstSplit[0]);

                                if (brSecondSplit.Length == 2)
                                    secondCell = HtmlNode.CreateNode(brSecondSplit[0]);

                                string key = HtmlNodeGetValue(firstCell);
                                string value = HtmlNodeGetValue(secondCell);

                                Console.WriteLine($"{key}: {value}");

                                getFormValues.Add(key, value);
                            }
                        }

                        var iPQuality = new IPQualityScore();

                        if (getFormValues.ContainsKey("Fraud Score"))
                        {
                            string fraudScoreString = Regex.Match(getFormValues["Fraud Score"], @"\d+").Value;
                            int fraud_score;

                            if (int.TryParse(fraudScoreString, out fraud_score))
                                iPQuality.FraudScore = fraud_score;
                        }

                        if (getFormValues.ContainsKey("Time Zone"))
                            iPQuality.Timezone = getFormValues["Time Zone"];

                        if (getFormValues.ContainsKey("Organization"))
                            iPQuality.Organization = getFormValues["Organization"];

                        if (getFormValues.ContainsKey("Hostname"))
                            iPQuality.Host = getFormValues["Hostname"];

                        if (getFormValues.ContainsKey("City"))
                            iPQuality.City = getFormValues["City"];

                        if (getFormValues.ContainsKey("Longitude"))
                        {
                            float longitude;

                            if (float.TryParse(getFormValues["Longitude"], out longitude))
                                iPQuality.Longitude = longitude;
                        }

                        if (getFormValues.ContainsKey("Latitude"))
                        {
                            float longitude;

                            if (float.TryParse(getFormValues["Latitude"], out longitude))
                                iPQuality.Latitude = longitude;
                        }

                        _logger.LogInformation("[ {0} ] Fraud Score: {1}", proxy.RealAddress, iPQuality.FraudScore);

                        if (iPQuality.FraudScore == null || (uint)iPQuality.FraudScore != 0)
                            continue;

                        iPQuality.Ip = proxy.Ip;
                        iPQuality.ProxyType = proxy.ProxyType;

                        using (var db = new DatabaseContext())
                        {
                            await db.IPQualityScoreChecks.AddAsync(iPQuality);
                            await db.SaveChangesAsync();
                        }

                        _logger.LogInformation("Прокси \"[{0}] - {1}\" добавлен в список результатов", proxy.Id, iPQuality.Ip);
                    }
                }
                catch (MaxLookupsException ex) when (_flareSolverrProxies.Count > 0)
                {
                    _logger.LogError(ex.ToString());
                    backProxyToQueue = true;
                    _currentFlareSolverrProxy = _flareSolverrProxies.Dequeue();

                    _logger.LogInformation("Попытка подключится через прокси: {0}:{1}",
                        _currentFlareSolverrProxy.Ip,
                        _currentFlareSolverrProxy.Port);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.ToString());
                    backProxyToQueue = true;
                    await Task.Delay(6000);
                }

                if (backProxyToQueue)
                {
                    lock (locker)
                    {
                        _proxiesCheckedQueue.Enqueue(proxy);
                        _logger.LogInformation("Прокси \"[{0}] - {1}\" был добавлен обратно в очередь", proxy.Id, proxy.RealAddress);
                    }
                }
            }

            if (threadIndex == 0)
                await StopBrowsers();
        }

        /// <summary>
        /// Процесс проверки прокси через метод обращения к API сканера.
        /// </summary>
        /// <param name="locker">Объект для синхронизации потоков</param>
        /// <param name="threadIndex">ID текущего потока</param>
        /// <returns>Task</returns>
        private async Task CheckByApi(object locker, int threadIndex)
        {
            string checkIdentifier = _currentCheckIdentifier;
            string currentApiToken = string.Empty;
            var apiTokens = new Queue<IPQualityScoreToken>();

            lock (locker)
            {
                using (var db = new DatabaseContext())
                {
                    apiTokens = new Queue<IPQualityScoreToken>(db.IPQualityScoreTokens);
                    currentApiToken = apiTokens.Dequeue().Token;
                }
            }

            while (_proxiesCheckedQueue.Count > 0 && checkIdentifier == _currentCheckIdentifier)
            {
                Proxy? proxy = null;

                lock (locker)
                {
                    if (_proxiesCheckedQueue.Count != 0)
                        proxy = _proxiesCheckedQueue.Dequeue();
                }

                if (proxy == null)
                    break;

                try
                {
                    Uri webUrl = GetApiCheckerLink(proxy.RealAddress, currentApiToken);
                    string jsonData = string.Empty;

                    _logger.LogInformation("Получаю информацию по адресу:\n{0}", webUrl.AbsoluteUri);

                    using (var httpClient = new HttpClient())
                    {
                        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:90.0) Gecko/20100101 Firefox/90.0");
                        httpClient.Timeout = TimeSpan.FromSeconds(5);
                        jsonData = await httpClient.GetStringAsync(webUrl);
                    }

                    var iPQuality = JsonConvert.DeserializeObject<IPQualityScore>(jsonData);
                    if (iPQuality == null)
                        throw new Exception("Не удалось преобразовать ответ в JSON");

                    bool success = iPQuality.Success != null ? (bool)iPQuality.Success : false;
                    if (!success)
                        throw new Exception(iPQuality.Message != null ? iPQuality.Message : "API отклонил запрос");

                    _logger.LogInformation("[ {0} ] Fraud Score: {1}", proxy.RealAddress, iPQuality.FraudScore);

                    if (iPQuality.FraudScore == null || (uint)iPQuality.FraudScore != 0)
                        continue;

                    iPQuality.Ip = proxy.Ip;
                    iPQuality.ProxyType = proxy.ProxyType;

                    using (var db = new DatabaseContext())
                    {
                        await db.IPQualityScoreChecks.AddAsync(iPQuality);
                        await db.SaveChangesAsync();
                    }

                    _logger.LogInformation("Прокси \"[{0}] - {1}\" добавлен в список результатов", proxy.Id, iPQuality.Ip);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.ToString());

                    lock (locker)
                    {
                        if (apiTokens.Count != 0)
                        {
                            _proxiesCheckedQueue.Enqueue(proxy);
                            _logger.LogInformation("Прокси \"[{0}] - {1}\" был добавлен обратно в очередь", proxy.Id, proxy.RealAddress);

                            currentApiToken = apiTokens.Dequeue().Token;
                            _logger.LogInformation("Попытка обновить токен на новый: {0}", currentApiToken);
                        }
                    }
                }
            }

            if (threadIndex == 0)
            {
                await StopBrowsers();
            }

            _logger.LogInformation("Поток #{0} завершил работу", threadIndex);
        }

        /// <summary>
        /// Возвращает значение из HTML объекта.
        /// </summary>
        /// <param name="htmlNode">HTML объект</param>
        /// <returns>Строка между тегов</returns>
        private string HtmlNodeGetValue(HtmlNode htmlNode)
        {
            string value = htmlNode.InnerText;
            value = value.Replace("\n", string.Empty);
            value = value.Trim();
            return value;
        }

        /// <summary>
        /// Возвращает ссылку для сканирования IP через API сканера с использованием токена
        /// </summary>
        /// <param name="ip">IP для сканирования</param>
        /// <param name="token">Токен для обращения к API</param>
        /// <returns>Конечная ссылка</returns>
        private Uri GetApiCheckerLink(string ip, string token)
        {
            string url = _apiUrl;
            url += $"{token}/{ip}";

            if (_apiGetOptions.Count != 0)
            {
                url += "?";
                foreach (KeyValuePair<string, string> keyValue in _apiGetOptions)
                    url += $"{keyValue.Key}={keyValue.Value}";
            }

            return new Uri(url);
        }

        /// <summary>
        /// Возвращает ссылку для сканирования IP методом парсинга
        /// </summary>
        /// <param name="ip">IP для сканирования</param>
        /// <returns>Конечная ссылка</returns>
        private Uri GetProxyCheckerLink(string ip)
        {
            string url = $"https://www.ipqualityscore.com/free-ip-lookup-proxy-vpn-test/lookup/{ip}";
            return new Uri(url);
        }

        /// <summary>
        /// Возвращает клиенту процент выполнения сканирования.
        /// </summary>
        /// <returns>Вернёт 100 если процесс завершён, или в очереди нету прокси</returns>
        [HttpGet("Api/[controller]/CheckProcess")]
        public IActionResult CheckProcess()
        {
            if (_proxiesCheckedQueue.Count == 0)
                return Ok(100);

            int value = 100 / _proxiesCheckedTotal;
            int percent = (_proxiesCheckedTotal - _proxiesCheckedQueue.Count) * value;
            return Ok(percent);
        }

        /// <summary>
        /// Возвращает клиенту список с результатами сканирования за последний момент.
        /// </summary>
        /// <returns>Список с последними результатами сканирования из базы данных</returns>
        [HttpGet("Api/[controller]")]
        public IEnumerable<IPQualityScore> Get()
        {
            List<IPQualityScore> response;

            using (var db = new DatabaseContext())
                response = new List<IPQualityScore>(db.IPQualityScoreChecks);

            return response;
        }
    }
}
