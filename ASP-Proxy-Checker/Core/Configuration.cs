using ProxyChecker.Database;
using ProxyChecker.Database.Models;

namespace ProxyChecker.Core
{
    public class Configuration
    {
        public static Config GetConfig()
        {
            Config config;

            using (var db = new DatabaseContext())
                config = db.Configuration.FirstOrDefault(x => x.Id == 1);

            return config;
        }

        public static void SetFlareSolverrUrl(string flareSolverrUrl)
        {
            using (var db = new DatabaseContext())
            {
                var entry = db.Configuration.FirstOrDefault(x => x.Id == 1);
                entry.FlareSolverrUrl = flareSolverrUrl;
                db.SaveChanges();
            }
        }

        public static void SetAdminUsername(string adminUsername)
        {
            using (var db = new DatabaseContext())
            {
                var entry = db.Configuration.FirstOrDefault(x => x.Id == 1);
                entry.AdminUsername = adminUsername;
                db.SaveChanges();
            }
        }

        public static void SetAdminPassword(string adminPassword)
        {
            using (var db = new DatabaseContext())
            {
                var entry = db.Configuration.FirstOrDefault(x => x.Id == 1);
                entry.AdminPassword = adminPassword;
                db.SaveChanges();
            }
        }
    }
}
