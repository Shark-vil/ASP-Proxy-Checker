using ProxyChecker.Core.Models;
using System.Net;

namespace ProxyChecker.Core
{
    /// <summary>
    /// Класс для создания объектов HttpClient с настроенными прокси 
    /// </summary>
    public class HttpClientProxyChecker
    {
        //private static TimeSpan _timeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Страница для проверки доступности через прокси
        /// </summary>
        private static Uri _proxyValidateAddressCheck = new Uri("http://google.com/generate_204");

        /// <summary>
        /// Список допустимых типов прокси
        /// </summary>
        private static string[] _proxyTypes = new string[]
        {
            "http",
            "https",
            "socks4",
            "socks4a",
            "socks5"
        };

        /// <summary>
        /// Возвращает клиента с настроенными параметрами прокси для обмена. Если настройка не удалась, вернёт - NULL.
        /// </summary>
        /// <param name="ip">Адрес прокси</param>
        /// <param name="port">Порт прокси</param>
        /// <param name="username">Имя пользователя</param>
        /// <param name="password">Пароль пользователя</param>
        /// <returns>Модель "HttpClientProxyModel" с типом прокси и клиентом для вызова</returns>
        internal static async Task<HttpClientProxyModel?> GetHttpClient(string ip, uint port, string username = "", string password = "")
        {
            try
            {
                string? peoxyType = await DetectProxyType(ip, port, username, password);
                if (peoxyType != null)
                {
                    var httpClientModel = new HttpClientProxyModel();
                    httpClientModel.HttpClient = new HttpClient(GetProxyHandler(peoxyType, ip, port, username, password));
                    httpClientModel.ProxyType = peoxyType;
                    return httpClientModel;
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"[GetHttpClient] {ex}");
            }

            return null;
        }

        /// <summary>
        /// Возвращает тип определённого прокси. Если тип не удалось определить, вернёт - NULL.
        /// </summary>
        /// <param name="ip">Адрес прокси</param>
        /// <param name="port">Порт прокси</param>
        /// <param name="username">Имя пользователя</param>
        /// <param name="password">Пароль пользователя</param>
        /// <returns>Тип прокси или NULL в случае неудачи</returns>
        private static async Task<string?> DetectProxyType(string ip, uint port, string username, string password)
        {
            HttpClient? resultHttpClient = null;

            foreach (string proxyType in _proxyTypes)
            {
                //Console.WriteLine($"[DetectProxyType][{proxyType.ToUpper()}] Check proxy type");

                try
                {
                    using (resultHttpClient = new HttpClient(GetProxyHandler(proxyType, ip, port, username, password)))
                    {
                        //resultHttpClient.Timeout = _timeout;
                        await resultHttpClient.GetStringAsync(_proxyValidateAddressCheck);
                        return proxyType;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DetectProxyType][{proxyType.ToUpper()}] {ex}");
                }
            }

            return null;
        }

        /// <summary>
        /// Возвращает обработчик прокси для HTTP Client.
        /// </summary>
        /// <param name="proxyType">Тип прокси</param>
        /// <param name="ip">Адрес прокси</param>
        /// <param name="port">Порт прокси</param>
        /// <param name="username">Имя пользователя</param>
        /// <param name="password">Пароль пользователя</param>
        /// <returns>Обработчик "HttpClientHandler" для работы прокси в "HttpClient"</returns>
        private static HttpClientHandler GetProxyHandler(string proxyType, string ip, uint port, string username, string password)
        {
            return new HttpClientHandler
            {
                Proxy = new WebProxy
                {
                    Address = new Uri($"{proxyType}://{ip}:{port}"),
                    BypassProxyOnLocal = false,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(username, password)
                },
            };
        }
    }
}
