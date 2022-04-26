namespace ProxyChecker.Core.Models.FlareSolverr
{
    /// <summary>
    /// Модель для обработки <see href="https://github.com/FlareSolverr/FlareSolverr">FlareSolverr</see>
    /// </summary>
    [Serializable]
    public class FlareSolverrRequestList : FlareSolverrRequest
    {
        internal FlareSolverrRequestList()
        {
            cmd = "sessions.list";
        }
    }
}
