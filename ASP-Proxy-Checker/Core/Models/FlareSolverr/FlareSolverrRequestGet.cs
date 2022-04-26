namespace ProxyChecker.Core.Models.FlareSolverr
{
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
