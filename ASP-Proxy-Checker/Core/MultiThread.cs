namespace ProxyChecker.Core
{
    /// <summary>
    /// Класс "MultiThread" способен запускать несколько одновременных потоков для выполенния одной задачи.
    /// </summary>
    public class MultiThread
    {
        /// <summary>
        /// Список всех активных потоков в формате > [ Ключ, Класс MultiThread ]
        /// </summary>
        private static Dictionary<string, MultiThread> _threadsPool = new Dictionary<string, MultiThread>();

        /// <summary>
        /// Имя/ID текущего процесса (мультипотока)
        /// </summary>
        private string _processName;

        /// <summary>
        /// Количество одновременных потоков для запуска
        /// </summary>
        private uint _threadsLimit = 1;

        /// <summary>
        /// Список всех активных потоков
        /// </summary>
        private List<Thread> _threads = new List<Thread>();

        /// <summary>
        /// Объект для синхронизации потоков
        /// </summary>
        private object _locker = new object();

        /// <summary>
        /// Событие вызывается перед инициализацией всех потоков
        /// </summary>
        private Action? _initAction;

        /// <summary>
        /// Событие вызывается при завершении всех потоков
        /// </summary>
        private Action? _finishAction;

        /// <summary>
        /// Основная функция вызываемая в потоках каждый тик
        /// </summary>
        private Action<object, int>? _mainFunction;

        /// <summary>
        /// Основная асинхронная функция вызываемая в потоках каждый тик
        /// </summary>
        private Func<object, int, Task>? _mainFunctionAsync;

        /// <summary>
        /// Логгер компонента
        /// </summary>
        private readonly ILogger<MultiThread> _logger;

        /// <summary>
        /// Конструктор мультипотока.
        /// </summary>
        /// <param name="func">Основная функция вызываемая в потоках каждый тик</param>
        public MultiThread(Action<object, int>? func = null)
        {
            _processName = Guid.NewGuid().ToString();
            _mainFunction = func;
            _logger = LogService.LoggerFactory.CreateLogger<MultiThread>();
        }

        /// <summary>
        /// Конструктор мультипотока.
        /// </summary>
        /// <param name="processName">Идентификатор мультипотока. Если NULL - будет назначен случайный ID</param>
        /// <param name="func">Основная функция вызываемая в потоках каждый тик</param>
        public MultiThread(string? processName = null, Action<object, int>? func = null)
        {
            _processName = processName == null ? Guid.NewGuid().ToString() : processName;
            _mainFunction = func;
            _logger = LogService.LoggerFactory.CreateLogger<MultiThread>();
        }

        /// <summary>
        /// Конструктор мультипотока.
        /// </summary>
        /// <param name="func">Основная асинхронная функция вызываемая в потоках каждый тик</param>
        public MultiThread(Func<object, int, Task>? func = null)
        {
            _processName = Guid.NewGuid().ToString();
            _mainFunctionAsync = func;
            _logger = LogService.LoggerFactory.CreateLogger<MultiThread>();
        }

        /// <summary>
        /// Конструктор мультипотока.
        /// </summary>
        /// <param name="processName">Идентификатор мультипотока. Если NULL - будет назначен случайный ID</param>
        /// <param name="func">Основная асинхронная функция вызываемая в потоках каждый тик</param>
        public MultiThread(string? processName = null, Func<object, int, Task>? func = null)
        {
            _processName = processName == null ? Guid.NewGuid().ToString() : processName;
            _mainFunctionAsync = func;
            _logger = LogService.LoggerFactory.CreateLogger<MultiThread>();
        }

        /// <summary>
        /// Останавливает мультипоток по его идентификатору.
        /// </summary>
        /// <param name="processName">Индентификатор мультипотока</param>
        public static void StopProcess(string processName)
        {
            if (_threadsPool.ContainsKey(processName))
                _threadsPool[processName].Stop();
        }

        /// <summary>
        /// Проверяет, активен ли указанный мультипоток в данный момент.
        /// </summary>
        /// <param name="processName">Индентификатор мультипотока</param>
        /// <returns>True - если активен, иначе - False</returns>
        public static bool ExistsProcess(string processName)
        {
            return _threadsPool.ContainsKey(processName);
        }

        /// <summary>
        /// Устанавливает максимальное количество одновременно запускаемых потоков.
        /// </summary>
        /// <param name="threadsLimit">Число от 1 и выше</param>
        public void SetLimit(uint threadsLimit)
        {
            _threadsLimit = threadsLimit == 0 ? 1 : threadsLimit;
        }

        /// <summary>
        /// Устанавливает функцию вызова перед инициализацией всех мультипотоков.
        /// </summary>
        /// <param name="func">Action</param>
        public void Init(Action func)
        {
            _initAction = func;
        }

        /// <summary>
        /// Устанавливает функцию вызова при завершении всех мультипотоков.
        /// </summary>
        /// <param name="func">Action</param>
        public void Finish(Action func)
        {
            _finishAction = func;
        }

        /// <summary>
        /// Запускает мультипоток и добавляет себя в общий список.
        /// </summary>
        public void Start()
        {
            _logger.LogInformation("Запуск мультипотока: {0}", _processName);

            if (_threadsPool.ContainsKey(_processName))
                _threadsPool[_processName].Stop();

            _threadsPool[_processName] = this;

            try
            {
                _initAction?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }

            Task.Run(RunProcess);
        }

        /// <summary>
        /// Останавливает мультипоток и удаляет себя из общего списка.
        /// </summary>
        public void Stop()
        {
            _logger.LogInformation("Остановка мультипотока: {0}", _processName);

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
        }

        /// <summary>
        /// Задача, вызываемая при инициализации всех потоков.
        /// </summary>
        /// <returns>Task</returns>
        private async Task RunProcess()
        {
            _logger.LogInformation("Инициализация компонентов мультипотока: {0}", _processName);

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
                _logger.LogInformation("Мультипоток \"{0}\" запустил поток \"{1}\"", _processName, i);
            }

            do
            {
                for (int i = _threads.Count - 1; i >= 0; i--)
                {
                    Thread thread = _threads[i];
                    if (thread == null || !thread.IsAlive)
                    {
                        _threads.RemoveAt(i);
                        _logger.LogInformation("Мультипоток \"{0}\" остановил мёртвый поток \"{1}\"", _processName, i);
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
                _logger.LogError(ex.ToString());
            }

            _logger.LogInformation("Мультипоток \"{0}\" завершает работу", _processName);

            Stop();
        }

        /// <summary>
        /// Метод выполняемый во всех запущеных потоках.
        /// </summary>
        /// <param name="threadIndex">Индекс запущенного потока</param>
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
                            _logger.LogError(ex.ToString());
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
                _logger.LogError(ex.ToString());
            }
        }
    }
}
