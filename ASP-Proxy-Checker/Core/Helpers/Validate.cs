using System.Text.RegularExpressions;

namespace ProxyChecker.Core.Helpers
{
    /// <summary>
    /// Обработка валидаторов данных
    /// </summary>
    public class Validate
    {
        /// <summary>
        /// Паттерн для проверки IP адреса через "Regex"
        /// </summary>
        private const string _ipPattern = @"^([1-9]|[1-9][0-9]|1[0-9][0-9]|2[0-4][0-9]|25[0-5])(\.([0-9]|[1-9][0-9]|1[0-9][0-9]|2[0-4][0-9]|25[0-5])){3}$";

        /// <summary>
        /// Проверяет валидность строки на IP адрес
        /// </summary>
        /// <param name="address">Любая строка</param>
        /// <returns>Вернёт - True, если IP адрес валидный. Иначе - False.</returns>
        public static bool IsValidIpAddress(string address)
        {
            if (string.IsNullOrEmpty(address))
                return false;
            else
                return new Regex(_ipPattern).IsMatch(address, 0);
        }
    }
}
