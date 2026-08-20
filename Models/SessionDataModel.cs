using Newtonsoft.Json;

namespace ComunioAdminTools.Models
{
    public class SessionDataModel
    {
        [JsonProperty("user")]
        public UserModel User { get; set; } = default!;

        [JsonProperty("community")]
        public CommunityModel Community { get; set; } = default!;
    }
}
