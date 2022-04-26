using ProxyChecker.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace ProxyChecker.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        // GET: Home
        public IActionResult Index() => View();

        // GET: Home/Privacy
        public IActionResult Privacy() => View();

        // GET: Home/ApiTokens
        public IActionResult ApiTokens() => View();

        // GET: Home/ProxyList
        public IActionResult ProxyList() => View();

        // GET: Home/BlockedList
        public IActionResult BlockedList() => View();

        // GET: Home/AddProxy
        public IActionResult AddProxy() => View();

        // GET: Home/CheckProxy
        public IActionResult CheckProxy() => View();

        // GET: Home/AddFlareSolverrProxy
        public IActionResult AddFlareSolverrProxy() => View();

        // GET: Error page
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}