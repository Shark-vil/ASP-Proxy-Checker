using System.ComponentModel.DataAnnotations;

namespace ProxyChecker.Database.Models
{
    /// <summary>
    /// Модель пользовательских прокси для обхода CloudFlare
    /// </summary>
    public class FlareSolverrProxy
    {
        [Key]
        public uint Id { get; set; }
        public string Ip { get; set; }
        public uint Port { get; set; }
        public string ProxyType { get; set; }
    }
}
