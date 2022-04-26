namespace ProxyChecker.Core.Models
{
    /// <summary>
    /// Модель возвращаемая через "HttpClientProxyChecker"
    /// </summary>
    public class HttpClientProxyModel
    {
        public HttpClient HttpClient;
        public string ProxyType;
    }
}
