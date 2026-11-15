using System.Net;
using System.Text;

using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

using ComunioAdminTools.Core;
using ComunioAdminTools.Core.Authentication;
using ComunioAdminTools.Core.Localization;
using ComunioAdminTools.Core.Models;
using ComunioAdminTools.Core.Services;

static void Check(bool condition, string message)
{
    if (!condition)
    {
        throw new Exception(message);
    }
}

var api = new CashApi();
var services = new ServiceCollection();
services.AddComunioCore();
services.AddHttpClient("ComunioAuth").ConfigurePrimaryHttpMessageHandler(() => api);
services.AddHttpClient("Comunio").ConfigurePrimaryHttpMessageHandler(() => api);
using var provider = services.BuildServiceProvider();
var session = provider.GetRequiredService<ISessionManager>();
await session.Load(new("test", "refresh", DateTimeOffset.UtcNow.AddHours(1)));
var cash = provider.GetRequiredService<ICashService>();
var plan = await cash.Prepare(CashOperation.RoundPoints);

Check(api.PaymentAttempts == 0 && plan.Payments.Select(payment => payment.Amount).SequenceEqual(new[] { 25_000_000, 5_000_000, 0, 0 }),
    "Preparing calculates all amounts without sending any movement.");

var reads = api.StandingsRequests;
api.Points = "999";
var results = await cash.Apply(plan);

Check(api.StandingsRequests == reads && api.Payments.Select(payment => payment.Amount).SequenceEqual(new[] { 10_000_000, 10_000_000, 5_000_000, 5_000_000 })
    && api.Payments.All(payment => payment.Reason == "PUNTS") && results.Count == 4 && results[0].Amount == 25_000_000,
    "Applying uses the preview snapshot, original reasons, 100-point blocks and sorted results, including zeros.");

var attempts = api.PaymentAttempts;

try
{
    await cash.Apply(plan);
    throw new Exception("Expected a single-use plan.");
}
catch (LocalizedInvalidOperationException ex)
{
    Check(ex.ResourceKey == TextKey.CashPlanExpired && api.PaymentAttempts == attempts, "A plan cannot pay twice.");
}

api.Points = "250";
api.ResetPayments();
plan = await cash.Prepare(CashOperation.RevertRoundPoints);
Check(plan.Payments[0].Amount == -25_000_000 && api.PaymentAttempts == 0, "Reversal previews negative amounts without payments.");
await cash.Apply(plan);

Check(api.Payments.Select(payment => payment.Amount).SequenceEqual(new[] { -10_000_000, -10_000_000, -5_000_000, -5_000_000 })
    && api.Payments.All(payment => payment.Reason == "REVERTIR PUNTS"), "Reversal preserves its amounts and current reason.");

api.ResetPayments();
plan = await cash.Prepare(CashOperation.InternationalBreak);
Check(plan.Payments.Count == 4 && plan.Payments.All(payment => payment.Amount == 5_000_000), "Break payment previews 5M per user.");
await cash.Apply(plan);

Check(api.Payments.Count == 4 && api.Payments.All(payment => payment.Amount == 5_000_000 && payment.Reason == "PARÓ SELECCIONS"),
    "Break payment executes the preview for all users.");

api.ResetPayments();
plan = await cash.Prepare(CashOperation.RoundPoints);
api.FailAtPayment = 3;

try
{
    await cash.Apply(plan);
    throw new Exception("Expected a partial failure.");
}
catch (CashOperationException ex)
{
    Check(ex.ConfirmedPayments.Single().Amount == 20_000_000 && api.PaymentAttempts == 3,
        "Partial failure preserves only confirmed blocks and does not retry.");
}

try
{
    await cash.Apply(plan);
    throw new Exception("Expected the failed plan to remain consumed.");
}
catch (LocalizedInvalidOperationException)
{
    Check(api.PaymentAttempts == 3, "A partially applied plan cannot be replayed.");
}

api.ResetPayments();
plan = await cash.Prepare(CashOperation.RoundPoints);
session.Session!.Community.Id = "another-community";

try
{
    await cash.Apply(plan);
    throw new Exception("Expected a changed-session failure.");
}
catch (LocalizedInvalidOperationException ex)
{
    Check(ex.ResourceKey == TextKey.CashPlanExpired && api.PaymentAttempts == 0, "A changed session cannot execute an old calculation.");
}

session.Session.Community.Id = "test";
api.ResetPayments();

static object News(string id, string date, string type, object message) => new { id, date, type, message };

static object Bonus(string id, string date, string amount, string user, string admin, string reason)
    => News(id, date, "TRANSACTION_BONUS", new { text = $"Abono: {amount} serán abonados a {user} por el administrador de la comunidad ({admin}). Razón: {reason}" });

void FillNewsTo(int count, string date)
{
    while (api.NewsEntries.Count < count)
        api.NewsEntries.Add(News("filler-" + api.NewsEntries.Count, date, "LINEUP_CHANGED", new { }));
}

var firstBonus = Bonus("bonus-a-1", "2026-11-11T12:00:00+01:00", "10.000.000", "a", "b", "PUNTS");
api.NewsEntries.Add(Bonus("after-next-round", "2026-11-16T10:00:00Z", "500.000.000", "a", "b", "Ignored"));
api.NewsEntries.Add(Bonus("at-next-round", "2026-11-15T11:00:00+01:00", "500.000.000", "a", "b", "Ignored"));
api.NewsEntries.Add(firstBonus);
api.NewsEntries.Add(Bonus("bonus-a-2", "2026-11-11T11:00:00+01:00", "10.000.000", "a", "b", "Other reason"));
api.NewsEntries.Add(Bonus("bonus-b-1", "2026-11-11T10:00:00+01:00", "4.000.000", "b", "a", "PARÓ SELECCIONS"));
api.NewsEntries.Add(Bonus("bonus-c", "2026-11-11T09:00:00+01:00", "200.000", "c", "b", ""));
FillNewsTo(20, "2026-11-10T12:00:00+01:00");
api.NewsEntries.Add(firstBonus);
api.NewsEntries.Add(Bonus("bonus-b-2", "2026-11-10T11:00:00+01:00", "1.000.000", "b", "a", "Anything"));
api.NewsEntries.Add(News("prediction", "2026-11-10T10:00:00+01:00", "SYSTEM_ADMINISTRATION", new { type = "PREDICTION_CALCULATION", matchday = 12 }));
api.NewsEntries.Add(News("other-round", "2026-11-10T09:00:00+01:00", "SYSTEM_ADMINISTRATION", new { type = "POINTS_CALCULATION", matchday = 11 }));
FillNewsTo(40, "2026-11-10T08:00:00+01:00");
var calculation = News("calculation", "2026-11-09T10:00:00+01:00", "SYSTEM_ADMINISTRATION", new { type = "POINTS_CALCULATION", matchday = 12 });
api.NewsEntries.Add(calculation);
api.NewsEntries.Add(Bonus("old", "2026-11-08T10:00:00+01:00", "999.000.000", "a", "b", "PUNTS"));
FillNewsTo(61, "2026-11-08T09:00:00+01:00");
plan = await cash.Prepare(CashOperation.Correction);

Check(api.NewsStarts.SequenceEqual(new[] { 0, 20, 40 }) && api.PaymentAttempts == 0,
    "Correction paginates by 20 from zero until the correct points-calculation date, without making payments.");

Check(plan.Payments.Count == 2 && plan.Payments.Single(payment => payment.UserId == "a").Amount == 5_000_000
    && plan.Payments.Single(payment => payment.UserId == "c").Amount == -200_000,
    "Correction sums split payments once, ignores admin names and reasons, excludes payments before calculation or at/after the next start and keeps only differences.");

api.Points = "999";
api.NewsEntries.Clear();
var newsReadCount = api.NewsStarts.Count;
await cash.Apply(plan);

Check(api.Payments.Select(payment => payment.Amount).SequenceEqual(new[] { 5_000_000, -200_000 }) && api.NewsStarts.Count == newsReadCount,
    "Confirmation applies positive and negative correction amounts without reading news again.");

api.Points = "250";
api.ResetPayments();
api.NewsStarts.Clear();
api.NewsEntries.Add(Bonus("exact-a", "2026-11-10T10:00:00+01:00", "25.000.000", "a", "b", "Ignored"));
api.NewsEntries.Add(Bonus("exact-b", "2026-11-10T09:00:00+01:00", "5.000.000", "b", "a", "Ignored"));
api.NewsEntries.Add(calculation);
Check((await cash.Prepare(CashOperation.Correction)).Payments.Count == 0, "Matching payments produce no corrections.");

api.UserNames["a"] = "José\u00a0  Pérez";
api.NewsEntries.Clear();
api.NewsEntries.Add(Bonus("normalized-user", "2026-11-10T10:00:00+01:00", "1.000.000", "Jose\u0301 &nbsp; Pe\u0301rez", "b", "Ignored"));
api.NewsEntries.Add(calculation);
plan = await cash.Prepare(CashOperation.Correction);

Check(plan.Payments.Single(payment => payment.UserId == "a").Amount == 24_000_000,
    "News and standings match names with HTML spaces, repeated whitespace and equivalent Unicode accents.");

api.UserNames["b"] = "Jose\u0301 Pe\u0301rez";

try
{
    await cash.Prepare(CashOperation.Correction);
    throw new Exception("Expected normalized duplicate names to remain ambiguous.");
}
catch (LocalizedInvalidOperationException ex)
{
    Check(ex.ResourceKey == TextKey.AmbiguousBonusRecipient && ex.Arguments[0]?.ToString() == "José Pérez"
        && ex.Arguments[1]?.ToString() == "normalized-user", "Ambiguous names identify the exact recipient and news without choosing a user arbitrarily.");
}

api.UserNames.Clear();

foreach (var (message, expectedDifference) in new[]
    {
        ("Pena disciplinaria: 100.000 € han sido restados a a por el administrador de la comunidad (b). Razón: Punts últims", 25_100_000),
        ("Pena disciplinaria: -100.000 € han sido restados a a por el administrador de la comunidad (b). Razón: Ignored", 25_100_000),
        ("Abono: 100.000 € han sido abonados a a por el administrador de la comunidad (b). Razón: Ignored", 24_900_000),
        ("Abono: -100.000 serán abonados a a por el administrador de la comunidad (b). Razón: Ignored", 25_100_000),
        ("Movimiento: 100.000 € han sido restados a a por el administrador de la comunidad (b). Razón: Ignored", 25_100_000),
        ("100.000 € han sido abonados a a por el administrador de la comunidad (b). Razón: Ignored", 24_900_000)
    })
{
    api.NewsEntries.Clear();
    api.NewsEntries.Add(News("payment-format", "2026-11-10T10:00:00+01:00", "TRANSACTION_BONUS", new { text = message }));
    api.NewsEntries.Add(calculation);
    plan = await cash.Prepare(CashOperation.Correction);

    Check(plan.Payments.Single(payment => payment.UserId == "a").Amount == expectedDifference,
        "Disciplinary deductions count negatively, including euro symbols and already-signed amounts, while bonuses count positively.");
}

api.NewsEntries.Clear();
api.NewsEntries.Add(Bonus("paid-a", "2026-11-10T12:00:00+01:00", "25.100.000", "a", "b", "Ignored"));
api.NewsEntries.Add(Bonus("paid-b", "2026-11-10T11:00:00+01:00", "5.000.000", "b", "a", "Ignored"));

api.NewsEntries.Add(News("deducted-a", "2026-11-10T10:00:00+01:00", "TRANSACTION_BONUS", new
{
    text = "Pena disciplinaria: 100.000 € han sido restados a a por el administrador de la comunidad (b). Razón: Punts últims"
}));

api.NewsEntries.Add(calculation);

Check((await cash.Prepare(CashOperation.Correction)).Payments.Count == 0,
    "An existing deduction offsets a prior overpayment and prevents repeating the same correction.");

foreach (var invalid in new[]
    {
        Bonus("unknown", "2026-11-10T10:00:00+01:00", "100.000", "unknown-user", "a", "Ignored"),
        News("unreadable", "2026-11-10T10:00:00+01:00", "TRANSACTION_BONUS", new { text = "Unknown bonus format" })
    })
{
    api.NewsEntries.Clear();
    api.NewsEntries.Add(invalid);
    api.NewsEntries.Add(calculation);

    try
    {
        await cash.Prepare(CashOperation.Correction);
        throw new Exception("Expected an unreadable payment to block correction.");
    }
    catch (LocalizedInvalidOperationException ex)
    {
        Check(ex.ResourceKey is TextKey.UnreadableBonusNews or TextKey.BonusRecipientNotFound && api.PaymentAttempts == 0
            && ex.Arguments.Length == 2,
            "Unrecognized recipients or unreadable amounts never silently become missing payments.");
    }
}

api.NewsEntries.Clear();

try
{
    await cash.Prepare(CashOperation.Correction);
    throw new Exception("Expected a missing calculation error.");
}
catch (LocalizedInvalidOperationException ex)
{
    Check(ex.ResourceKey == TextKey.PointsCalculationNewsNotFound, "Missing calculation news does not assume all payments are absent.");
}

api.NewsStarts.Clear();
FillNewsTo(19, "2026-11-10T10:00:00+01:00");
api.NewsEntries.Add(calculation);
api.NewsEntries.Add(Bonus("same-instant", "2026-11-09T09:00:00Z", "1.000.000", "a", "b", "Ignored"));
FillNewsTo(40, "2026-11-08T09:00:00+01:00");
plan = await cash.Prepare(CashOperation.Correction);

Check(api.NewsStarts.SequenceEqual(new[] { 0, 20 }) && plan.Payments.Single(payment => payment.UserId == "a").Amount == 24_000_000,
    "A calculation on a page boundary includes bonuses at the same instant on the next page, regardless of timezone offset.");

api.NewsEntries.Clear();
api.NewsStarts.Clear();
FillNewsTo(40, "2026-11-10T10:00:00+01:00");
api.RepeatNewsPage = true;

try
{
    await cash.Prepare(CashOperation.Correction);
    throw new Exception("Expected repeated pagination to stop.");
}
catch (LocalizedInvalidOperationException ex)
{
    Check(ex.ResourceKey == TextKey.InvalidJsonResponse && api.NewsStarts.SequenceEqual(new[] { 0, 20 }),
        "Repeated pages cannot loop indefinitely or double-count bonuses.");
}

api.RepeatNewsPage = false;
var originalMatchdays = api.Matchdays;

foreach (var (matchdays, expectedError) in new[]
    {
        ("[]", TextKey.NoFinishedMatchday),
        ("""[{"matchdayKey":"12","timestamp":"2026-11-07T10:00:00Z","started":true,"finished":true}]""", TextKey.NoNextMatchday)
    })
{
    api.Matchdays = matchdays;
    var previousNewsReads = api.NewsStarts.Count;

    try
    {
        await cash.Prepare(CashOperation.Correction);
        throw new Exception("Expected missing matchday boundaries to block correction.");
    }
    catch (LocalizedInvalidOperationException ex)
    {
        Check(ex.ResourceKey == expectedError && api.NewsStarts.Count == previousNewsReads && api.PaymentAttempts == 0,
            "Correction never guesses an unknown matchday or next-start boundary.");
    }
}

api.Matchdays = originalMatchdays;

var heldResponse = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
api.NewsResponse = heldResponse;

using (var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100)))
{
    try
    {
        await cash.Prepare(CashOperation.Correction, cancellation.Token).WaitAsync(TimeSpan.FromSeconds(3));
        throw new Exception("Expected a blocked news response to be cancellable.");
    }
    catch (OperationCanceledException)
    {
        Check(cancellation.IsCancellationRequested && api.PaymentAttempts == 0,
            "A blocked first news request stops waiting even if the transport ignores cancellation.");
    }
    finally
    {
        heldResponse.TrySetResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
        api.NewsResponse = null;
    }
}

var content = new HeldNewsContent();
api.NewsResponse = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
api.NewsResponse.SetResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });

using (var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100)))
{
    try
    {
        await cash.Prepare(CashOperation.Correction, cancellation.Token).WaitAsync(TimeSpan.FromSeconds(3));
        throw new Exception("Expected a stalled response body to be cancellable.");
    }
    catch (OperationCanceledException)
    {
        await content.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Check(content.ReadStarted && cancellation.IsCancellationRequested, "Cancellation reaches the response body and releases its resources.");
    }
}

content = new HeldNewsContent();
api.NewsResponse = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
api.NewsResponse.SetResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = content });

try
{
    await cash.Prepare(CashOperation.Correction).WaitAsync(TimeSpan.FromSeconds(3));
    throw new Exception("Expected the HTTP error to be reported without reading its hanging body.");
}
catch (LocalizedHttpRequestException ex)
{
    Check(ex.ResourceKey == TextKey.ServiceUnavailable && !content.ReadStarted,
        "Error status headers are processed immediately without waiting for the error body.");
}

api.NewsResponse = null;

if (args.Contains("--timeout"))
{
    heldResponse = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
    api.NewsResponse = heldResponse;

    try
    {
        await cash.Prepare(CashOperation.Correction).WaitAsync(TimeSpan.FromSeconds(35));
        throw new Exception("Expected the real news deadline to stop the waiting request.");
    }
    catch (LocalizedHttpRequestException ex)
    {
        Check(ex.ResourceKey == TextKey.NewsRequestTimeout && api.PaymentAttempts == 0,
            "The real 30-second deadline reports a translated news timeout without making payments.");
    }
    finally
    {
        heldResponse.TrySetResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
        api.NewsResponse = null;
    }
}

await session.Clear();

try
{
    await cash.Apply(plan);
    throw new Exception("Expected authentication to be required.");
}
catch (AuthenticationRequiredException)
{
    Check(api.PaymentAttempts == 0, "A cleared session cannot pay a pending plan.");
}

Console.WriteLine("Cash checks passed: corrections, paginated news, exact preview amounts, single execution, partial failures and session changes; simulated API only.");

sealed class CashApi : HttpMessageHandler
{
    public string Points { get; set; } = "250";
    public int StandingsRequests { get; private set; }
    public int PaymentAttempts { get; private set; }
    public int? FailAtPayment { get; set; }
    public List<PostCashRequestModel> Payments { get; } = [];
    public Dictionary<string, string> UserNames { get; } = [];
    public List<object> NewsEntries { get; } = [];
    public List<int> NewsStarts { get; } = [];
    public bool RepeatNewsPage { get; set; }
    public TaskCompletionSource<HttpResponseMessage>? NewsResponse { get; set; }
    public string Matchdays { get; set; } = """[{"matchdayKey":"13","timestamp":"2026-11-15T10:00:00Z","started":true,"finished":false},{"matchdayKey":"12","timestamp":"2026-11-07T10:00:00Z","started":true,"finished":true}]""";

    public void ResetPayments()
    {
        Payments.Clear();
        PaymentAttempts = 0;
        FailAtPayment = null;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Headers.Authorization is not { Scheme: "Bearer", Parameter: "test" })
        {
            throw new Exception("All requests need the session token.");
        }

        var path = request.RequestUri!.AbsolutePath;
        var body = "{}";
        var status = HttpStatusCode.OK;

        if (path == "/")
            body = """{"user":{"id":"admin","name":"Admin"},"community":{"id":"test"}}""";
        else if (path == "/communities/test/standings" && request.Method == HttpMethod.Get)
        {
            StandingsRequests++;

            body = JsonConvert.SerializeObject(new
            {
                items = new[] { ("a", Points), ("b", "50"), ("c", "0"), ("d", "-3") }
                    .Select(team => new { lastPoints = team.Item2, _embedded = new { user = new { id = team.Item1, name = UserNames.GetValueOrDefault(team.Item1, team.Item1) } } })
            });
        }
        else if (path == "/matchdays")
            body = Matchdays;
        else if (path == "/communities/test/users/admin/news")
        {
            var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query);

            if (request.Method != HttpMethod.Get || query["originaltypes"] != "true" || query["limit"] != "20")
            {
                throw new Exception("News requests require GET, original types and limit 20.");
            }

            var start = int.Parse(query["start"]!);
            NewsStarts.Add(start);

            if (!request.Headers.Accept.Any(accept => accept.MediaType == "application/json"))
            {
                throw new Exception("News requests explicitly accept JSON.");
            }

            if (NewsResponse is { } pending)
            {
                return await pending.Task;
            }

            var sourceIndex = RepeatNewsPage ? 0 : start;

            body = JsonConvert.SerializeObject(new
            {
                newsList = new { entries = NewsEntries.Skip(sourceIndex).Take(20), hasMore = sourceIndex + 20 < NewsEntries.Count }
            });
        }
        else if (path.StartsWith("/communities/test/users/", StringComparison.Ordinal) && path.EndsWith("/penalties") && request.Method == HttpMethod.Post)
        {
            PaymentAttempts++;

            if (PaymentAttempts == FailAtPayment)
                status = HttpStatusCode.ServiceUnavailable;
            else
                Payments.Add(JsonConvert.DeserializeObject<PostCashRequestModel>(await request.Content!.ReadAsStringAsync(cancellationToken))!);
        }
        else
        {
            throw new Exception("Unexpected request: " + request.Method + " " + path);
        }

        return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}

sealed class HeldNewsContent : HttpContent
{
    public bool ReadStarted { get; private set; }
    public TaskCompletionSource Disposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        => SerializeToStreamAsync(stream, context, CancellationToken.None);

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
    {
        ReadStarted = true;
        await Task.Delay(Timeout.Infinite, cancellationToken);
    }

    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return false;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Disposed.TrySetResult();
    }
}
