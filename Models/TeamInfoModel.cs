using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace ComunioAdminTools.Models
{
    public class TeamInfoModel
    {
        [JsonProperty("user")]
        public UserModel User { get; set; }
    }
}
