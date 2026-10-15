using Newtonsoft.Json;

namespace ComunioAdminTools.Core.Models
{
    public class StandingsModel
    {
        [JsonProperty("items")]
        public IEnumerable<TeamModel> Teams { get; set; } = default!;
    }
}
