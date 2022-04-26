namespace ProxyChecker.Core.Models.FlareSolverr
{
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
