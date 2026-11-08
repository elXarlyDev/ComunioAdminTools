using System.Net;
using System.Text;

using Microsoft.Extensions.DependencyInjection;

using ComunioAdminTools.Core;
using ComunioAdminTools.Core.Authentication;
using ComunioAdminTools.Core.Localization;
using ComunioAdminTools.Core.Services;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

var api = new MarketApi();
var services = new ServiceCollection();
services.AddComunioCore();
services.AddHttpClient("ComunioAuth").ConfigurePrimaryHttpMessageHandler(() => api);
services.AddHttpClient("Comunio").ConfigurePrimaryHttpMessageHandler(() => api);
using var provider = services.BuildServiceProvider();
var session = provider.GetRequiredService<ISessionManager>();
await session.LoadAsync(new("test", "refresh", DateTimeOffset.UtcNow.AddHours(1)));
var market = provider.GetRequiredService<IMarketService>();
var communityUsers = await market.GetCommunityUserNamesAsync();
Check(communityUsers.SequenceEqual(new[] { "Current", "No transfers yet" }),
    "The community user list includes members without transfers and trims names.");
var transfers = await market.GetTransferHistoryAsync();
Check(api.HistoryRequests == 1, "History must use a single request without pagination.");
Check(transfers.Count == 3, "Every history item must appear.");
Check(transfers[0].SellerName == "Buyer" && transfers[0].BuyerName == "Seller", "SALE direction is tradingPartner -> user.");
Check(transfers[1].SellerName == "Buyer" && transfers[1].BuyerName == "Seller", "PURCHASE also uses tradingPartner -> user.");
Check(transfers[2].PlayerName == "Fornals"
    && transfers[2].SellerName == "Dixeume a la gateta en pau"
    && transfers[2].BuyerName == "Bazar Chino Lollito Plimavela",
    "Fornals was sold by Dixeume to Bazar; SALE must not reverse the parties.");
Check(transfers[0].PlayerName == "Jugador" && transfers[0].ClubName == "Club"
    && transfers[0].Price == 2100006 && transfers[0].QuotedPrice == 2140000,
    "Player, club, transfer price and quoted market price must stay distinct.");
Check(transfers[0].DateChanged == DateTimeOffset.Parse("2026-11-07T04:30:49+01:00")
    && transfers[0].DateChanged.Offset == TimeSpan.FromHours(1), "Execution date must retain its timezone.");
api.Body = """{"items":[],"hasMore":false}""";
Check((await market.GetTransferHistoryAsync()).Count == 0, "Empty history is valid.");
foreach (var body in new[] { "{}", """{"items":[{"type":"SALE"}]}""" })
{
    api.Body = body;
    try
    {
        await market.GetTransferHistoryAsync();
        throw new Exception("Invalid history must not produce misleading rows.");
    }
    catch (LocalizedInvalidOperationException ex)
    {
        Check(ex.ResourceKey == TextKey.InvalidJsonResponse, "Invalid history uses the localized JSON error.");
    }
}
api.Status = HttpStatusCode.ServiceUnavailable;
try
{
    await market.GetTransferHistoryAsync();
    throw new Exception("Expected HTTP failure.");
}
catch (LocalizedHttpRequestException ex)
{
    Check(ex.ResourceKey == TextKey.ServiceUnavailable, "HTTP errors remain localized.");
}
await session.ClearAsync();
var previousRequests = api.HistoryRequests;
try
{
    await market.GetTransferHistoryAsync();
    throw new Exception("Expected authentication failure.");
}
catch (AuthenticationRequiredException)
{
    Check(api.HistoryRequests == previousRequests, "History requires a session before sending a request.");
}
Console.WriteLine("Market checks passed: authenticated request, limit without offset, transfer direction, fields, dates, empty history and errors; simulated API only.");

sealed class MarketApi : HttpMessageHandler
{
    public const string History = """
        {"items":[
          {"type":"SALE","tradable":{"name":"Jugador","club":{"name":"Club"},"quotedPrice":2140000},
           "user":{"name":"Seller "},"tradingPartner":{"name":"Buyer"},"price":2100006,"datechanged":"2026-11-07T04:30:49+01:00"},
          {"type":"PURCHASE","tradable":{"name":"Otro","club":{"name":"Otro club"},"quotedPrice":100},
           "user":{"name":"Seller"},"tradingPartner":{"name":"Buyer "},"price":90,"datechanged":"2026-11-06T23:30:00-03:00"},
          {"type":"SALE","tradable":{"name":"Fornals","club":{"name":"Betis"},"quotedPrice":5570000,
           "owner":{"id":21150946,"name":"Dixeume a la gateta en pau "}},
           "user":{"id":21150951,"name":"Bazar Chino Lollito Plimavela"},
           "tradingPartner":{"id":21150946,"name":"Dixeume a la gateta en pau "},
           "price":10000000,"datechanged":"2026-10-02T23:58:14+02:00"}
        ],"hasMore":true}
        """;

    public string Body { get; set; } = History;
    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
    public int HistoryRequests { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Headers.Authorization is not { Scheme: "Bearer", Parameter: "test" })
            throw new Exception("Expected the session token.");
        var profile = request.RequestUri!.AbsolutePath == "/";
        if (request.RequestUri.PathAndQuery == "/communities/community/standings?period=total&wpe=true")
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"items":[{"_embedded":{"user":{"name":"Current "}}},{"_embedded":{"user":{"name":"No transfers yet"}}}]}""",
                    Encoding.UTF8, "application/json")
            });
        }
        if (!profile)
        {
            if (request.Method != HttpMethod.Get
                || request.RequestUri.PathAndQuery != "/communities/community/users/current/offers/history?limit=999999")
                throw new Exception($"Unexpected history request: {request.RequestUri}");
            HistoryRequests++;
        }
        return Task.FromResult(new HttpResponseMessage(profile ? HttpStatusCode.OK : Status)
        {
            Content = new StringContent(profile
                ? """{"user":{"id":"current","name":"Current"},"community":{"id":"community"}}"""
                : Body, Encoding.UTF8, "application/json")
        });
    }
}
