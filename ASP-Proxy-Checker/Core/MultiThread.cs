namespace ProxyChecker.Core
{
    public class MultiThread
    {
        private static Dictionary<string, MultiThread> _threadsPool = new Dictionary<string, MultiThread>();
        private string _processName;
        private uint _threadsLimit = 1;
        private List<Thread> _threads = new List<Thread>();
        private object _locker = new object();
        private Action? _initAction;
        private Action? _finishAction;
        private Action<object, int>? _mainFunction;
        private Func<object, int, Task>? _mainFunctionAsync;
        private bool _consoleLog;

        public MultiThread(Action<object, int>? func = null)
        {
            _processName = Guid.NewGuid().ToString();
            _mainFunction = func;
        }

        public MultiThread(string? processName = null, Action<object, int>? func = null)
        {
            _processName = processName == null ? Guid.NewGuid().ToString() : processName;
            _mainFunction = func;
        }

        public MultiThread(Func<object, int, Task>? func = null)
        {
            _processName = Guid.NewGuid().ToString();
            _mainFunctionAsync = func;
        }

        public MultiThread(string? processName = null, Func<object, int, Task>? func = null)
        {
            _processName = processName == null ? Guid.NewGuid().ToString() : processName;
            _mainFunctionAsync = func;
        }

        public static void StopProcess(string processName)
        {
            if (_threadsPool.ContainsKey(processName))
                _threadsPool[processName].Stop();
        }

        public static bool ExistsProcess(string processName)
        {
            return _threadsPool.ContainsKey(processName);
        }

        public void SetConsoleLog(bool enable)
        {
            _consoleLog = enable;
        }

        public void SetLimit(uint threadsLimit)
        {
            _threadsLimit = threadsLimit;
        }

        public void Init(Action func)
        {
            _initAction = func;
        }

        public void Finish(Action func)
        {
            _finishAction = func;
        }

        public void Start()
        {
            if (_threadsPool.ContainsKey(_processName))
                _threadsPool[_processName].Stop();

            _threadsPool[_processName] = this;

            try
            {
                _initAction?.Invoke();
            }
            catch (Exception ex)
            {
                ConsoleLog($"[Error][MultiThreads][InitAction]: {ex}");
            }

            Task.Run(RunProcess);

            ConsoleLog($"[MultiThreads] Run - {_processName}");
        }

        public void Stop()
        {
            if (_threadsPool.ContainsKey(_processName))
                _threadsPool.Remove(_processName);

            if (_threads.Count != 0)
            {
                for (int i = 0; i < _threads.Count - 1; i++)
                {
                    Thread thread = _threads[i];
                    if (thread != null && thread.IsAlive)
                        thread?.Interrupt();
                }
            }

            ConsoleLog($"[MultiThreads] Stop - {_processName}");
        }

        private async Task RunProcess()
        {
            for (int i = 0; i < _threadsLimit; i++)
            {
                var thread = new Thread(() => ThreadProcess(i));
                thread.IsBackground = true;
                thread.Priority = ThreadPriority.Highest;
                _threads.Add(thread);
            }

            for (int i = 0; i < _threadsLimit; i++)
            {
                Thread thread = _threads[i];
                thread.Start();
                ConsoleLog($"[MultiThreads][RunProcess] Start new thread: {i}");
            }

            do
            {
                for (int i = _threads.Count - 1; i >= 0; i--)
                {
                    Thread thread = _threads[i];
                    if (thread == null || !thread.IsAlive)
                    {
                        _threads.RemoveAt(i);
                        ConsoleLog($"[MultiThreads][RunProcess] Removing dead thread: {i}");
                    }
                }

                await Task.Yield();
            } while (_threads.Count != 0);

            try
            {
                _finishAction?.Invoke();
            }
            catch (Exception ex)
            {
                ConsoleLog($"[Error][MultiThreads][FinishAction]: {ex}");
            }

            Stop();
        }

        private void ThreadProcess(int threadIndex)
        {
            try
            {
                if (_mainFunction != null)
                    _mainFunction?.Invoke(_locker, threadIndex);

                if (_mainFunctionAsync != null)
                {
                    bool isFinished = false;

                    Task.Run(async () =>
                    {
                        try
                        {
                            await _mainFunctionAsync.Invoke(_locker, threadIndex);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex);
                        }

                        isFinished = true;
                    });

                    while (!isFinished)
                    {
                        Thread.Sleep(500);
                    }
                }
            }
            catch (Exception ex)
            {
                ConsoleLog($"[Error][MultiThreads][ThreadProcess]: {ex}");
            }
        }

        private void ConsoleLog(string str)
        {
            if (!_consoleLog) return;
            Console.WriteLine(str);
        }
    }
}
