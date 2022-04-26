namespace ProxyChecker.Core.Models.FlareSolverr
{
    [Serializable]
    public class FlareSolverrRequestDestroy : FlareSolverrRequest
    {
        public string session;

        public FlareSolverrRequestDestroy()
        {
            cmd = "sessions.destroy";
        }
    }
}
