using System.Globalization;
using System.Net;
using System.Text;
using System.Resources;
using ComunioAdminTools.Core;
using ComunioAdminTools.Core.Authentication;
using ComunioAdminTools.Core.Localization;
using ComunioAdminTools.Core.Models;
using ComunioAdminTools.Core.Services;
using ComunioAdminTools.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

static void Check(bool value, string message)
{
    if (!value)
    {
        throw new Exception(message);
    }
}

static async Task<Exception> Capture(Func<Task> action)
{
    try { await action(); }
    catch (Exception ex) { return ex; }

    throw new Exception("Expected an error.");
}

var originalCulture = CultureInfo.CurrentUICulture;
var serviceCollection = new ServiceCollection();
serviceCollection.AddLogging();
serviceCollection.AddSingleton<IJSRuntime, TestJsRuntime>();
serviceCollection.AddComunioCore();
serviceCollection.AddSingleton<NavigationManager, TestNavigationManager>();
var api = new TestApi();
serviceCollection.AddHttpClient("ComunioAuth").ConfigurePrimaryHttpMessageHandler(() => api);
serviceCollection.AddHttpClient("Comunio").ConfigurePrimaryHttpMessageHandler(() => api);
using var services = serviceCollection.BuildServiceProvider();
var text = services.GetRequiredService<AppLocalization>();
var auth = services.GetRequiredService<AuthService>();

foreach (var language in new[] { AppLocalization.SpanishCulture, AppLocalization.ValencianCulture })
{
    text.SetCulture(language);

    var resources = new ResourceManager($"ComunioAdminTools.Core.Localization.Strings.{language}", typeof(AppLocalization).Assembly)
        .GetResourceSet(CultureInfo.InvariantCulture, true, false)!;

    foreach (var key in Enum.GetValues<TextKey>())
        Check(!string.IsNullOrWhiteSpace(resources.GetString(key.ToString()))
            && text[key] == resources.GetString(key.ToString()), $"Missing resource {language}/{key}");

    api.LoginStatus = HttpStatusCode.Unauthorized;

    Check((await Capture(() => auth.Login("", ""))).Message == text[TextKey.SignInError],
        "Empty credentials are sent to the API and use the generic login error when rejected.");

    api.LoginStatus = HttpStatusCode.OK;
    await auth.Logout();
    Check((await Capture(() => services.GetRequiredService<ISessionManager>().GetValidAccessToken())).Message == text[TextKey.SignInRequired], "Localized session error.");

    foreach (var status in new[] { HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized,
        HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError, HttpStatusCode.Created,
        HttpStatusCode.NoContent })
    {
        api.LoginStatus = status;

        Check((await Capture(() => auth.Login("user", "password"))).Message == text[TextKey.SignInError],
            "Every non-200 authentication response uses the same error.");

        Check(services.GetRequiredService<ISessionManager>().Session is null, "Rejected authentication leaves no session.");
    }

    api.LoginStatus = HttpStatusCode.OK;
    await auth.Login("user", "password");

    foreach (var (status, key) in new[] {
        (HttpStatusCode.Forbidden, TextKey.PermissionDenied),
        (HttpStatusCode.NotFound, TextKey.ApiNotFound),
        (HttpStatusCode.TooManyRequests, TextKey.TooManyRequests),
        (HttpStatusCode.InternalServerError, TextKey.ServiceUnavailable),
        (HttpStatusCode.BadRequest, TextKey.HttpError) })
    {
        api.Status = status;
        var exception = await Capture(() => services.GetRequiredService<PointsService>().GetLastRoundPoints());
        Check(exception is HttpRequestException http && http.StatusCode == status, "Preserve HTTP status.");
        Check(exception.Message == text.Format(key, (int)status) && !exception.Message.Contains("EXTERNAL ERROR"), "Translated HTTP errors without external text.");
    }

    api.Status = HttpStatusCode.OK;
    api.Body = "broken JSON";
    Check((await Capture(() => services.GetRequiredService<PointsService>().GetLastRoundPoints())).Message == text[TextKey.InvalidJsonResponse], "Localized malformed JSON.");
    api.Body = "null";
    Check((await Capture(() => services.GetRequiredService<PointsService>().GetLastRoundPoints())).Message == text[TextKey.EmptyJsonResponse], "Localized empty JSON.");
    api.Body = null;
    api.TransportError = new HttpRequestException("UNTRANSLATED TRANSPORT ERROR");
    Check((await Capture(() => services.GetRequiredService<PointsService>().GetLastRoundPoints())).Message == text[TextKey.NetworkError], "Localized transport error.");
    api.TransportError = new TaskCanceledException("UNTRANSLATED TIMEOUT");
    Check((await Capture(() => services.GetRequiredService<PointsService>().GetLastRoundPoints())).Message == text[TextKey.TimeoutError], "Localized timeout.");
    api.TransportError = null;
    var partial = new CashOperationException(text, "A < B", [], new Exception("external"));
    Check(partial.Message == text.Format(TextKey.CashOperationError, "A < B"), "Localized formatted partial payment.");
}

await auth.Logout();
text.SetCulture(AppLocalization.SpanishCulture);
var retainedError = new AuthenticationRequiredException(text, TextKey.SessionExpired);

await using (var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>()))
{
    var root = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<TestHost>());
    var spanishHtml = await renderer.Dispatcher.InvokeAsync(() => WebUtility.HtmlDecode(root.ToHtmlString()));
    Check(spanishHtml.Contains("Comunio</h1>") && spanishHtml.Contains("Admin Tools</p>") && !spanishHtml.Contains("class=\"actions\""), "Fixed brand and operations hidden before login.");
    Check(spanishHtml.Contains("Usuario") && spanishHtml.Contains("No hay resultados"), "Spanish UI and results.");
    await renderer.Dispatcher.InvokeAsync(() => text.SetCulture(AppLocalization.ValencianCulture));
    var valencianHtml = await renderer.Dispatcher.InvokeAsync(() => WebUtility.HtmlDecode(root.ToHtmlString()));
    Check(valencianHtml.Contains("Comunio</h1>") && valencianHtml.Contains("Admin Tools</p>"), "App name is unchanged in Valencian.");
    Check(valencianHtml.Contains("Usuari") && valencianHtml.Contains("No hi ha resultats"), "Runtime switch updates an open results dialog.");
    Check(!valencianHtml.Contains("Herramientas de administración") && retainedError.Message == text[TextKey.SessionExpired], "No stale translated strings.");
}

foreach (var language in new[] { AppLocalization.SpanishCulture, AppLocalization.ValencianCulture })
{
    text.SetCulture(language);

    foreach (var (isAdmin, isLeader) in new[] { (false, false), (true, false), (false, true), (true, true) })
    {
        api.IsAdmin = isAdmin;
        api.IsLeader = isLeader;
        await auth.Login("user", "password");
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var root = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<MainPage>());
        var html = await renderer.Dispatcher.InvokeAsync(() => WebUtility.HtmlDecode(root.ToHtmlString()));
        var canManageMoney = isAdmin || isLeader;

        Check(html.Contains($">{text[TextKey.Money]}</button>") == canManageMoney,
            "Money tab is only rendered for administrators or leaders.");

        foreach (var key in new[] { TextKey.PayPoints, TextKey.PayBreak, TextKey.Correction })
            Check(html.Contains(text[key]) == canManageMoney, "Money actions follow the session permissions.");

        Check(!html.Contains(text[TextKey.RevertPoints]), "Revert is hidden before paying a round.");

        Check(html.Contains($">{text[TextKey.Queries]}</button>")
            && html.Contains(text[TextKey.QueryPoints]),
            "Queries remain available for every signed-in user.");

        Check(html.Contains($">{text[TextKey.Squad]}</button>")
            && html.Contains(text[TextKey.QuerySquads]) && html.Contains(text[TextKey.SquadStatus]),
            "Squad tab and both actions are available for every signed-in user in both languages.");

        var queryPanel = html.IndexOf("class=\"action-panel query-panel\"", StringComparison.Ordinal);
        var squadPanel = html.IndexOf("class=\"action-panel squad-panel\"", StringComparison.Ordinal);
        var marketPanel = html.IndexOf("class=\"action-panel market-panel\"", StringComparison.Ordinal);
        var excessButton = html.IndexOf(text[TextKey.QuerySquads], StringComparison.Ordinal);

        Check(queryPanel >= 0 && squadPanel > queryPanel && excessButton > squadPanel && excessButton < marketPanel,
            "Squad excess belongs to the Squad panel rather than Queries.");

        Check(System.Text.RegularExpressions.Regex.IsMatch(html,
            "<button[^>]*class=\"action-button comunio\"[^>]*>\\s*" + text[TextKey.SquadStatus] + "\\s*</button>"),
            "Squad status uses the Comunio color class and has only text, without an icon.");
    }
}

await auth.Logout();

api.IsAdmin = true;
api.IsLeader = false;
api.Body = """{"items":[{"lastPoints":"2","_embedded":{"user":{"id":"a","name":"Player A"}}},{"lastPoints":"3","_embedded":{"user":{"id":"b","name":"Player B"}}}]}""";
await auth.Login("user", "password");

await using (var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>()))
{
    MainPage page = null!;

    var root = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<MainPageHost>(
        ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(MainPageHost.Capture)] = (Action<MainPage>)(value => page = value) })));

    async Task<string> Click(string methodName, params object?[] arguments)
    {
        await renderer.Dispatcher.InvokeAsync(() => ((IHandleEvent)page).HandleEventAsync(
            new EventCallbackWorkItem((Func<Task>)(() => (Task?)typeof(MainPage)
                .GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)!
                .Invoke(page, arguments) ?? Task.CompletedTask)), null));

        return await renderer.Dispatcher.InvokeAsync(() => WebUtility.HtmlDecode(root.ToHtmlString()));
    }

    async Task<string> ConfirmPayment(string methodName)
    {
        await Click(methodName);
        return await Click("ConfirmCashPlan");
    }

    var initialHtml = await renderer.Dispatcher.InvokeAsync(() => root.ToHtmlString());

    Check(!System.Text.RegularExpressions.Regex.IsMatch(initialHtml, "<header[^>]*\\binert(?:[=\\s>])")
        && !System.Text.RegularExpressions.Regex.IsMatch(initialHtml, "<button[^>]*class=\"action-button sign-out\"[^>]*\\bdisabled(?:[=\\s>])"),
        "The idle header is interactive and sign-out is enabled.");

    foreach (var (method, expectedAmount) in new[]
        {
            ("PayRoundPoints", 200_000), ("RevertRoundPoints", -200_000), ("PayInternationalBreak", 5_000_000),
            ("CorrectRoundPoints", 100_000)
        })
    {
        var previousPayments = api.PaymentAttempts;
        var preview = await Click(method);

        Check(preview.Contains("class=\"cash-confirmation\"") && preview.Contains("Player A")
            && preview.Contains(expectedAmount.ToString("C0", text.Culture))
            && preview.Contains(text[TextKey.Accept]) && preview.Contains(text[TextKey.Cancel])
            && api.PaymentAttempts == previousPayments, "All four movements preview their amounts without making payments.");

        var cancelled = await Click("CancelCashPlan");

        Check(!cancelled.Contains("class=\"cash-confirmation\"") && api.PaymentAttempts == previousPayments,
            "Cancelling a payment confirmation never changes money.");
    }

    var correctionPreview = await Click("CorrectRoundPoints");
    var correctionNewsReads = api.NewsRequests;
    var correctionStandingsReads = api.StandingsRequests;
    var correctionPaymentStart = api.Payments.Count;

    Check(correctionPreview.Contains((-100_000).ToString("C0", text.Culture)), "Correction previews refunds as negative differences too.");
    var correctionResult = await Click("ConfirmCashPlan");

    Check(api.Payments.Skip(correctionPaymentStart).Select(payment => payment.Amount).SequenceEqual(new[] { 100_000, -100_000 })
        && api.NewsRequests == correctionNewsReads && api.StandingsRequests == correctionStandingsReads
        && correctionResult.Contains(text[TextKey.CorrectionCompleted]) && !correctionResult.Contains("class=\"cash-confirmation\""),
        "Accepting correction applies the previewed differences and shows inline results without recalculation.");

    var originalStandings = api.Body;
    await Click("PayRoundPoints");
    var standingsRequests = api.StandingsRequests;
    api.Body = """{"items":[{"lastPoints":"100","_embedded":{"user":{"id":"a","name":"Player A"}}}]}""";
    var paymentStart = api.Payments.Count;
    var html = await Click("ConfirmCashPlan");

    Check(api.Payments.Skip(paymentStart).Select(payment => payment.Amount).SequenceEqual(new[] { 200_000, 300_000 })
        && api.StandingsRequests == standingsRequests && !html.Contains("class=\"cash-confirmation\""),
        "Accept applies exactly the previewed amounts without recalculating, even if current standings changed.");

    var acceptedPayments = api.PaymentAttempts;
    await Click("ConfirmCashPlan");
    Check(api.PaymentAttempts == acceptedPayments, "A repeated accept does not duplicate the prepared movements.");
    api.Body = originalStandings;
    html = await ConfirmPayment("PayRoundPoints");

    Check(html.Contains(text[TextKey.PaidMoney]) && html.Contains("Player A")
        && html.Contains(text[TextKey.RevertPoints]) && !html.Contains("role=\"dialog\""),
        "Round payment shows inline results and offers revert without opening a dialog.");

    Check(html.IndexOf("Player A", StringComparison.Ordinal) < html.IndexOf(text[TextKey.RevertPoints], StringComparison.Ordinal),
        "Revert is placed below the payment results.");

    Check(html.Contains(text[TextKey.Hide]), "Money results offer a hide button.");
    html = await Click("HideMoneyResults");

    Check(!html.Contains("class=\"money-results\"") && !html.Contains(text[TextKey.RevertPoints])
        && !html.Contains(text[TextKey.Hide]) && html.Contains(text[TextKey.PayPoints]) && html.Contains(text[TextKey.PayBreak]),
        "Hiding round results also removes revert and keeps the payment actions available.");

    await ConfirmPayment("PayRoundPoints");
    html = await ConfirmPayment("RevertRoundPoints");

    Check(html.Contains(text[TextKey.RevertedMoney]) && !html.Contains(text[TextKey.RevertPoints])
        && html.Contains(text[TextKey.Hide]) && !html.Contains("role=\"dialog\""),
        "Revert updates inline results, hides its button and offers hide.");

    await ConfirmPayment("PayRoundPoints");
    html = await ConfirmPayment("PayInternationalBreak");

    Check(html.Contains(text[TextKey.BreakPaymentCompleted]) && !html.Contains(text[TextKey.RevertPoints])
        && html.Contains(text[TextKey.Hide]) && !html.Contains("role=\"dialog\""),
        "Break payment replaces inline results without offering round reversal and offers hide.");

    html = await Click("HideMoneyResults");
    Check(!html.Contains("class=\"money-results\""), "Break payment results can be hidden.");

    api.FailPaymentAt = api.PaymentAttempts + 2;
    html = await ConfirmPayment("PayRoundPoints");

    Check(html.Contains(text[TextKey.IncompleteOperation]) && html.Contains("Player A")
        && !html.Contains(text[TextKey.RevertPoints]) && !html.Contains("role=\"dialog\""),
        "Partial payments retain confirmed results inline without offering a full round reversal.");

    api.FailPaymentAt = null;

    api.TransportError = new HttpRequestException("Simulated network error");
    html = await ConfirmPayment("PayInternationalBreak");

    Check(html.Contains(text[TextKey.NetworkError]) && html.Contains(text[TextKey.Hide]) && !html.Contains("role=\"dialog\""),
        "Money errors are displayed inline.");

    html = await Click("HideMoneyResults");

    Check(!html.Contains(text[TextKey.NetworkError]) && !html.Contains("class=\"money-results\""),
        "Money errors can be hidden too.");

    api.TransportError = null;

    await ConfirmPayment("PayRoundPoints");
    html = await Click("ShowPoints");

    Check(html.Contains("class=\"query-results\"") && html.Contains(text[TextKey.LastRoundPoints])
        && html.Contains("Player A") && html.Contains(text[TextKey.Hide]) && !html.Contains("role=\"dialog\""),
        "Last points results are displayed inline with hide and no dialog.");

    Check(html.Contains("class=\"money-results\"") && html.Contains(text[TextKey.RevertPoints]),
        "Queries preserve the money tab results.");

    html = await Click("HideQueryResults");

    Check(!html.Contains("class=\"query-results\"") && html.Contains("class=\"money-results\"")
        && html.Contains(text[TextKey.RevertPoints]), "Hiding query results does not clear money results.");

    var standingsBody = api.Body;
    await Click("ShowPoints");
    api.Body = """{"items":[{"_embedded":{"user":{"id":"a","name":"Player A"}}},{"_embedded":{"user":{"id":"b","name":"Player B"}}},{"_embedded":{"user":{"id":"c","name":"Player C"}}}]}""";
    html = await Click("ShowSquadExcess");

    Check(html.Contains("class=\"squad-results\"") && html.Contains(text[TextKey.SquadExcess])
        && html.Contains(text[TextKey.Players]) && html.Contains("26") && !html.Contains("role=\"dialog\""),
        "Squad excess shows its own title and player counts inline.");

    Check(html.Contains("class=\"query-results\"") && html.Contains(text[TextKey.LastRoundPoints]),
        "Squad results preserve the independent query results.");

    Check(html.Contains(text.Format(TextKey.SquadExcessMatchday, "9")),
        "Squad result title includes the selected matchday key.");

    Check(html.Contains("Player A") && html.Contains("Player B") && html.Contains("Player C")
        && html.Contains(">26</td>") && html.Contains(">25</td>") && html.Contains(">24</td>"),
        "Squad results show every user, including users at and below the limit.");

    Check(System.Text.RegularExpressions.Regex.Matches(html, "class=\"row-over-limit\"").Count == 1
        && System.Text.RegularExpressions.Regex.Matches(html, "class=\"row-within-limit\"").Count == 2,
        "Only squads above 25 get red shading; 25 and below get green shading.");

    foreach (var language in new[] { AppLocalization.SpanishCulture, AppLocalization.ValencianCulture })
    {
        text.SetCulture(language);
        html = await Click("ShowSquadExcess");

        Check(html.Contains(text.Format(TextKey.SquadExcessMatchday, "9")),
            "The matchday title is localized in both languages.");
    }

    html = await Click("ShowSquadStatus");

    Check(html.Contains("class=\"squad-status-grid starters-list\"") && html.Contains("Gerard Moreno")
        && html.Contains("Villarreal") && html.Contains("DL") && html.Contains("aria-label=\"INJURED\"")
        && html.Contains("fa-kit-medical status-injured")
        && html.Contains("(Lesión muscular)"), "Squad status renders player, club, position, status and information as cards.");

    static string ListHtml(string html, string listClass) => System.Text.RegularExpressions.Regex.Match(html,
        "<(?:ol|ul) class=\"squad-status-grid " + listClass + "\"[\\s\\S]*?</(?:ol|ul)>").Value;

    var startersHtml = ListHtml(html, "starters-list");
    var substitutesHtml = ListHtml(html, "substitutes-list");
    var statusGrid = startersHtml + substitutesHtml;

    Check(statusGrid.Length > 0 && !statusGrid.Contains("<table") && !statusGrid.Contains("<th"),
        "Squad status uses cards without table columns.");

    foreach (var (positionClass, label) in new[]
        {
            ("keeper", "PT"), ("defender", "DF"), ("midfielder", "MC"), ("striker", "DL")
        })
        Check(System.Text.RegularExpressions.Regex.IsMatch(statusGrid,
            "class=\"position-badge position-" + positionClass + "\"[^>]*>" + label + "</span>"),
            "Every player position displays its abbreviation and color class.");

    Check(html.Contains("fa-check status-active") && html.Contains("aria-label=\"ACTIVE\"")
        && !html.Contains("ACTIVE_INFO_MUST_STAY_HIDDEN")
        && html.Contains("(Acumulación de tarjetas)") && html.Contains("(Molestias)"),
        "Active players hide statusInfo even when populated; other statuses retain it.");

    Check(System.Text.RegularExpressions.Regex.Matches(startersHtml, "data-position=\"(\\d+)\"")
        .Select(match => int.Parse(match.Groups[1].Value)).SequenceEqual(Enumerable.Range(1, 11).Reverse()),
        "Starters always contain eleven slots ordered by descending position.");

    Check(startersHtml.Contains("Gerard Moreno") && startersHtml.Contains("Portero")
        && !substitutesHtml.Contains("Gerard Moreno") && !substitutesHtml.Contains("Portero")
        && substitutesHtml.IndexOf("Defensa", StringComparison.Ordinal) < substitutesHtml.IndexOf("Centrocampista", StringComparison.Ordinal),
        "Substitutes exclude starters by id and preserve positional sorting.");

    html = await Click("MoveLineupPlayer", "1927", null, null);

    Check(!ListHtml(html, "starters-list").Contains("Gerard Moreno") && ListHtml(html, "substitutes-list").Contains("Gerard Moreno")
        && System.Text.RegularExpressions.Regex.Matches(ListHtml(html, "starters-list"), "data-position=").Count == 11,
        "Removing a starter keeps its empty slot and returns the player to substitutes.");

    html = await Click("MoveLineupPlayer", "1927", 9, null);

    Check(ListHtml(html, "starters-list").Contains("Gerard Moreno") && !ListHtml(html, "substitutes-list").Contains("Gerard Moreno"),
        "Adding a substitute fills a free slot and removes it from substitutes.");

    html = await Click("MoveLineupPlayer", "1927", 9, null);

    Check(System.Text.RegularExpressions.Regex.Matches(ListHtml(html, "starters-list"), "<strong[^>]*>Gerard Moreno</strong>").Count == 1,
        "Adding the same player again does not duplicate its card.");

    var initialLineup = api.Lineup;

    api.Lineup = System.Text.Json.JsonSerializer.Serialize(new
    {
        tactic = "352",
        items = new
        {
            lineup = Enumerable.Range(1, 11).ToDictionary(position => position.ToString(), position => new
            {
                id = "starter-" + position, name = "Starter " + position, position,
                type = position == 11 ? "keeper" : position >= 8 ? "defender" : position >= 3 ? "midfielder" : "striker",
                status = "ACTIVE"
            })
        }
    });

    html = await Click("ShowSquadStatus");

    Check(!html.Contains("lineup-action") && html.Contains("data-lineup-substitute=\"1927\""),
        "Player cards are draggable and the old add/remove buttons are absent.");

    html = await Click("MoveLineupPlayer", "1927", null, null);
    Check(!ListHtml(html, "starters-list").Contains("Gerard Moreno"), "Dropping a substitute in its own list leaves the lineup unchanged.");
    html = await Click("MoveLineupPlayer", "1927", 10, null);

    Check(ListHtml(html, "starters-list").Contains("Gerard Moreno")
        && System.Text.RegularExpressions.Regex.Matches(ListHtml(html, "starters-list"), "data-lineup-player=").Count == 11,
        "A substitute replaces an occupied slot even with eleven starters, without adding a twelfth player.");

    api.Lineup = initialLineup;

    await Click("ShowSquadStatus");
    html = await Click("MoveLineupPlayer", "striker-reserve", 10, null);

    Check(ListHtml(html, "starters-list").Contains("Delantero suplente") && ListHtml(html, "substitutes-list").Contains("Gerard Moreno"),
        "Dropping a substitute on a starter exchanges them.");

    html = await Click("MoveLineupPlayer", "striker-reserve", null, "1927");

    Check(ListHtml(html, "starters-list").Contains("Gerard Moreno") && ListHtml(html, "substitutes-list").Contains("Delantero suplente"),
        "Dropping a starter on a substitute exchanges them in the opposite direction.");

    await Click("MoveLineupPlayer", "striker-reserve", 9, null);
    html = await Click("MoveLineupPlayer", "1927", 9, null);

    Check(ListHtml(html, "starters-list").IndexOf("Gerard Moreno", StringComparison.Ordinal)
        < ListHtml(html, "starters-list").IndexOf("Delantero suplente", StringComparison.Ordinal),
        "Dragging between starter slots swaps their occupants.");

    var beforeInvalidMove = ListHtml(html, "starters-list");
    html = await Click("MoveLineupPlayer", "1927", -1, null);
    Check(ListHtml(html, "starters-list") == beforeInvalidMove, "Invalid drop destinations preserve the lineup.");

    html = await Click("MoveLineupPlayer", "defender", 10, null);
    Check(ListHtml(html, "starters-list") == beforeInvalidMove, "A defender cannot replace a striker.");
    html = await Click("MoveLineupPlayer", "1927", 0, null);
    Check(ListHtml(html, "starters-list") == beforeInvalidMove, "Two starters in different positional lines cannot be swapped.");
    html = await Click("MoveLineupPlayer", "keeper", null, "defender");
    Check(ListHtml(html, "starters-list") == beforeInvalidMove, "A reverse swap cannot replace a goalkeeper with a defender.");

    await Click("ShowSquadStatus");
    var formationField = typeof(MainPage).GetField("lineupFormation", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
    Check((LineupFormation)formationField.GetValue(page)! == LineupFormation.ThreeFiveTwo, "The dropdown starts with the API tactic.");
    await Click("MoveLineupPlayer", "midfielder", 8, null);
    await renderer.Dispatcher.InvokeAsync(() => formationField.SetValue(page, LineupFormation.ThreeFourThree));
    html = await Click("ChangeFormation");
    var changedSlot = System.Text.RegularExpressions.Regex.Match(html, "<li[^>]*data-lineup-slot=\"8\"[\\s\\S]*?</li>").Value;

    Check(changedSlot.Contains("data-lineup-position=\"Striker\"") && changedSlot.Contains("fa-plus")
        && !changedSlot.Contains("Centrocampista") && ListHtml(html, "substitutes-list").Contains("Centrocampista")
        && ListHtml(html, "starters-list").Contains("Gerard Moreno") && ListHtml(html, "starters-list").Contains("Portero"),
        "Changing 352 to 343 empties the changed midfield slot, returns its player to substitutes and preserves compatible starters.");

    html = await Click("MoveLineupPlayer", "defender", 8, null);

    Check(!System.Text.RegularExpressions.Regex.IsMatch(ListHtml(html, "starters-list"), "<strong[^>]*>Defensa</strong>"),
        "The changed slot rejects a defender.");

    html = await Click("MoveLineupPlayer", "striker-reserve", 8, null);
    Check(ListHtml(html, "starters-list").Contains("Delantero suplente"), "The changed slot accepts a striker.");

    foreach (var (formation, defenders, midfielders, strikers) in new[]
        {
            (LineupFormation.FourFourTwo, 4, 4, 2), (LineupFormation.ThreeFourThree, 3, 4, 3),
            (LineupFormation.ThreeFiveTwo, 3, 5, 2), (LineupFormation.FourThreeThree, 4, 3, 3),
            (LineupFormation.FourFiveOne, 4, 5, 1)
        })
    {
        await renderer.Dispatcher.InvokeAsync(() => formationField.SetValue(page, formation));
        html = await Click("ChangeFormation");
        var slots = ListHtml(html, "starters-list");

        Check(System.Text.RegularExpressions.Regex.Matches(slots, "data-lineup-position=\"Keeper\"").Count == 1
            && System.Text.RegularExpressions.Regex.Matches(slots, "data-lineup-position=\"Defender\"").Count == defenders
            && System.Text.RegularExpressions.Regex.Matches(slots, "data-lineup-position=\"Midfielder\"").Count == midfielders
            && System.Text.RegularExpressions.Regex.Matches(slots, "data-lineup-position=\"Striker\"").Count == strikers,
            "Every supported formation assigns the expected positional counts to eleven slots.");
    }

    Check(!html.Contains("class=\"squad-results\"") && html.Contains("class=\"query-results\""),
        "Opening lineup replaces squad excess and preserves the independent query results.");

    await Click("ShowSquadStatus");
    await Click("MoveLineupPlayer", "striker-reserve", 9, null);
    await renderer.Dispatcher.InvokeAsync(() => formationField.SetValue(page, LineupFormation.ThreeFourThree));
    await Click("ChangeFormation");
    var lineupBeforeSaving = api.Lineup;

    api.LineupAfterSave = """
        {"tactic":"343","items":{"lineup":{
            "1":{"id":"1927","name":"Server confirmed player","type":"striker","position":1,"status":"ACTIVE"},
            "11":{"id":"keeper","name":"Portero","type":"keeper","position":11,"status":"ACTIVE"}
        }}}
        """;

    api.LineupRequests.Clear();
    html = await Click("SaveLineup");

    Check(api.LineupRequests.SequenceEqual(new[] { "PUT", "GET" }) && html.Contains(text[TextKey.LineupSaved])
        && ListHtml(html, "starters-list").Contains("Server confirmed player") && html.Contains("fa-floppy-disk")
        && System.Text.RegularExpressions.Regex.IsMatch(html, "<dialog[^>]*class=\"lineup-save-confirmation\"[\\s\\S]*?>OK</button>[\\s\\S]*?</dialog>"),
        "Save performs PUT then GET, displays the server lineup and confirms in a modal with OK only after reload.");

    using (var payload = System.Text.Json.JsonDocument.Parse(api.SavedLineupBody!))
    {
        var body = payload.RootElement;

        Check(body.GetProperty("userId").GetInt64() == 77 && body.GetProperty("tactic").GetString() == "343"
            && body.GetProperty("lineup").GetProperty("2").GetString() == "striker-reserve"
            && body.GetProperty("substitutes").EnumerateObject().Count() == 4
            && body.GetProperty("substitutes").EnumerateObject().All(property => property.Value.GetString() == "")
            && body.GetProperty("type").GetString() == "default",
            "Save sends the edited lineup, four empty substitute positions and type default.");
    }

    html = await Click("MoveLineupPlayer", "defender", 1, null);
    Check(!html.Contains(text[TextKey.LineupSaved]), "Editing the lineup clears the previous save confirmation.");
    api.LineupAfterSave = null;
    api.SaveLineupStatus = HttpStatusCode.BadRequest;
    api.LineupRequests.Clear();
    var beforeFailedSave = ListHtml(html, "starters-list");
    html = await Click("SaveLineup");

    Check(api.LineupRequests.SequenceEqual(new[] { "PUT" }) && !html.Contains(text[TextKey.LineupSaved])
        && html.Contains("role=\"alert\"") && ListHtml(html, "starters-list") == beforeFailedSave,
        "A failed PUT neither reloads nor confirms and preserves the local selection.");

    api.SaveLineupStatus = HttpStatusCode.NoContent;
    api.GetLineupStatus = HttpStatusCode.ServiceUnavailable;
    api.LineupRequests.Clear();
    html = await Click("SaveLineup");

    Check(api.LineupRequests.SequenceEqual(new[] { "PUT", "GET" }) && html.Contains(text[TextKey.LineupReloadFailed])
        && !html.Contains(text[TextKey.LineupSaved]) && ListHtml(html, "starters-list") == beforeFailedSave,
        "A successful PUT with a failed reload reports the partial outcome and preserves the view.");

    api.GetLineupStatus = HttpStatusCode.OK;
    api.Lineup = lineupBeforeSaving;

    foreach (var language in new[] { AppLocalization.SpanishCulture, AppLocalization.ValencianCulture })
    {
        text.SetCulture(language);
        html = await Click("ShowSquadStatus");

        Check(System.Text.RegularExpressions.Regex.IsMatch(html, "id=\"squad-status-title\"[^>]*>" + text[TextKey.SquadStatus] + "</h2>")
            && html.Contains("(Lesión muscular)") && !html.Contains("ACTIVE_INFO_MUST_STAY_HIDDEN"),
            "The card title is localized while API information and active status filtering are preserved.");

        var helpDialog = System.Text.RegularExpressions.Regex.Match(html, "<dialog\\b[\\s\\S]*?</dialog>").Value;
        var withoutDialog = html.Replace(helpDialog, "");

        Check(helpDialog.Contains(text[TextKey.LineupDragInstructions]) && !withoutDialog.Contains(text[TextKey.LineupDragInstructions])
            && withoutDialog.Contains("fa-circle-info"), "Lineup instructions appear only in the information dialog.");

        foreach (var key in new[] { TextKey.StatusActive, TextKey.StatusInjured, TextKey.StatusRehabilitation, TextKey.StatusWeakened, TextKey.StatusBanned, TextKey.StatusAway })
            Check(helpDialog.Contains(text[key]), "The information dialog includes every translated status meaning.");

        api.LineupRequests.Clear();
        html = await Click("SaveLineup");

        Check(html.Contains(text[TextKey.LineupSaved]) && api.LineupRequests.SequenceEqual(new[] { "PUT", "GET" }),
            "The save confirmation is localized and follows a successful reload in both languages.");

        html = await Click("ClearLineupSaveFeedback");

        Check(!html.Contains("lineup-save-confirmation") && !html.Contains(text[TextKey.LineupSaved])
            && ListHtml(html, "starters-list").Length > 0, "Dismissing confirmation removes the modal and preserves the lineup.");
    }

    html = await Click("HideSquadStatus");

    Check(!html.Contains("class=\"squad-status-results\"") && !html.Contains("class=\"squad-results\""),
        "Hiding lineup leaves the Squad panel without results.");

    api.EmptyOwnSquad = true;
    html = await Click("ShowSquadStatus");

    Check(html.Contains("class=\"squad-status-results\"") && html.Contains(text[TextKey.NoResults])
        && System.Text.RegularExpressions.Regex.Matches(ListHtml(html, "starters-list"), "data-position=").Count == 11,
        "Empty squads retain eleven empty starter slots and show the substitutes empty state.");

    foreach (var role in new[] { "keeper", "defender", "midfielder", "striker" })
        Check(ListHtml(html, "starters-list").Contains("empty-" + role), "Empty slots receive the corresponding positional tint class.");

    api.EmptyOwnSquad = false;
    await Click("HideSquadStatus");
    api.Body = standingsBody;
    await Click("ShowSquadStatus");
    html = await Click("ShowSquadExcess");

    Check(html.Contains("class=\"squad-results\"") && !html.Contains("class=\"squad-status-results\""),
        "Opening squad excess replaces the lineup, including its local selection.");

    await Click("HideMoneyResults");
    html = await Click("HideQueryResults");

    Check(!html.Contains("class=\"query-results\"") && html.Contains("class=\"squad-results\""),
        "Hiding query results preserves the Squad panel results.");

    html = await Click("HideSquadResults");

    Check(!html.Contains("class=\"squad-results\"") && !html.Contains(text[TextKey.Hide]),
        "Squad excess results can be hidden.");

    api.TransportError = new HttpRequestException("Simulated query error");
    html = await Click("ShowSquadStatus");

    Check(html.Contains(text[TextKey.NetworkError]) && html.Contains("class=\"squad-status-results\"")
        && !html.Contains("class=\"squad-lineup\""), "Status errors do not show stale player data.");

    html = await Click("HideSquadStatus");
    Check(!html.Contains(text[TextKey.NetworkError]), "Status errors can be hidden.");
    html = await Click("ShowSquadExcess");

    Check(html.Contains(text[TextKey.NetworkError]) && html.Contains("class=\"squad-results\"")
        && !html.Contains("class=\"query-results\""), "Squad errors stay in the Squad panel.");

    html = await Click("HideSquadResults");

    Check(!html.Contains(text[TextKey.NetworkError]) && !html.Contains("class=\"squad-results\""),
        "Squad errors can be hidden.");

    html = await Click("ShowPoints");

    Check(html.Contains(text[TextKey.NetworkError]) && html.Contains("class=\"query-results\"")
        && html.Contains(text[TextKey.Hide]) && !html.Contains("role=\"dialog\""),
        "Query errors are displayed inline and can be hidden.");

    html = await Click("HideQueryResults");

    Check(!html.Contains(text[TextKey.NetworkError]) && !html.Contains("class=\"query-results\""),
        "Hiding a query error removes the whole result block.");

    api.TransportError = null;

    api.Body = "{\"items\":[]}";
    html = await Click("ShowPoints");

    Check(html.Contains(text[TextKey.NoResults]) && html.Contains(text[TextKey.Hide]),
        "Empty queries display the empty state inline with hide.");

    api.Body = """{"items":[{"totalPoints":10,"_embedded":{"user":{"id":"a","name":"Low scorer"}}},{"totalPoints":50,"_embedded":{"user":{"id":"b","name":"High scorer"}}}]}""";
    html = await Click("ShowClassifications");
    var currentMonth = DateOnly.FromDateTime(DateTime.Now);
    var months = SeasonCalendar.GetMonths(currentMonth);

    Check(api.LastStandingsQuery == $"?period=month&year={currentMonth.Year}&month={currentMonth.Month}"
        && System.Text.RegularExpressions.Regex.Matches(html, "<select\\b").Count == 2,
        "Classifications opens the monthly period and current month with two adjacent selectors.");

    foreach (var month in months)
        Check(html.Contains(month.ToString("MMMM yyyy", text.Culture)), "Every season month has a localized label and year.");

    Check(html.IndexOf("High scorer", StringComparison.Ordinal) < html.IndexOf("Low scorer", StringComparison.Ordinal)
        && html.Contains(text[TextKey.Points]) && !html.Contains("role=\"dialog\""),
        "Classification uses total points, sorted descending, inside the query tab.");

    Check(!html.Contains("row-over-limit") && !html.Contains("row-within-limit"),
        "Squad row shading is not applied to other queries.");

    var monthField = typeof(MainPage).GetField("selectedMonthIndex", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
    await renderer.Dispatcher.InvokeAsync(() => monthField.SetValue(page, 0));
    await Click("RefreshClassification");

    Check(api.LastStandingsQuery == $"?period=month&year={months[0].Year}&month=8",
        "Choosing the first month queries August of the season year.");

    var periodField = typeof(MainPage).GetField("classificationPeriod", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;

    foreach (var (period, query) in new[] { ("FirstHalf", "?period=firstHalf&wpe=true"), ("SecondHalf", "?period=secondHalf&wpe=true"), ("Season", "?period=total") })
    {
        await renderer.Dispatcher.InvokeAsync(() => periodField.SetValue(page, Enum.Parse(periodField.FieldType, period)));
        html = await Click("RefreshClassification");

        Check(api.LastStandingsQuery == query && System.Text.RegularExpressions.Regex.Matches(html, "<select\\b").Count == 1,
            "Half and season classifications use the specified endpoint and hide the month selector.");
    }

    await renderer.Dispatcher.InvokeAsync(() => periodField.SetValue(page, Enum.Parse(periodField.FieldType, "Month")));
    html = await Click("RefreshClassification");

    Check(System.Text.RegularExpressions.Regex.Matches(html, "<select\\b").Count == 2,
        "Returning to monthly classifications restores the month selector.");

    api.TransportError = new HttpRequestException("Simulated standings error");
    html = await Click("RefreshClassification");

    Check(html.Contains(text[TextKey.NetworkError]) && html.Contains("classification-filters") && html.Contains(text[TextKey.Hide]),
        "Classification errors remain inline and preserve the filters for retry.");

    api.TransportError = null;
    html = await Click("HideQueryResults");

    Check(!html.Contains("classification-filters") && !html.Contains("class=\"query-results\""),
        "Hide removes classification results and their selectors.");

    await Click("ShowClassifications");
    html = await Click("ShowPoints");
    Check(!html.Contains("classification-filters"), "Other query actions hide classification selectors.");
    await Click("Logout");

    Check(services.GetRequiredService<ISessionManager>().Session is null
        && services.GetRequiredService<NavigationManager>().Uri == "https://example.test/app/",
        "Sign-out clears the session and navigates back to login.");
}

await using (var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>()))
{
    foreach (var (status, apiValue, icon, colorClass) in new[]
        {
            (PlayerStatus.Active, "ACTIVE", "fa-check", "status-active"),
            (PlayerStatus.Injured, "INJURED", "fa-kit-medical", "status-injured"),
            (PlayerStatus.Rehabilitation, "REHABILITATION", "fa-kit-medical", "status-caution"),
            (PlayerStatus.Weakened, "WEAKENED", "fa-question", "status-caution"),
            (PlayerStatus.RedBanned, "RED_BANNED", "fa-square", "status-banned"),
            (PlayerStatus.YellowBanned, "YELLOW_BANNED", "fa-square", "status-banned"),
            (PlayerStatus.YellowRedBanned, "YELLOW_RED_BANNED", "fa-square", "status-banned"),
            (PlayerStatus.Suspended, "SUSPENDED", "fa-square", "status-banned"),
            (PlayerStatus.Away, "AWAY", "fa-person-walking-arrow-right", "status-away"),
            (PlayerStatus.GameBreak, "GAME_BREAK", "fa-person-walking-arrow-right", "status-away")
        })
    {
        var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(SquadPlayerCard.Player)] = new PlayerDetailsModel { Name = "Test player", Status = status, StatusInfo = "Test status information" }
        });

        var root = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<SquadPlayerCard>(parameters));
        var html = await renderer.Dispatcher.InvokeAsync(() => WebUtility.HtmlDecode(root.ToHtmlString()));

        Check(html.Contains($"class=\"fa-solid {icon} {colorClass}\"") && html.Contains($"aria-label=\"{apiValue}\""),
            "Every specified status uses its requested icon, color class and accessible label.");

        Check(html.Contains("Test status information") == (status != PlayerStatus.Active),
            "Active alone suppresses statusInfo; other status icons preserve it.");
    }
}

api.Body = null;
await auth.Logout();

using var secondClient = serviceCollection.BuildServiceProvider();

await using (var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>()))
{
    await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<MainPage>());

    Check(services.GetRequiredService<NavigationManager>().Uri == "https://example.test/app/",
        "Opening operations without a session redirects to login within the app base path.");
}

Check(secondClient.GetRequiredService<AppLocalization>().Culture.Name == AppLocalization.SpanishCulture, "Language is isolated per client.");
Check(ReferenceEquals(CultureInfo.CurrentUICulture, originalCulture), "Never change process-wide culture.");

Check(AppLocalization.NormalizeCulture("ca-ES") == AppLocalization.ValencianCulture
    && AppLocalization.NormalizeCulture("en-US") == AppLocalization.SpanishCulture, "Supported cultures and Spanish fallback.");

Check(text.ErrorMessage(new Exception("UNTRANSLATED")) == text[TextKey.OperationError], "Unknown errors use a translated fallback.");
Console.WriteLine("Localization checks passed for Spanish and Valencian, including UI and backend errors.");

sealed class TestApi : HttpMessageHandler
{
    public string? LastStandingsQuery { get; private set; }
    public int PaymentAttempts { get; private set; }
    public List<PostCashRequestModel> Payments { get; } = [];
    public int StandingsRequests { get; private set; }
    public int NewsRequests { get; private set; }
    public int? FailPaymentAt { get; set; }
    public bool IsAdmin { get; set; } = true;
    public bool IsLeader { get; set; }
    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
    public HttpStatusCode LoginStatus { get; set; } = HttpStatusCode.OK;
    public string? Body { get; set; }
    public Exception? TransportError { get; set; }
    public bool EmptyOwnSquad { get; set; }
    public HttpStatusCode SaveLineupStatus { get; set; } = HttpStatusCode.NoContent;
    public HttpStatusCode GetLineupStatus { get; set; } = HttpStatusCode.OK;
    public List<string> LineupRequests { get; } = [];
    public string? SavedLineupBody { get; private set; }
    public string? LineupAfterSave { get; set; }

    public string Lineup { get; set; } = """
        {"tactic":"352","items":{"lineup":{
            "1":{"id":"1927","name":"Gerard Moreno","club":{"name":"Villarreal"},"type":"striker","position":1,"status":"INJURED","statusInfo":"Lesión muscular"},
            "11":{"id":"keeper","name":"Portero","type":"keeper","position":11,"status":"ACTIVE","statusInfo":"ACTIVE_INFO_MUST_STAY_HIDDEN"}
        }}}
        """;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;

        if (path.EndsWith("/standings"))
        {
            LastStandingsQuery = request.RequestUri.Query;
            StandingsRequests++;
        }

        if (path == "/login")
        {
            return Task.FromResult(Json(LoginStatus, "{\"access_token\":\"test\",\"refresh_token\":\"refresh\",\"expires_in\":1800}"));
        }

        if (path == "/")
        {
            return Task.FromResult(Json(HttpStatusCode.OK, System.Text.Json.JsonSerializer.Serialize(new
            {
                user = new { id = "77", name = "User", isAdmin = IsAdmin, isLeader = IsLeader },
                community = new { id = "test" }
            })));
        }

        if (TransportError is not null)
        {
            return Task.FromException<HttpResponseMessage>(TransportError);
        }

        if (path == "/matchdays")
        {
            return Task.FromResult(Json(HttpStatusCode.OK,
                """[{"matchdayKey":"9","timestamp":"2026-10-16T21:02:01+02:00","started":true,"finished":true},{"matchdayKey":"10","timestamp":"2026-10-20T10:00:00Z","started":false,"finished":false}]"""));
        }

        if (path == "/communities/test/users/77/news")
        {
            NewsRequests++;

            return Task.FromResult(Json(HttpStatusCode.OK, """
                {"newsList":{"entries":[
                    {"id":"future-bonus","date":"2026-10-20T10:00:00Z","type":"TRANSACTION_BONUS","message":{"text":"Abono: 99.000.000 serán abonados a Player A por el administrador de la comunidad (Player B). Razón: Whatever"}},
                    {"id":"bonus-a","date":"2026-10-18T11:00:00Z","type":"TRANSACTION_BONUS","message":{"text":"Abono: 100.000 serán abonados a Player A por el administrador de la comunidad (Player B). Razón: Whatever"}},
                    {"id":"bonus-b","date":"2026-10-18T10:00:00Z","type":"TRANSACTION_BONUS","message":{"text":"Abono: 500.000 serán abonados a Player B por el administrador de la comunidad (Player A). Razón: Other"}},
                    {"id":"penalty-b","date":"2026-10-18T09:00:00Z","type":"TRANSACTION_BONUS","message":{"text":"Pena disciplinaria: 100.000 € han sido restados a Player B por el administrador de la comunidad (Player A). Razón: Punts últims"}},
                    {"id":"points","date":"2026-10-17T10:00:00Z","type":"SYSTEM_ADMINISTRATION","message":{"type":"POINTS_CALCULATION","matchday":9}},
                    {"id":"old-bonus","date":"2026-10-16T10:00:00Z","type":"TRANSACTION_BONUS","message":{"text":"Unrelated old payment"}}
                ],"hasMore":false}}
                """));
        }

        if (path == "/communities/test/users/77/lineup")
        {
            LineupRequests.Add(request.Method.Method);

            if (request.Method == HttpMethod.Put)
            {
                return SaveLineup(request, cancellationToken);
            }

            return Task.FromResult(Json(GetLineupStatus, EmptyOwnSquad ? """{"tactic":"352","items":{"lineup":{}}}""" : Lineup));
        }

        if (path == "/users/77/squad")
        {
            return Task.FromResult(Json(HttpStatusCode.OK,
                EmptyOwnSquad ? """{"items":[]}""" : """{"items":[{"id":"1927"},{"id":"keeper"},{"id":"defender"},{"id":"midfielder"},{"id":"striker-reserve"},{"id":"null-info"}]}"""));
        }

        if (path.StartsWith("/communities/test/users/77/players/", StringComparison.Ordinal))
        {
            var detail = path.Split('/')[^1] switch
            {
                "1927" => """{"name":"Gerard Moreno","club":{"name":"Villarreal"},"type":"striker","status":"INJURED","statusInfo":"Lesión muscular"}""",
                "striker-reserve" => """{"name":"Delantero suplente","type":"striker","status":"ACTIVE"}""",
                "keeper" => """{"name":"Portero","type":"keeper","status":"ACTIVE","statusInfo":"ACTIVE_INFO_MUST_STAY_HIDDEN"}""",
                "defender" => """{"name":"Defensa","type":"defender","status":"SUSPENDED","statusInfo":"Acumulación de tarjetas"}""",
                "midfielder" => """{"name":"Centrocampista","type":"midfielder","status":"WEAKENED","statusInfo":"Molestias"}""",
                _ => """{"name":"Otro jugador","club":null,"type":null,"status":null,"statusInfo":null}"""
            };

            return Task.FromResult(Json(HttpStatusCode.OK, detail));
        }

        if (path.EndsWith("/squad"))
        {
            return Task.FromResult(Json(HttpStatusCode.OK, System.Text.Json.JsonSerializer.Serialize(new
            {
                items = Enumerable.Range(0, path.Contains("/a/") ? 26 : path.Contains("/b/") ? 25 : 24)
                    .Select(id => new { id = id.ToString(), purchaseInfo = new { date = "2026-10-01T00:00:00+02:00" } })
            })));
        }

        if (path.EndsWith("/penalties"))
        {
            return Pay(request, cancellationToken);
        }

        return Task.FromResult(Json(Status, Status == HttpStatusCode.OK ? Body ?? "{\"items\":[]}" : "EXTERNAL ERROR"));
    }

    private async Task<HttpResponseMessage> Pay(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        PaymentAttempts++;

        if (PaymentAttempts == FailPaymentAt)
        {
            return Json(HttpStatusCode.InternalServerError, "{}");
        }

        Payments.Add(Newtonsoft.Json.JsonConvert.DeserializeObject<PostCashRequestModel>(await request.Content!.ReadAsStringAsync(cancellationToken))!);
        return Json(HttpStatusCode.OK, "{}");
    }

    private async Task<HttpResponseMessage> SaveLineup(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        SavedLineupBody = await request.Content!.ReadAsStringAsync(cancellationToken);

        if (SaveLineupStatus == HttpStatusCode.NoContent && LineupAfterSave is not null)
            Lineup = LineupAfterSave;

        return new HttpResponseMessage(SaveLineupStatus);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}

sealed class TestJsRuntime : IJSRuntime
{
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        => throw new NotSupportedException("Static rendering does not execute JavaScript.");

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        => throw new NotSupportedException("Static rendering does not execute JavaScript.");
}

sealed class MainPageHost : ComponentBase, IDisposable
{
    [Inject] public AppLocalization Text { get; set; } = default!;
    [Parameter] public Action<MainPage> Capture { get; set; } = default!;

    public void Dispose() => Text.Changed -= Changed;

    protected override void OnInitialized() => Text.Changed += Changed;

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<CascadingValue<CultureInfo>>(0);
        builder.AddAttribute(1, "Value", Text.Culture);

        builder.AddAttribute(2, "ChildContent", (RenderFragment)(content =>
        {
            content.OpenComponent<MainPage>(0);
            content.AddComponentReferenceCapture(1, component => Capture((MainPage)component));
            content.CloseComponent();
        }));

        builder.CloseComponent();
    }

    private void Changed() => _ = InvokeAsync(StateHasChanged);
}

sealed class TestNavigationManager : NavigationManager
{
    public TestNavigationManager() => Initialize("https://example.test/app/", "https://example.test/app/main");

    protected override void NavigateToCore(string uri, NavigationOptions options)
        => Uri = ToAbsoluteUri(uri).AbsoluteUri;
}

sealed class TestHost : ComponentBase, IDisposable
{
    [Inject] public AppLocalization Text { get; set; } = default!;

    protected override void OnInitialized() => Text.Changed += Changed;
    private void Changed() => StateHasChanged();
    public void Dispose() => Text.Changed -= Changed;

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<CascadingValue<CultureInfo>>(0);
        builder.AddAttribute(1, "Value", Text.Culture);

        builder.AddAttribute(2, "ChildContent", (RenderFragment)(content =>
        {
            content.OpenComponent<LoginPage>(0);
            content.CloseComponent();
            content.OpenComponent<ActionResults>(1);
            content.CloseComponent();
        }));

        builder.CloseComponent();
    }
}
