using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace ComunioAdminTools.Core.Models
{
    public class TeamInfoModel
    {
        [JsonProperty("user")]
        public UserModel User { get; set; } = default!;
    }
}
