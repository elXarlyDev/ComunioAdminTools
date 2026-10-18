using Newtonsoft.Json;

namespace ComunioAdminTools.Core.Models;

public sealed class PurchaseInfoModel
{
    [JsonProperty("date")]
    public DateTimeOffset? Date { get; set; }

    [JsonProperty("price")]
    public decimal? Price { get; set; }

    [JsonProperty("marketvalue")]
    public decimal? MarketValue { get; set; }

    [JsonProperty("lastTransferCalculation")]
    public DateTimeOffset? LastTransferCalculation { get; set; }
}
