using Newtonsoft.Json;

namespace ComunioAdminTools.Models
{
    public class StandingsModel
    {
        [JsonProperty("items")]
        public IEnumerable<TeamModel> Teams { get; set; } = default!;
    }
}
