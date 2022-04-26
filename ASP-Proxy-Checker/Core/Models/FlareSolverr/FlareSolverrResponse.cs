namespace ProxyChecker.Core.Models.FlareSolverr
{
    /// <summary>
    /// Модель для обработки <see href="https://github.com/FlareSolverr/FlareSolverr">FlareSolverr</see>
    /// </summary>
    [Serializable]
    public abstract class FlareSolverrResponse
    {
        public string? status;
        public string? message;
        public long? startTimestamp;
        public long? endTimestamp;
        public string? version;
    }
}
