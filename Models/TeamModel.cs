using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace ComunioAdminTools.Models
{
    public class TeamModel
    {
        [JsonProperty("totalPoints")]
        public int TotalPoints { get; set; }

        [JsonProperty("lastPoints")]
        public string LastPoints { get; set; }

        [JsonProperty("livePoints")]
        public int? LivePoints { get; set; }

        [JsonProperty("_embedded")]
        public TeamInfoModel TeamInfo { get; set; }
    }
}
