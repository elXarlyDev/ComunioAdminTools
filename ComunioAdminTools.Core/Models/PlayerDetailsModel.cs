using Newtonsoft.Json;

namespace ComunioAdminTools.Core.Models;

public sealed class PlayerDetailsModel
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public ClubModel? Club { get; set; }

    [JsonProperty("type")]
    public PlayerPosition? Position { get; set; }

    public PlayerStatus? Status { get; set; }
    public string? StatusInfo { get; set; }

    [JsonProperty("position")]
    public int? LineupPosition { get; set; }

    public sealed class ClubModel
    {
        public string Name { get; set; } = "";
    }
}
