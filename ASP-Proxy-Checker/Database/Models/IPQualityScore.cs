using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace ProxyChecker.Database.Models
{
    /// <summary>
    /// Результаты сканирования прокси
    /// </summary>
    public class IPQualityScore
    {
        [Key]
        [JsonIgnore]
        public uint Id { get; set; }

        [JsonIgnore]
        public string? Ip { get; set; }

        [JsonIgnore]
        public string? ProxyType { get; set; }

        [JsonProperty("success")]
        public bool? Success { get; set; }

        [JsonProperty("message")]
        public string? Message { get; set; }

        [JsonProperty("fraud_score")]
        public int? FraudScore { get; set; }

        [JsonProperty("country_code")]
        public string? CountryCode { get; set; }

        [JsonProperty("region")]
        public string? Region { get; set; }

        [JsonProperty("city")]
        public string? City { get; set; }

        [JsonProperty("ISP")]
        public string? ISP { get; set; }

        [JsonProperty("ASN")]
        public int? ASN { get; set; }

        [JsonProperty("organization")]
        public string? Organization { get; set; }

        [JsonProperty("is_crawler")]
        public bool? IsCrawler { get; set; }

        [JsonProperty("timezone")]
        public string? Timezone { get; set; }

        [JsonProperty("mobile")]
        public bool? Mobile { get; set; }

        [JsonProperty("host")]
        public string? Host { get; set; }

        [JsonProperty("proxy")]
        public bool? Proxy { get; set; }

        [JsonProperty("vpn")]
        public bool? Vpn { get; set; }

        [JsonProperty("tor")]
        public bool? Tor { get; set; }

        [JsonProperty("active_vpn")]
        public bool? ActiveVpn { get; set; }

        [JsonProperty("active_tor")]
        public bool? ActiveTor { get; set; }

        [JsonProperty("recent_abuse")]
        public bool? RecentAbuse { get; set; }

        [JsonProperty("bot_status")]
        public bool? BotStatus { get; set; }

        [JsonProperty("connection_type")]
        public string? ConnectionType { get; set; }

        [JsonProperty("abuse_velocity")]
        public string? AbuseVelocity { get; set; }

        [JsonProperty("zip_code")]
        public string? ZipCode { get; set; }

        [JsonProperty("latitude")]
        public float? Latitude { get; set; }

        [JsonProperty("longitude")]
        public float? Longitude { get; set; }

        [JsonProperty("request_id")]
        public string? RequestId { get; set; }
    }
}
