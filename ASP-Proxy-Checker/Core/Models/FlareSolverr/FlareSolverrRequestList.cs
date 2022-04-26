namespace ProxyChecker.Core.Models.FlareSolverr
{
    [Serializable]
    public class FlareSolverrRequestList : FlareSolverrRequest
    {
        internal FlareSolverrRequestList()
        {
            cmd = "sessions.list";
        }
    }
}
