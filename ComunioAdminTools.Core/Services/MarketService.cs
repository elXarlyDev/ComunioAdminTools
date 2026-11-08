using Newtonsoft.Json;

using ComunioAdminTools.Core.Authentication;
using ComunioAdminTools.Core.Localization;
using ComunioAdminTools.Core.Models;

namespace ComunioAdminTools.Core.Services;

public interface IMarketService
{
    Task<IReadOnlyList<TransferModel>> GetTransferHistoryAsync();
    Task<IReadOnlyList<string>> GetCommunityUserNamesAsync();
}

public sealed class MarketService(IHttpClientFactory factory, ISessionManager sessionManager, AppLocalization text)
    : CommunityService(factory, sessionManager, text), IMarketService
{
    public async Task<IReadOnlyList<TransferModel>> GetTransferHistoryAsync()
    {
        var session = Session;
        var history = await GetAsync<HistoryResponse>(
            $"communities/{Uri.EscapeDataString(session.Community.Id)}/users/{Uri.EscapeDataString(session.User.Id)}/offers/history?limit=999999");

        // History uses tradingPartner as seller and user as buyer for both SALE and PURCHASE.
        return history.Items.Select(offer => new TransferModel(offer.Tradable.Name, offer.Tradable.Club.Name,
            offer.TradingPartner.Name.Trim(), offer.User.Name.Trim(), offer.Price,
            offer.Tradable.QuotedPrice, offer.DateChanged)).ToArray();
    }

    public async Task<IReadOnlyList<string>> GetCommunityUserNamesAsync()
    {
        var standings = await GetStandingsAsync();
        return standings.Teams.Select(team => team.TeamInfo.User.Name.Trim()).ToArray();
    }

    private sealed class HistoryResponse
    {
        [JsonProperty("items", Required = Required.Always)]
        public List<HistoryOffer> Items { get; set; } = [];
    }

    private sealed class HistoryOffer
    {
        [JsonProperty("tradable", Required = Required.Always)]
        public HistoryPlayer Tradable { get; set; } = default!;

        [JsonProperty("user", Required = Required.Always)]
        public HistoryParty User { get; set; } = default!;

        [JsonProperty("tradingPartner", Required = Required.Always)]
        public HistoryParty TradingPartner { get; set; } = default!;

        [JsonProperty("price", Required = Required.Always)]
        public decimal Price { get; set; }

        [JsonProperty("datechanged", Required = Required.Always)]
        public DateTimeOffset DateChanged { get; set; }
    }

    private sealed class HistoryPlayer
    {
        [JsonProperty("name", Required = Required.Always)]
        public string Name { get; set; } = "";

        [JsonProperty("club", Required = Required.Always)]
        public HistoryParty Club { get; set; } = default!;

        [JsonProperty("quotedPrice", Required = Required.Always)]
        public decimal QuotedPrice { get; set; }
    }

    private sealed class HistoryParty
    {
        [JsonProperty("name", Required = Required.Always)]
        public string Name { get; set; } = "";
    }
}
