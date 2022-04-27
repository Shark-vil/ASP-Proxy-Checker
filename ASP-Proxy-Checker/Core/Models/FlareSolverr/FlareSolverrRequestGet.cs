namespace ProxyChecker.Core.Models.FlareSolverr
{
    /// <summary>
    /// Модель для обработки <see href="https://github.com/FlareSolverr/FlareSolverr">FlareSolverr</see>
    /// </summary>
    [Serializable]
    public class FlareSolverrRequestGet : FlareSolverrRequest
    {
        public string url;
        public string? session;
        public uint? maxTimeout;
        public FlareSolverrCookie[]? cookies;
        public bool? returnOnlyCookies;
        public FlareSolverrProxy? proxy;

        internal FlareSolverrRequestGet()
        {
            cmd = "request.get";
        }
    }
}
