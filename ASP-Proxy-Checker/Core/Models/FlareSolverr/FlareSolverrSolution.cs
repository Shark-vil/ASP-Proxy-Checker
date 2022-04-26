namespace ProxyChecker.Core.Models.FlareSolverr
{
    /// <summary>
    /// Модель для обработки <see href="https://github.com/FlareSolverr/FlareSolverr">FlareSolverr</see>
    /// </summary>
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
