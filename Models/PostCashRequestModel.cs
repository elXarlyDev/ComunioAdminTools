using Newtonsoft.Json;

namespace ComunioAdminTools.Models
{
    public class PostCashRequestModel
    {
        [JsonProperty("amount")]
        public int Amount { get; set; }

        [JsonProperty("reason")]
        public string Reason { get; set; } = default!;
    }
}
