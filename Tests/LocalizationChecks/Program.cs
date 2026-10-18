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

static void Check(bool value, string message)
{
    if (!value) throw new Exception(message);
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
    Check((await Capture(() => auth.LoginAsync("", ""))).Message == text[TextKey.SignInError],
        "Empty credentials are sent to the API and use the generic login error when rejected.");
    api.LoginStatus = HttpStatusCode.OK;
    await auth.LogoutAsync();
    Check((await Capture(() => services.GetRequiredService<ISessionManager>().GetValidAccessTokenAsync())).Message == text[TextKey.SignInRequired], "Localized session error.");
    foreach (var status in new[] { HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized,
        HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError, HttpStatusCode.Created,
        HttpStatusCode.NoContent })
    {
        api.LoginStatus = status;
        Check((await Capture(() => auth.LoginAsync("user", "password"))).Message == text[TextKey.SignInError],
            "Every non-200 authentication response uses the same error.");
        Check(services.GetRequiredService<ISessionManager>().Session is null, "Rejected authentication leaves no session.");
    }
    api.LoginStatus = HttpStatusCode.OK;
    await auth.LoginAsync("user", "password");
    foreach (var (status, key) in new[] {
        (HttpStatusCode.Forbidden, TextKey.PermissionDenied),
        (HttpStatusCode.NotFound, TextKey.ApiNotFound),
        (HttpStatusCode.TooManyRequests, TextKey.TooManyRequests),
        (HttpStatusCode.InternalServerError, TextKey.ServiceUnavailable),
        (HttpStatusCode.BadRequest, TextKey.HttpError) })
    {
        api.Status = status;
        var exception = await Capture(() => services.GetRequiredService<PointsService>().GetLastRoundPointsAsync());
        Check(exception is HttpRequestException http && http.StatusCode == status, "Preserve HTTP status.");
        Check(exception.Message == text.Format(key, (int)status) && !exception.Message.Contains("EXTERNAL ERROR"), "Translated HTTP errors without external text.");
    }
    api.Status = HttpStatusCode.OK;
    api.Body = "broken JSON";
    Check((await Capture(() => services.GetRequiredService<PointsService>().GetLastRoundPointsAsync())).Message == text[TextKey.InvalidJsonResponse], "Localized malformed JSON.");
    api.Body = "null";
    Check((await Capture(() => services.GetRequiredService<PointsService>().GetLastRoundPointsAsync())).Message == text[TextKey.EmptyJsonResponse], "Localized empty JSON.");
    api.Body = null;
    api.TransportError = new HttpRequestException("UNTRANSLATED TRANSPORT ERROR");
    Check((await Capture(() => services.GetRequiredService<PointsService>().GetLastRoundPointsAsync())).Message == text[TextKey.NetworkError], "Localized transport error.");
    api.TransportError = new TaskCanceledException("UNTRANSLATED TIMEOUT");
    Check((await Capture(() => services.GetRequiredService<PointsService>().GetLastRoundPointsAsync())).Message == text[TextKey.TimeoutError], "Localized timeout.");
    api.TransportError = null;
    var partial = new CashOperationException(text, "A < B", [], new Exception("external"));
    Check(partial.Message == text.Format(TextKey.CashOperationError, "A < B"), "Localized formatted partial payment.");
}

await auth.LogoutAsync();
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
        await auth.LoginAsync("user", "password");
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var root = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<MainPage>());
        var html = await renderer.Dispatcher.InvokeAsync(() => WebUtility.HtmlDecode(root.ToHtmlString()));
        var canManageMoney = isAdmin || isLeader;
        Check(html.Contains($">{text[TextKey.Money]}</button>") == canManageMoney,
            "Money tab is only rendered for administrators or leaders.");
        foreach (var key in new[] { TextKey.PayPoints, TextKey.PayBreak })
            Check(html.Contains(text[key]) == canManageMoney, "Money actions follow the session permissions.");
        Check(!html.Contains(text[TextKey.RevertPoints]), "Revert is hidden before paying a round.");
        Check(html.Contains($">{text[TextKey.Queries]}</button>")
            && html.Contains(text[TextKey.QueryPoints]) && html.Contains(text[TextKey.QuerySquads]),
            "Queries remain available for every signed-in user.");
    }
}
await auth.LogoutAsync();

api.IsAdmin = true;
api.IsLeader = false;
api.Body = """{"items":[{"lastPoints":"2","_embedded":{"user":{"id":"a","name":"Player A"}}},{"lastPoints":"3","_embedded":{"user":{"id":"b","name":"Player B"}}}]}""";
await auth.LoginAsync("user", "password");
await using (var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>()))
{
    MainPage page = null!;
    var root = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<MainPageHost>(
        ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(MainPageHost.Capture)] = (Action<MainPage>)(value => page = value) })));

    async Task<string> ClickAsync(string methodName)
    {
        await renderer.Dispatcher.InvokeAsync(() => ((IHandleEvent)page).HandleEventAsync(
            new EventCallbackWorkItem((Func<Task>)(() => (Task?)typeof(MainPage)
                .GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(page, null) ?? Task.CompletedTask)), null));
        return await renderer.Dispatcher.InvokeAsync(() => WebUtility.HtmlDecode(root.ToHtmlString()));
    }

    var initialHtml = await renderer.Dispatcher.InvokeAsync(() => root.ToHtmlString());
    Check(!System.Text.RegularExpressions.Regex.IsMatch(initialHtml, "<header[^>]*\\binert(?:[=\\s>])")
        && !System.Text.RegularExpressions.Regex.IsMatch(initialHtml, "<button[^>]*class=\"action-button sign-out\"[^>]*\\bdisabled(?:[=\\s>])"),
        "The idle header is interactive and sign-out is enabled.");

    var html = await ClickAsync("PayRoundPointsAsync");
    Check(html.Contains(text[TextKey.PaidMoney]) && html.Contains("Player A")
        && html.Contains(text[TextKey.RevertPoints]) && !html.Contains("role=\"dialog\""),
        "Round payment shows inline results and offers revert without opening a dialog.");
    Check(html.IndexOf("Player A", StringComparison.Ordinal) < html.IndexOf(text[TextKey.RevertPoints], StringComparison.Ordinal),
        "Revert is placed below the payment results.");
    Check(html.Contains(text[TextKey.Hide]), "Money results offer a hide button.");
    html = await ClickAsync("HideMoneyResults");
    Check(!html.Contains("class=\"money-results\"") && !html.Contains(text[TextKey.RevertPoints])
        && !html.Contains(text[TextKey.Hide]) && html.Contains(text[TextKey.PayPoints]) && html.Contains(text[TextKey.PayBreak]),
        "Hiding round results also removes revert and keeps the payment actions available.");

    await ClickAsync("PayRoundPointsAsync");
    html = await ClickAsync("RevertRoundPointsAsync");
    Check(html.Contains(text[TextKey.RevertedMoney]) && !html.Contains(text[TextKey.RevertPoints])
        && html.Contains(text[TextKey.Hide]) && !html.Contains("role=\"dialog\""),
        "Revert updates inline results, hides its button and offers hide.");

    await ClickAsync("PayRoundPointsAsync");
    html = await ClickAsync("PayInternationalBreakAsync");
    Check(html.Contains(text[TextKey.BreakPaymentCompleted]) && !html.Contains(text[TextKey.RevertPoints])
        && html.Contains(text[TextKey.Hide]) && !html.Contains("role=\"dialog\""),
        "Break payment replaces inline results without offering round reversal and offers hide.");
    html = await ClickAsync("HideMoneyResults");
    Check(!html.Contains("class=\"money-results\""), "Break payment results can be hidden.");

    api.FailPaymentAt = api.PaymentAttempts + 2;
    html = await ClickAsync("PayRoundPointsAsync");
    Check(html.Contains(text[TextKey.IncompleteOperation]) && html.Contains("Player A")
        && !html.Contains(text[TextKey.RevertPoints]) && !html.Contains("role=\"dialog\""),
        "Partial payments retain confirmed results inline without offering a full round reversal.");
    api.FailPaymentAt = null;

    api.TransportError = new HttpRequestException("Simulated network error");
    html = await ClickAsync("PayInternationalBreakAsync");
    Check(html.Contains(text[TextKey.NetworkError]) && html.Contains(text[TextKey.Hide]) && !html.Contains("role=\"dialog\""),
        "Money errors are displayed inline.");
    html = await ClickAsync("HideMoneyResults");
    Check(!html.Contains(text[TextKey.NetworkError]) && !html.Contains("class=\"money-results\""),
        "Money errors can be hidden too.");
    api.TransportError = null;

    await ClickAsync("PayRoundPointsAsync");
    html = await ClickAsync("ShowPointsAsync");
    Check(html.Contains("class=\"query-results\"") && html.Contains(text[TextKey.LastRoundPoints])
        && html.Contains("Player A") && html.Contains(text[TextKey.Hide]) && !html.Contains("role=\"dialog\""),
        "Last points results are displayed inline with hide and no dialog.");
    Check(html.Contains("class=\"money-results\"") && html.Contains(text[TextKey.RevertPoints]),
        "Queries preserve the money tab results.");

    html = await ClickAsync("HideQueryResults");
    Check(!html.Contains("class=\"query-results\"") && html.Contains("class=\"money-results\"")
        && html.Contains(text[TextKey.RevertPoints]), "Hiding query results does not clear money results.");

    html = await ClickAsync("ShowSquadExcessAsync");
    Check(html.Contains("class=\"query-results\"") && html.Contains(text[TextKey.SquadExcess])
        && html.Contains(text[TextKey.Players]) && html.Contains("26") && !html.Contains("role=\"dialog\""),
        "Squad excess shows its own title and player counts inline.");
    await ClickAsync("HideMoneyResults");
    html = await ClickAsync("HideQueryResults");
    Check(!html.Contains("class=\"query-results\"") && !html.Contains(text[TextKey.Hide]),
        "Squad excess results can be hidden.");

    api.TransportError = new HttpRequestException("Simulated query error");
    html = await ClickAsync("ShowPointsAsync");
    Check(html.Contains(text[TextKey.NetworkError]) && html.Contains("class=\"query-results\"")
        && html.Contains(text[TextKey.Hide]) && !html.Contains("role=\"dialog\""),
        "Query errors are displayed inline and can be hidden.");
    html = await ClickAsync("HideQueryResults");
    Check(!html.Contains(text[TextKey.NetworkError]) && !html.Contains("class=\"query-results\""),
        "Hiding a query error removes the whole result block.");
    api.TransportError = null;

    api.Body = "{\"items\":[]}";
    html = await ClickAsync("ShowPointsAsync");
    Check(html.Contains(text[TextKey.NoResults]) && html.Contains(text[TextKey.Hide]),
        "Empty queries display the empty state inline with hide.");

    api.Body = """{"items":[{"totalPoints":10,"_embedded":{"user":{"id":"a","name":"Low scorer"}}},{"totalPoints":50,"_embedded":{"user":{"id":"b","name":"High scorer"}}}]}""";
    html = await ClickAsync("ShowClassificationsAsync");
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

    var monthField = typeof(MainPage).GetField("selectedMonthIndex", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
    await renderer.Dispatcher.InvokeAsync(() => monthField.SetValue(page, 0));
    await ClickAsync("RefreshClassificationAsync");
    Check(api.LastStandingsQuery == $"?period=month&year={months[0].Year}&month=8",
        "Choosing the first month queries August of the season year.");
    var periodField = typeof(MainPage).GetField("classificationPeriod", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
    foreach (var (period, query) in new[] { ("FirstHalf", "?period=firstHalf&wpe=true"), ("SecondHalf", "?period=secondHalf&wpe=true") })
    {
        await renderer.Dispatcher.InvokeAsync(() => periodField.SetValue(page, Enum.Parse(periodField.FieldType, period)));
        html = await ClickAsync("RefreshClassificationAsync");
        Check(api.LastStandingsQuery == query && System.Text.RegularExpressions.Regex.Matches(html, "<select\\b").Count == 1,
            "Half classifications use the specified endpoint and hide the month selector.");
    }
    await renderer.Dispatcher.InvokeAsync(() => periodField.SetValue(page, Enum.Parse(periodField.FieldType, "Month")));
    html = await ClickAsync("RefreshClassificationAsync");
    Check(System.Text.RegularExpressions.Regex.Matches(html, "<select\\b").Count == 2,
        "Returning to monthly classifications restores the month selector.");
    api.TransportError = new HttpRequestException("Simulated standings error");
    html = await ClickAsync("RefreshClassificationAsync");
    Check(html.Contains(text[TextKey.NetworkError]) && html.Contains("classification-filters") && html.Contains(text[TextKey.Hide]),
        "Classification errors remain inline and preserve the filters for retry.");
    api.TransportError = null;
    html = await ClickAsync("HideQueryResults");
    Check(!html.Contains("classification-filters") && !html.Contains("class=\"query-results\""),
        "Hide removes classification results and their selectors.");
    await ClickAsync("ShowClassificationsAsync");
    html = await ClickAsync("ShowPointsAsync");
    Check(!html.Contains("classification-filters"), "Other query actions hide classification selectors.");
    await ClickAsync("LogoutAsync");
    Check(services.GetRequiredService<ISessionManager>().Session is null
        && services.GetRequiredService<NavigationManager>().Uri == "https://example.test/app/",
        "Sign-out clears the session and navigates back to login.");
}
api.Body = null;
await auth.LogoutAsync();

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
    public int? FailPaymentAt { get; set; }
    public bool IsAdmin { get; set; } = true;
    public bool IsLeader { get; set; }
    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
    public HttpStatusCode LoginStatus { get; set; } = HttpStatusCode.OK;
    public string? Body { get; set; }
    public Exception? TransportError { get; set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        if (path.EndsWith("/standings")) LastStandingsQuery = request.RequestUri.Query;
        if (path == "/login")
            return Task.FromResult(Json(LoginStatus, "{\"access_token\":\"test\",\"refresh_token\":\"refresh\",\"expires_in\":1800}"));
        if (path == "/")
            return Task.FromResult(Json(HttpStatusCode.OK, System.Text.Json.JsonSerializer.Serialize(new
            {
                user = new { id = "user", name = "User", isAdmin = IsAdmin, isLeader = IsLeader },
                community = new { id = "test" }
            })));
        if (TransportError is not null) return Task.FromException<HttpResponseMessage>(TransportError);
        if (path.EndsWith("/squad"))
            return Task.FromResult(Json(HttpStatusCode.OK, System.Text.Json.JsonSerializer.Serialize(new
            {
                items = Enumerable.Range(0, 26).Select(id => new { id = id.ToString() })
            })));
        if (path.EndsWith("/penalties"))
        {
            PaymentAttempts++;
            return Task.FromResult(Json(PaymentAttempts == FailPaymentAt ? HttpStatusCode.InternalServerError : HttpStatusCode.OK, "{}"));
        }
        return Task.FromResult(Json(Status, Status == HttpStatusCode.OK ? Body ?? "{\"items\":[]}" : "EXTERNAL ERROR"));
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}

sealed class MainPageHost : ComponentBase
{
    [Parameter] public Action<MainPage> Capture { get; set; } = default!;

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<MainPage>(0);
        builder.AddComponentReferenceCapture(1, component => Capture((MainPage)component));
        builder.CloseComponent();
    }
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
