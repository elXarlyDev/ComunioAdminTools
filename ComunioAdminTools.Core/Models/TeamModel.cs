using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace ComunioAdminTools.Core.Models
{
    public class TeamModel
    {
        [JsonProperty("totalPoints")]
        public int TotalPoints { get; set; }

        [JsonProperty("lastPoints")]
        public string LastPoints { get; set; } = string.Empty;

        [JsonProperty("livePoints")]
        public int? LivePoints { get; set; }

        [JsonProperty("_embedded")]
        public TeamInfoModel TeamInfo { get; set; } = default!;
    }
}
