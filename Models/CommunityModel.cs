using Newtonsoft.Json;

namespace ComunioAdminTools.Models
{
    public class CommunityModel
    {
        [JsonProperty("id")]
        public string Id { get; set; } = default!;

        [JsonProperty("name")]
        public string Name { get; set; } = default!;
    }
}
