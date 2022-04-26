using ProxyChecker.Database;
using ProxyChecker.Database.Models;
using System.Data;

namespace ProxyChecker.Core
{
    public class UpdateProxyChecker
    {
        private static Uri _seeIpUri = new Uri("https://ip4.seeip.org");
        private static Queue<Proxy> _proxiesQueueUpdate = new Queue<Proxy>();

        public static void Run()
        {
            Task.Run(async () => await RunAsync());
        }

        private static async Task RunAsync()
        {
            while (true)
            {
                Console.WriteLine("Запуск проверки на обновление прокси");

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
                                    Console.WriteLine("Один из прокси был изменён, начинаем проверку остальных");

                                    _proxiesQueueUpdate = new Queue<Proxy>(db.Proxies);

                                    var multiThread = new MultiThread(UpdateEveryone);
                                    multiThread.Finish(() =>
                                    {
                                        Console.WriteLine("Все прокси были обновлены");
                                    });
                                    multiThread.SetLimit(10);
                                    multiThread.Start();
                                }
                                else
                                {
                                    Console.WriteLine("Изменений не зафиксированно");
                                }

                                break;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex);
                            }
                        }
                    }
                }
                catch (DataException ex)
                {
                    Console.WriteLine(ex);
                }

                await Task.Delay(TimeSpan.FromMinutes(10));
            }
        }

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
                                Console.WriteLine($"Обновление прокси #{entry.Id}: {entry.RealAddress} --> {proxyServerAddress} ({httpClientModel.ProxyType})");
                                entry.RealAddress = proxyServerAddress;
                                entry.ProxyType = httpClientModel.ProxyType;

                                try
                                {
                                    using (var db = new DatabaseContext())
                                    {
                                        db.Proxies.Update(entry);
                                        db.SaveChanges();
                                    }
                                }
                                catch (DataException ex)
                                {
                                    Console.WriteLine(ex);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
        }
    }
}
