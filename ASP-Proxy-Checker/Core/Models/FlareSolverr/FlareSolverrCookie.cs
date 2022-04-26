namespace ProxyChecker.Core.Models.FlareSolverr
{
    /// <summary>
    /// Модель для обработки <see href="https://github.com/FlareSolverr/FlareSolverr">FlareSolverr</see>
    /// </summary>
    [Serializable]
    public class FlareSolverrCookie
    {
        public string? name;
        public string? value;
        public string? domain;
        public string? path;
        public double? expires;
        public int? size;
        public bool? httpOnly;
        public bool? secure;
        public bool? session;
        public string? sameSite;
    }
}
