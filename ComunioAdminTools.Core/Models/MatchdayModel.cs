using Newtonsoft.Json;

namespace ComunioAdminTools.Core.Models;

public sealed class MatchdayModel
{
    [JsonProperty("matchdayKey")]
    public string? MatchdayKey { get; set; }

    [JsonProperty("timestamp", Required = Required.Always)]
    public DateTimeOffset Timestamp { get; set; }

    [JsonProperty("started", Required = Required.Always)]
    public bool Started { get; set; }

    [JsonProperty("finished", Required = Required.Always)]
    public bool Finished { get; set; }
}
