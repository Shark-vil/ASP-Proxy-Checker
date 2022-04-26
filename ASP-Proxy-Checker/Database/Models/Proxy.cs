using System.ComponentModel.DataAnnotations;

namespace ProxyChecker.Database.Models
{
    public class Proxy
    {
        [Key]
        public uint Id { get; set; }
        public string Ip { get; set; }
        public uint Port { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string RealAddress { get; set; }
        public string ProxyType { get; set; }
    }
}
