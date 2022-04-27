using Microsoft.AspNetCore.Mvc;
using ProxyChecker.Core;
using ProxyChecker.Core.Models;
using ProxyChecker.Database;

namespace ProxyChecker.Controllers.API
{
    /// <summary>
    /// Контроллер для обработки добавления прокси
    /// </summary>
    public class BackConnectProxyController : Controller
    {
        /// <summary>
        /// Схранит в себе список с информацией о результате работы конкретного процесса
        /// </summary>
        private static Dictionary<string, List<string>> _informationResponse = new Dictionary<string, List<string>>();

        /// <summary>
        /// Сколько времени в минутах нужно хранить результаты после последней проверки
        /// </summary>
        private const int _temporaryInformationResponse = 2;

        /// <summary>
        /// Логгер компонента
        /// </summary>
        private readonly ILogger<BackConnectProxyController> _logger;

        public BackConnectProxyController(ILogger<BackConnectProxyController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Добавляет текст в результаты вывода информации о процесса
        /// </summary>
        /// <param name="identifier">Идентификатор процесса</param>
        /// <param name="text">Строка с текстом</param>
        private void AddToLog(string identifier, string text)
        {
            if (_informationResponse.ContainsKey(identifier))
                _informationResponse[identifier].Add($"> {text}");

            _logger.LogInformation(text);
        }

        /// <summary>
        /// Выводит информацию со списком результата работы процесса.
        /// </summary>
        /// <param name="identifier">Идентификатор процесса</param>
        /// <returns>IActionResult</returns>
        [HttpGet("Api/[controller]/AddProxyListInfo/{identifier}")]
        public IActionResult AddProxyListInfo(string identifier)
        {
            if (!_informationResponse.ContainsKey(identifier))
            {
                _logger.LogWarning("Попытка получить запись по несуществующему идентификатору");
                return StatusCode(404);
            }

            return Ok(_informationResponse[identifier].ToArray());
        }

        /// <summary>
        /// Создаёт процесс и добавляет полученные прокси в обработчик. Пользователю выводится ID процесса.
        /// </summary>
        /// <param name="data">Список прокси для добавления</param>
        /// <returns>IActionResult</returns>
        [HttpPost("Api/[controller]/AddProxyList")]
        public IActionResult AddProxyList(string data)
        {
            var backConnects = Newtonsoft.Json.JsonConvert.DeserializeObject<BackConnectProxyModel[]>(data);
            if (backConnects == null)
            {
                _logger.LogWarning("Не удалось преобразовать JSON от клиента в {0}", nameof(BackConnectProxyModel));
                return StatusCode(400);
            }

            DateTime temporaryDataDeletionTime = DateTime.UtcNow.AddMinutes(_temporaryInformationResponse);
            string identifier = Guid.NewGuid().ToString();
            _informationResponse[identifier] = new List<string>();

            Task.Run(async () =>
            {
                while (temporaryDataDeletionTime > DateTime.UtcNow)
                    await Task.Delay(TimeSpan.FromSeconds(1));

                if (_informationResponse.ContainsKey(identifier))
                    _informationResponse.Remove(identifier);
            });

            for (int index = 0; index < backConnects.Length; index++)
            {
                BackConnectProxyModel backConnect = backConnects[index];

                Task.Run(async () =>
                {
                    try
                    {
                        await CheckProxy(
                            identifier,
                            backConnect.ip,
                            backConnect.port,
                            backConnect.username,
                            backConnect.password
                         );

                        temporaryDataDeletionTime = DateTime.UtcNow.AddMinutes(_temporaryInformationResponse);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                });
            }

            return StatusCode(200, identifier);
        }

        /// <summary>
        /// Проверяет прокси и добавляет его в базу данных в случае успеха.
        /// </summary>
        /// <param name="identifier">Идентификатор процесса</param>
        /// <param name="ip">IP адрес прокси</param>
        /// <param name="port">Порт прокси</param>
        /// <param name="username">Имя пользователя</param>
        /// <param name="password">Пароль пользователя</param>
        /// <returns>Task</returns>
        private async Task CheckProxy(string identifier, string ip, uint port, string username, string password)
        {
            if (string.IsNullOrEmpty(ip) || port <= 0) return;

            string authData = $"{username}:{password}@{ip}:{port}";

            using (var db = new DatabaseContext())
            {
                var entry = db.Proxies.FirstOrDefault(x =>
                    x.Username == username &&
                    x.Password == password &&
                    x.Ip == ip &&
                    x.Port == port
                );

                if (entry != null)
                {
                    AddToLog(identifier, $"Прокси \"{authData}\" уже существует в базе данных.\n" +
                        $"ID: {entry.Id}\n" +
                        $"Адрес: {entry.RealAddress}");

                    return;
                }
            }

            var httpClientModel = await HttpClientProxyChecker.GetHttpClient(ip, port, username, password);
            if (httpClientModel == null)
            {
                AddToLog(identifier, $"Не удалось определить прокси для {authData}");
                return;
            }

            try
            {
                string proxyServerAddress = string.Empty;

                using (httpClientModel.HttpClient)
                {
                    proxyServerAddress = await httpClientModel.HttpClient.GetStringAsync(new Uri("https://ip4.seeip.org"));
                }
                
                if (Core.Helpers.Validate.IsValidIpAddress(proxyServerAddress))
                {
                    using (var db = new DatabaseContext())
                    {
                        db.Proxies.Add(new Database.Models.Proxy
                        {
                            Ip = ip,
                            Port = port,
                            Username = username,
                            Password = password,
                            RealAddress = proxyServerAddress,
                            ProxyType = httpClientModel.ProxyType
                        });

                        db.SaveChanges();
                    }

                    AddToLog(identifier, $"В базу данных добавлен новый прокси: {proxyServerAddress}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                AddToLog(identifier, $"Возникло исключение при определении {authData}");
            }
        }
    }
}
