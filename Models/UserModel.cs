using Newtonsoft.Json;

namespace ComunioAdminTools.Models
{
    public class UserModel
    {
        [JsonProperty("id")]
        public string Id { get; set; } = default!;

        [JsonProperty("name")]
        public string Name { get; set; } = default!;

        [JsonProperty("firstName")]
        public string FirstName { get; set; } = default!;

        [JsonProperty("isLeader")]
        public bool IsLeader { get; set; } = default!;

        [JsonProperty("isAdmin")]
        public bool IsAdmin { get; set; } = default!;
    }
}
