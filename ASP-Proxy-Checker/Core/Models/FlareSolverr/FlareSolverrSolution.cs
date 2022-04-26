namespace ProxyChecker.Core.Models.FlareSolverr
{
    [Serializable]
    public class FlareSolverrSolution
    {
        public string? url;
        public int? status;
        public string? response;
        public FlareSolverrHeader? headers;
        public FlareSolverrCookie[]? cookies;
    }
}
