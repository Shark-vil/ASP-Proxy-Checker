using System.ComponentModel.DataAnnotations;

namespace ProxyChecker.Database.Models
{
    /// <summary>
    /// Заблокированный прокси
    /// </summary>
    public class BlockedProxy
    {
        [Key]
        public uint Id { get; set; }
        public string Ip { get; set; }
        public string Mask { get; set; }
    }
}
