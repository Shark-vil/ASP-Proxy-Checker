using ProxyChecker.Database;
using ProxyChecker.Database.Models;
using System.Data;

namespace ProxyChecker.Core
{
    /// <summary>
    /// Сервис для проверки и обновления прокси при старте и через каждые 10 минут
    /// </summary>
    public class UpdateProxyChecker
    {
        /// <summary>
        /// Адрес сайта для получения фактического IP прокси
        /// </summary>
        private static Uri _seeIpUri = new Uri("https://ip4.seeip.org");

        /// <summary>
        /// Очередь прокси на обновление
        /// </summary>
        private static Queue<Proxy> _proxiesQueueUpdate = new Queue<Proxy>();

        /// <summary>
        /// Логгер компонента
        /// </summary>
        private static ILogger<UpdateProxyChecker> _logger = LogService.LoggerFactory.CreateLogger<UpdateProxyChecker>();

        /// <summary>
        /// Запустить сервис
        /// </summary>
        public static void Run()
        {
            _logger.LogInformation("Вызов запуска фонового процесса отслеживания обновлений прокси");

            Task.Run(async () => await RunAsync());
        }

        /// <summary>
        /// Запустить сервис асинхроннл
        /// </summary>
        /// <returns>Task</returns>
        private static async Task RunAsync()
        {
            while (true)
            {
                _logger.LogInformation("Проверка на наличие обновлений прокси");

                try
                {
                    using (var db = new DatabaseContext())
                    {
                        foreach (var entry in db.Proxies)
                        {
                            try
                            {
                                var httpClientModel = await HttpClientProxyChecker.GetHttpClient(entry.Ip, entry.Port, entry.Username, entry.Password);
                                if (httpClientModel == null) continue;

                                string proxyServerAddress = await httpClientModel.HttpClient.GetStringAsync(_seeIpUri);

                                if (proxyServerAddress != entry.RealAddress)
                                {
                                    _logger.LogInformation("Зафиксировано изменение: {0} -> {1}. Начинаю процесс обновления", entry.RealAddress, proxyServerAddress);

                                    _proxiesQueueUpdate = new Queue<Proxy>(db.Proxies);

                                    var multiThread = new MultiThread(UpdateEveryone);
                                    multiThread.Finish(() =>
                                    {
                                        _logger.LogInformation("Все прокси были обновлены");
                                    });
                                    multiThread.SetLimit(10);
                                    multiThread.Start();
                                }
                                else
                                {
                                    _logger.LogInformation("Изменений не зафиксированно");
                                }

                                break;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex.ToString());
                            }
                        }
                    }
                }
                catch (DataException ex)
                {
                    _logger.LogError(ex.ToString());
                }

                await Task.Delay(TimeSpan.FromMinutes(10));
            }
        }

        /// <summary>
        /// Обновить все фактические адреса прокси
        /// </summary>
        /// <param name="locker">Объект для синхронизации потоков</param>
        /// <param name="threadIndex">Индекс текущего потока</param>
        /// <returns>Task</returns>
        private static async Task UpdateEveryone(object locker, int threadIndex)
        {
            while (_proxiesQueueUpdate.Count > 0)
            {
                Proxy entry;

                lock (locker)
                {
                    if (_proxiesQueueUpdate.Count == 0)
                        break;

                    entry = _proxiesQueueUpdate.Dequeue();
                }

                try
                {
                    var httpClientModel = await HttpClientProxyChecker.GetHttpClient(entry.Ip, entry.Port, entry.Username, entry.Password);
                    if (httpClientModel != null)
                    {
                        string proxyServerAddress = await httpClientModel.HttpClient.GetStringAsync(_seeIpUri);
                        if (Helpers.Validate.IsValidIpAddress(proxyServerAddress))
                        {
                            lock (locker)
                            {
                                uint tempId = entry.Id;
                                string tempRealAddress = entry.RealAddress;

                                entry.RealAddress = proxyServerAddress;
                                entry.ProxyType = httpClientModel.ProxyType;

                                try
                                {
                                    using (var db = new DatabaseContext())
                                    {
                                        db.Proxies.Update(entry);
                                        db.SaveChanges();
                                    }

                                    _logger.LogInformation($"Обновление прокси #{tempId}: {tempRealAddress} --> {proxyServerAddress} ({httpClientModel.ProxyType})");
                                }
                                catch (DataException ex)
                                {
                                    _logger.LogError(ex.ToString());
                                }
                            }
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
}
