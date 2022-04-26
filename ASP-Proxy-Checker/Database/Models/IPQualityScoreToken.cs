using System.ComponentModel.DataAnnotations;

namespace ProxyChecker.Database.Models
{
    public class IPQualityScoreToken
    {
        [Key]
        public uint Id { get; set; }
        public string Token { get; set; }
    }
}
