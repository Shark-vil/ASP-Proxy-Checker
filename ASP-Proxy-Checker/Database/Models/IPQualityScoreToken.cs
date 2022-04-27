using System.ComponentModel.DataAnnotations;

namespace ProxyChecker.Database.Models
{
    /// <summary>
    /// Токен для обмена с API сканера прокси
    /// </summary>
    public class IPQualityScoreToken
    {
        [Key]
        public uint Id { get; set; }
        public string Token { get; set; }
    }
}
