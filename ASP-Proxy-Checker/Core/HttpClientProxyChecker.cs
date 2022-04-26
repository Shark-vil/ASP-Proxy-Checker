using ProxyChecker.Core.Models;
using System.Net;

namespace ProxyChecker.Core
{
    public class HttpClientProxyChecker : HttpClient
    {
        private static TimeSpan _timeout = TimeSpan.FromSeconds(5);
        private static Uri _proxyValidateAddressCheck = new Uri("http://google.com/generate_204");
        private static string[] _proxyTypes = new string[]
        {
            "http",
            "https",
            "socks4",
            "socks4a",
            "socks5"
        };

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
