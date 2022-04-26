using ProxyChecker.Core;
using ProxyChecker.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.AspNetCore.Html;

namespace ProxyChecker.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        private static Dictionary<string, List<string>?> _waitProcess = new Dictionary<string, List<string>?>();

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        [HttpPost()]
        public IActionResult StartThreads(string identifier)
        {
            var rnd = new Random();
            var testList = new List<string>();

            var multiThread = new MultiThread(identifier, (object locker, int threadIndex) =>
            {
                Thread.Sleep(rnd.Next(1000, 10000));

                lock (locker)
                {
                    testList.Add(Guid.NewGuid().ToString());
                }
            });

            multiThread.Init(() =>
            {
                _waitProcess[identifier] = null;
            });

            multiThread.Finish(() =>
            {
                _waitProcess[identifier] = testList;
            });

            multiThread.SetConsoleLog(false);
            multiThread.SetLimit(100);
            multiThread.Start();

            return StatusCode(200);
        }

        [HttpPost()]
        public IActionResult StopThreads(string identifier)
        {
            MultiThread.StopProcess(identifier);
            return StatusCode(200);
        }

        [HttpGet("[controller]/Test/{identifier}")]
        public IActionResult Test(string identifier)
        {
            if (!_waitProcess.ContainsKey(identifier))
                return StatusCode((int)HttpStatusCode.NotFound);
            else if (_waitProcess[identifier] == null)
                return StatusCode((int)HttpStatusCode.Continue, "Request being processed");

#pragma warning disable CS8602 // Разыменование вероятной пустой ссылки.
            var result = new List<string>(_waitProcess[identifier].ToArray());
#pragma warning restore CS8602 // Разыменование вероятной пустой ссылки.
            _waitProcess.Remove(identifier);

            return new ContentResult
            {
                ContentType = "text/html",
                StatusCode = (int)HttpStatusCode.OK,
                Content = string.Join("<br/>", result.ToArray())
            };
        }

        public IActionResult Index() => View();

        public IActionResult Privacy() => View();

        public IActionResult ApiTokens() => View();

        public IActionResult ProxyList() => View();

        public IActionResult BlockedList() => View();

        public IActionResult AddProxy() => View();

        public IActionResult CheckProxy() => View();

        public IActionResult AddFlareSolverrProxy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}