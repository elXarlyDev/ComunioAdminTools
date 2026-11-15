using Newtonsoft.Json;

namespace ComunioAdminTools.Core.Models;

public sealed class CommunityNewsModel
{
    [JsonProperty("newsList", Required = Required.Always)]
    public NewsListModel NewsList { get; set; } = new();

    public sealed class NewsListModel
    {
        [JsonProperty("entries", Required = Required.Always)]
        public IReadOnlyList<NewsEntry> Entries { get; set; } = [];

        [JsonProperty("hasMore", Required = Required.Always)]
        public bool HasMore { get; set; }
    }

    public sealed class NewsEntry
    {
        [JsonProperty("id", Required = Required.Always)]
        public string Id { get; set; } = "";

        [JsonProperty("date", Required = Required.Always)]
        public DateTimeOffset Date { get; set; }

        public string Type { get; set; } = "";
        public NewsMessage? Message { get; set; }
    }

    public sealed class NewsMessage
    {
        public string? Type { get; set; }
        public string? Matchday { get; set; }
        public string? Text { get; set; }
    }
}
