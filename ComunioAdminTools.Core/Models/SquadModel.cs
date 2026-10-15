using Newtonsoft.Json;

namespace ComunioAdminTools.Core.Models
{
    public class SquadModel
    {
        [JsonProperty("items")]
        public IEnumerable<PlayerModel> Players { get; set; } = default!;
    }
}
