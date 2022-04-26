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

        // GET: Home/Config
        public IActionResult Config()
        {
            Database.Models.Config config;

            using (var db = new Database.DatabaseContext())
            {
                var entry = db.Configuration.FirstOrDefault();
                if (entry != null)
                {
                    config = entry;
                }
                else
                {
                    config = new Database.Models.Config
                    {
                        Id = 1,
                        FlareSolverrUrl = "",
                        AdminUsername = "",
                        AdminPassword = ""
                    };
                }
            }

            var viewModelConfig = new Core.Models.Views.ViewConfigModel
            {
                FlareSolverrUrl = config.FlareSolverrUrl,
                AdminUsername = config.AdminUsername,
                AdminPassword = config.AdminPassword
            };

            return View(viewModelConfig);
        }

        [HttpPost]
        public async Task<ActionResult> Config(Core.Models.Views.ViewConfigModel viewModelConfig)
        {
            using (var db = new Database.DatabaseContext())
            {
                Database.Models.Config? entry = db.Configuration.FirstOrDefault();

                if (entry == null)
                    entry = new Database.Models.Config();

                entry.FlareSolverrUrl = viewModelConfig.FlareSolverrUrl == null ? "" : viewModelConfig.FlareSolverrUrl;
                entry.AdminUsername = viewModelConfig.AdminUsername == null ? "" : viewModelConfig.AdminUsername;
                entry.AdminPassword = viewModelConfig.AdminPassword == null ? "" : viewModelConfig.AdminPassword;

                if (entry.Id == 0)
                    db.Configuration.Add(entry);
                else
                    db.Configuration.Update(entry);

                await db.SaveChangesAsync();
            }

            return View("Config", viewModelConfig);
        }

        // GET: Error page
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}