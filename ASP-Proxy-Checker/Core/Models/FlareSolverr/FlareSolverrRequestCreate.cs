namespace ProxyChecker.Core.Models.FlareSolverr
{
    /// <summary>
    /// Модель для обработки <see href="https://github.com/FlareSolverr/FlareSolverr">FlareSolverr</see>
    /// </summary>
    [Serializable]
    public class FlareSolverrRequestCreate : FlareSolverrRequest
    {
        public string? session;
        public FlareSolverrProxy? proxy;

        public FlareSolverrRequestCreate()
        {
            cmd = "sessions.create";
        }
    }
}
