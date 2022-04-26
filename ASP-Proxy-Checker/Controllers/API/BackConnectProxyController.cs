using Microsoft.AspNetCore.Mvc;
using ProxyChecker.Core;
using ProxyChecker.Core.Models;
using ProxyChecker.Database;

namespace ProxyChecker.Controllers.API
{
    //[Route("api/[controller]")]
    //[ApiController]
    public class BackConnectProxyController : Controller
    {
        private static Dictionary<string, List<string>> _newProxyRegistred = new Dictionary<string, List<string>>();
        private const int _temporaryProxyRegistredTimeMinutes = 2;

        private void AddToLog(string identifier, string text)
        {
            if (_newProxyRegistred.ContainsKey(identifier))
                _newProxyRegistred[identifier].Add(text);

            Console.WriteLine(text);
        }

        [HttpGet("Api/[controller]/AddProxyListInfo/{identifier}")]
        public IActionResult AddProxyListInfo(string identifier)
        {
            if (!_newProxyRegistred.ContainsKey(identifier))
                return StatusCode(404);

            return Ok(_newProxyRegistred[identifier].ToArray());
        }

        [HttpPost("Api/[controller]/AddProxyList")]
        public IActionResult AddProxyList(string data)
        {
            var backConnects = Newtonsoft.Json.JsonConvert.DeserializeObject<BackConnectProxyModel[]>(data);
            if (backConnects == null) return StatusCode(400);

            DateTime temporaryDataDeletionTime = DateTime.UtcNow.AddMinutes(_temporaryProxyRegistredTimeMinutes);
            string identifier = Guid.NewGuid().ToString();
            _newProxyRegistred[identifier] = new List<string>();

            Task.Run(async () =>
            {
                while (temporaryDataDeletionTime > DateTime.UtcNow)
                    await Task.Delay(TimeSpan.FromSeconds(1));

                if (_newProxyRegistred.ContainsKey(identifier))
                    _newProxyRegistred.Remove(identifier);
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
                            backConnect.password,
                            backConnects.Length
                         );

                        temporaryDataDeletionTime = DateTime.UtcNow.AddMinutes(_temporaryProxyRegistredTimeMinutes);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                });
            }

            return StatusCode(200, identifier);
        }

        private async Task CheckProxy(string identifier, string ip, uint port, string username, string password, int totalChecks)
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
                    AddToLog(identifier, $"> Прокси \"{authData}\" уже существует в базе данных.\n" +
                        $"ID: {entry.Id}\n" +
                        $"Адрес: {entry.RealAddress}");

                    return;
                }
            }

            var httpClientModel = await HttpClientProxyChecker.GetHttpClient(ip, port, username, password);
            if (httpClientModel == null)
            {
                AddToLog(identifier, $"> Не удалось определить прокси для {authData}");
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

                    AddToLog(identifier, $"> В базу данных добавлен новый прокси: {proxyServerAddress}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                AddToLog(identifier, $"> Возникло исключение при определении {authData}");
            }
        }
    }
}
