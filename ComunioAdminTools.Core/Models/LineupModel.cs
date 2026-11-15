using Newtonsoft.Json;

namespace ComunioAdminTools.Core.Models;

public sealed class LineupModel
{
    [JsonProperty("tactic", Required = Required.Always)]
    public LineupFormation Tactic { get; set; }

    [JsonProperty("items", Required = Required.Always)]
    public LineupItems Items { get; set; } = new();

    public sealed class LineupItems
    {
        [JsonProperty("lineup", Required = Required.Always)]
        public Dictionary<int, PlayerDetailsModel?> Lineup { get; set; } = [];
    }
}
