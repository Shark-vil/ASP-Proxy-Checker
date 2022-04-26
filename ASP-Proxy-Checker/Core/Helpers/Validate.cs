using System.Text.RegularExpressions;

namespace ProxyChecker.Core.Helpers
{
    public class Validate
    {
        private const string _ipPattern = @"^([1-9]|[1-9][0-9]|1[0-9][0-9]|2[0-4][0-9]|25[0-5])(\.([0-9]|[1-9][0-9]|1[0-9][0-9]|2[0-4][0-9]|25[0-5])){3}$";

        public static bool IsValidIpAddress(string address)
        {
            if (string.IsNullOrEmpty(address))
                return false;
            else
                return new Regex(_ipPattern).IsMatch(address, 0);
        }
    }
}
