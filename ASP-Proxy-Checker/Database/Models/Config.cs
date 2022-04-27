using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ProxyChecker.Database.Models
{
    /// <summary>
    /// Конфигурация настроек сайта
    /// </summary>
    public class Config
    {
        [Key]
        public uint Id { get; set; }

        [DefaultValue("")]
        public string FlareSolverrUrl { get; set; }

        [DefaultValue("")]
        public string AdminUsername { get; set; }

        [DefaultValue("")]
        public string AdminPassword { get; set; }
    }
}
