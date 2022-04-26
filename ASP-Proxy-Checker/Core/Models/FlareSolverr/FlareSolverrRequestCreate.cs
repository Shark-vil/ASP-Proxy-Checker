namespace ProxyChecker.Core.Models.FlareSolverr
{
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
