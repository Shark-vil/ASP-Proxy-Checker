﻿namespace ProxyChecker.Core.Models.FlareSolverr
{
    /// <summary>
    /// Модель для обработки <see href="https://github.com/FlareSolverr/FlareSolverr">FlareSolverr</see>
    /// </summary>
    [Serializable]
    public class FlareSolverrResponseDestroy : FlareSolverrResponse
    {
        public string? session;
    }
}
