using Newtonsoft.Json;

namespace ProxyChecker.Core.Models.FlareSolverr
{
    [Serializable]
    public class FlareSolverrHeader
    {
        public int? status;
        public string? date;
        public string? expires;
        public string? server;
        public string? p3p;

        [JsonProperty("cache-control")]
        public string? cache_control;

        [JsonProperty("content-type")]
        public string? content_type;

        [JsonProperty("strict-transport-security")]
        public string? strict_transport_security;

        [JsonProperty("content-encoding")]
        public string? content_encoding;

        [JsonProperty("content-length")]
        public string? content_length;

        [JsonProperty("x-xss-protection")]
        public string? x_xss_protection;

        [JsonProperty("x-frame-option")]
        public string? x_frame_option;

        [JsonProperty("set-cookie")]
        public string? set_cookie;
    }
}
