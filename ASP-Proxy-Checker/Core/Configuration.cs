using ProxyChecker.Database;
using ProxyChecker.Database.Models;

namespace ProxyChecker.Core
{
    /// <summary>
    /// Класс для настройки конфигурации сервера
    /// </summary>
    public class Configuration
    {
        /// <summary>
        /// Получить конфигурацию сервера.
        /// </summary>
        /// <returns>Модель "Config"</returns>
        public static Config GetConfig()
        {
            Config config;

            using (var db = new DatabaseContext())
                config = db.Configuration.FirstOrDefault(x => x.Id == 1);

            return config;
        }

        /// <summary>
        /// Устанавливает адрес для обработки запросов к CloudFlare.
        /// </summary>
        /// <param name="flareSolverrUrl">Строка с URL адресом</param>
        public static void SetFlareSolverrUrl(string flareSolverrUrl)
        {
            using (var db = new DatabaseContext())
            {
                var entry = db.Configuration.FirstOrDefault(x => x.Id == 1);
                entry.FlareSolverrUrl = flareSolverrUrl;
                db.SaveChanges();
            }
        }

        /// <summary>
        /// Устанавливает имя пользователя панели.
        /// </summary>
        /// <param name="adminUsername">Имя пользователя</param>
        public static void SetAdminUsername(string adminUsername)
        {
            using (var db = new DatabaseContext())
            {
                var entry = db.Configuration.FirstOrDefault(x => x.Id == 1);
                entry.AdminUsername = adminUsername;
                db.SaveChanges();
            }
        }

        /// <summary>
        /// Устанавливает пароль пользователя панели.
        /// </summary>
        /// <param name="adminPassword">Пароль пользователя</param>
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
