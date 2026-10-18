using Newtonsoft.Json;

namespace ComunioAdminTools.Core.Models
{
    public class PlayerModel
    {
        public string Id { get; set; } = default!;
        public string Name { get; set; } = default!;

        [JsonProperty("purchaseInfo")]
        public PurchaseInfoModel? PurchaseInfo { get; set; }
    }
}
