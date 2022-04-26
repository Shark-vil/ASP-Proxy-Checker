﻿using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using ProxyChecker.Core;
using ProxyChecker.Core.Models;
using ProxyChecker.Core.Models.FlareSolverr;
using ProxyChecker.Database;
using ProxyChecker.Database.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using DbFlareSolverrProxy = ProxyChecker.Database.Models.FlareSolverrProxy;

namespace ProxyChecker.Controllers.API
{
    public class CheckProxyController : Controller
    {
        private class MaxLookupsException : Exception
        {
            public MaxLookupsException() { }

            public MaxLookupsException(string message) : base(message) { }

            public MaxLookupsException(string message, Exception inner) : base(message, inner) { }
        }

        private static List<Proxy> _validProxies = new List<Proxy>();
        private static Queue<Proxy> _proxiesCheckedQueue = new Queue<Proxy>();
        private static Queue<DbFlareSolverrProxy> _flareSolverrProxies = new Queue<DbFlareSolverrProxy>();
        private static DbFlareSolverrProxy _currentFlareSolverrProxy = null;
        private static int _proxiesCheckedTotal = 0;
        private static string _checkType = string.Empty;
        private static uint _threadCount = 1;
        private static string _currentCheckIdentifier = string.Empty;
        private static string _apiUrl = "https://ipqualityscore.com/api/json/ip/";
        private static string _sessionName = "proxychecker";
        private static Dictionary<string, string> _apiGetOptions = new Dictionary<string, string>() {
            ["strictness"] = "1",
            ["allow_public_access_points"] = "false",
            ["fast"] = "false",
            ["lighter_penalties"] = "true",
            ["mobile"] = "true",
        };

        [HttpPost("Api/[controller]/Stop")]
        public IActionResult Stop()
        {
            _flareSolverrProxies.Clear();
            _proxiesCheckedQueue.Clear();
            _currentCheckIdentifier = string.Empty;

            Task.Run(async () => await StopBrowsers());

            return Ok();
        }

        [HttpPost("Api/[controller]/Start")]
        public IActionResult Start([FromForm] string checkType, [FromForm] uint threadCount)
        {
            if (string.IsNullOrEmpty(checkType) || (checkType != "api" && checkType != "proxy"))
                return StatusCode(400);

            _flareSolverrProxies.Clear();
            _proxiesCheckedQueue.Clear();
            _validProxies.Clear();

            _currentCheckIdentifier = Guid.NewGuid().ToString();
            _threadCount = threadCount;
            _checkType = checkType;

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
                        return StatusCode(500);
                }
            }

            MultiThread multiThread;

            Task.Run(async () =>
            {
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
                    multiThread = new MultiThread(CheckByProxy);

                    await RebuildBrowsers();
                    await CloudFlareBypass();
                }

                if (_proxiesCheckedQueue.Count > 0)
                {
                    multiThread.SetLimit(_threadCount);
                    multiThread.Start();
                }
            });

            return Ok();
        }

        private async Task CloudFlareBypass()
        {
            bool isBypass = false;
            uint timeLimit = 5000;
            var flareSolverrProxies = new Queue<DbFlareSolverrProxy>();
            var flareSolverrProxiesBlocked = new List<DbFlareSolverrProxy>();
            DbFlareSolverrProxy currentFlareSolverrProxy = null;
            var random = new Random();

            using (var db = new DatabaseContext())
                Console.WriteLine($"Число обходных прокси: {db.FlareSolverrProxies.Count()}");

            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:90.0) Gecko/20100101 Firefox/90.0");

                while (!isBypass && _proxiesCheckedQueue.Count > 0)
                {
                    try
                    {
                        Console.WriteLine($"Попытка пробить CloudFlare");

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

                                Console.WriteLine($"Определён прокси: {flareSolverrProxy.url}");
                            }
                            else
                            {
                                Console.WriteLine($"Прокси \"{proxyIp}:{proxyPort}\" не определён");
                                flareSolverrProxiesBlocked.Add(currentFlareSolverrProxy);
                            }

                            currentFlareSolverrProxy = null;
                        }

                        HttpResponseMessage httpResponse = await httpClient.SendAsync(new HttpRequestMessage
                        {
                            RequestUri = new Uri("http://194.67.78.16:8191/v1/"),
                            Method = new HttpMethod("POST"),
                            Content = new StringContent(JsonConvert.SerializeObject(new FlareSolverrRequestGet
                            {
                                url = "https://www.ipqualityscore.com/",
                                session = _sessionName,
                                maxTimeout = (uint)random.Next(5000, (int)timeLimit),
                                proxy = flareSolverrProxy
                            }), Encoding.UTF8, "application/json")
                        });

                        string jsonResponse = await httpResponse.Content.ReadAsStringAsync();
                        var flareSolverrResponse = JsonConvert.DeserializeObject<FlareSolverrResponseGet>(jsonResponse);
                        
                        if (
                            flareSolverrResponse != null &&
                            flareSolverrResponse.status != "error" &&
                            flareSolverrResponse.solution != null &&
                            flareSolverrResponse.solution.status != null &&
                            flareSolverrResponse.solution.status == 200
                        )
                        {
                            Console.WriteLine("CloudFlare успешно пробит");
                            isBypass = true;
                            break;
                        }

                        Console.WriteLine($"Сервер вернул сообщение:\n{jsonResponse}");

                        if (flareSolverrResponse != null && flareSolverrResponse.solution != null)
                        {
                            await Task.Delay(6000);
                        }
                        else
                        {
                            if (flareSolverrProxies.Count > 0)
                            {
                                currentFlareSolverrProxy = flareSolverrProxies.Dequeue();

                                Console.WriteLine($"Попытка подключится через прокси: " +
                                    $"{currentFlareSolverrProxy.Ip}:{currentFlareSolverrProxy.Port}");
                            }
                            else
                            {
                                using (var db = new DatabaseContext())
                                    flareSolverrProxies = new Queue<DbFlareSolverrProxy>(db.FlareSolverrProxies);
                            }

                            timeLimit = Math.Clamp(timeLimit + 1000, 0, 60000);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }

                    //await Task.Delay(6000);
                }
            }
        }

        private async Task StopBrowsers()
        {
            using (var httpClient = new HttpClient())
            {
                try
                {
                    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:90.0) Gecko/20100101 Firefox/90.0");

                    await httpClient.SendAsync(new HttpRequestMessage
                    {
                        RequestUri = new Uri("http://194.67.78.16:8191/v1/"),
                        Method = new HttpMethod("POST"),
                        Content = new StringContent(JsonConvert.SerializeObject(new FlareSolverrRequestDestroy
                        {
                            session = _sessionName
                        }), Encoding.UTF8, "application/json")
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
        }

        private async Task StartBrowsers()
        {
            using (var httpClient = new HttpClient())
            {
                try
                {
                    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:90.0) Gecko/20100101 Firefox/90.0");

                    await httpClient.SendAsync(new HttpRequestMessage
                    {
                        RequestUri = new Uri("http://194.67.78.16:8191/v1/"),
                        Method = new HttpMethod("POST"),
                        Content = new StringContent(JsonConvert.SerializeObject(new FlareSolverrRequestCreate
                        {
                            session = _sessionName
                        }), Encoding.UTF8, "application/json")
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
        }

        private async Task RebuildBrowsers()
        {
            await StopBrowsers();
            await StartBrowsers();
        }

        private async Task CheckByProxy(object locker, int threadIndex)
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

                    Console.WriteLine($"Получаю информацию о:\n{normalUrl}");

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

                                Console.WriteLine($"Определён прокси: {flareSolverrProxy.url}");
                            }
                            else
                            {
                                Console.WriteLine($"Прокси \"{proxyIp}:{proxyPort}\" не определён");
                            }
                        }

                        httpResponse = await httpClient.SendAsync(new HttpRequestMessage
                        {
                            RequestUri = new Uri("http://194.67.78.16:8191/v1/"),
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

                        Console.WriteLine($"Fraud Score ({proxy.RealAddress}): {iPQuality.FraudScore}");

                        if (iPQuality.FraudScore == null || (uint)iPQuality.FraudScore != 0)
                            continue;

                        iPQuality.Ip = proxy.Ip;
                        iPQuality.ProxyType = proxy.ProxyType;

                        using (var db = new DatabaseContext())
                        {
                            await db.IPQualityScoreChecks.AddAsync(iPQuality);
                            await db.SaveChangesAsync();
                        }

                        _validProxies.Add(proxy);
                    }
                }
                catch (MaxLookupsException ex) when (_flareSolverrProxies.Count > 0)
                {
                    Console.WriteLine(ex);
                    backProxyToQueue = true;
                    _currentFlareSolverrProxy = _flareSolverrProxies.Dequeue();

                    Console.WriteLine($"Попытка подключится через прокси: " +
                        $"{_currentFlareSolverrProxy.Ip}:{_currentFlareSolverrProxy.Port}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    backProxyToQueue = true;
                    await Task.Delay(6000);
                }

                if (backProxyToQueue)
                {
                    lock (locker)
                    {
                        _proxiesCheckedQueue.Enqueue(proxy);
                        Console.WriteLine("Прокси был добавлен обратно в очередь");
                    }
                }
            }

            if (threadIndex == 0)
                await StopBrowsers();
        }

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

                    Console.WriteLine($"Получаю информацию о:\n{webUrl.AbsoluteUri}");

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

                    Console.WriteLine($"Fraud Score ({proxy.RealAddress}): {iPQuality.FraudScore}");

                    if (iPQuality.FraudScore == null || (uint)iPQuality.FraudScore != 0)
                        continue;

                    iPQuality.Ip = proxy.Ip;
                    iPQuality.ProxyType = proxy.ProxyType;

                    using (var db = new DatabaseContext())
                    {
                        await db.IPQualityScoreChecks.AddAsync(iPQuality);
                        await db.SaveChangesAsync();
                    }

                    _validProxies.Add(proxy);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);

                    lock (locker)
                    {
                        if (apiTokens.Count != 0)
                        {
                            _proxiesCheckedQueue.Enqueue(proxy);
                            Console.WriteLine("Прокси был добавлен обратно в очередь");

                            currentApiToken = apiTokens.Dequeue().Token;
                            Console.WriteLine($"Попытка обновить токен на новый: {currentApiToken}");
                        }
                    }
                }
            }

            if (threadIndex == 0)
                await StopBrowsers();
        }

        private string HtmlNodeGetValue(HtmlNode htmlNode)
        {
            string value = htmlNode.InnerText;
            value = value.Replace("\n", string.Empty);
            value = value.Trim();
            return value;
        }

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

        private Uri GetProxyCheckerLink(string ip)
        {
            string url = $"https://www.ipqualityscore.com/free-ip-lookup-proxy-vpn-test/lookup/{ip}";
            return new Uri(url);
        }


        [HttpGet("Api/[controller]/CheckProcess")]
        public IActionResult CheckProcess()
        {
            if (_proxiesCheckedQueue.Count == 0)
                return Ok(100);

            int value = 100 / _proxiesCheckedTotal;
            int percent = (_proxiesCheckedTotal - _proxiesCheckedQueue.Count) * value;
            return Ok(percent);
        }

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