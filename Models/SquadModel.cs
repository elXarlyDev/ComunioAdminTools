using Newtonsoft.Json;

namespace ComunioAdminTools.Models
{
    public class SquadModel
    {
        [JsonProperty("items")]
        public IEnumerable<PlayerModel> Players { get; set; } = default!;
    }
}
