using System.Globalization;
using System.Net;
using System.Text;
using System.Resources;
using ComunioAdminTools.Core;
using ComunioAdminTools.Core.Authentication;
using ComunioAdminTools.Core.Localization;
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
    Check((await Capture(() => auth.LoginAsync("", ""))).Message == text[TextKey.CredentialsRequired], "Localized credential validation.");
    await auth.LogoutAsync();
    Check((await Capture(() => auth.GetValidAccessTokenAsync())).Message == text[TextKey.SignInRequired], "Localized session error.");
    api.LoginStatus = HttpStatusCode.Unauthorized;
    Check((await Capture(() => auth.LoginAsync("user", "password"))).Message == text[TextKey.CredentialsRejected], "Localized authentication rejection.");
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
    var factory = services.GetRequiredService<IHttpClientFactory>();
    Check((await Capture(() => factory.CreateClient("Comunio").GetAsync("https://example.com/"))).Message == text[TextKey.TokenDestinationNotAllowed], "Localized domain restriction.");
    var partial = new CashOperationException(text, "A < B", [], new Exception("external"));
    Check(partial.Message == text.Format(TextKey.CashOperationError, "A < B"), "Localized formatted partial payment.");
}

text.SetCulture(AppLocalization.SpanishCulture);
var retainedError = new AuthenticationRequiredException(text, TextKey.SessionExpired);
await using (var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>()))
{
    var root = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<TestHost>());
    var spanishHtml = await renderer.Dispatcher.InvokeAsync(() => WebUtility.HtmlDecode(root.ToHtmlString()));
    Check(spanishHtml.Contains("Comunio</h1>") && spanishHtml.Contains("Admin Tools</p>") && !spanishHtml.Contains("class=\"actions\""), "Fixed brand and operations hidden before login.");
    Check(spanishHtml.Contains("Usuario") && spanishHtml.Contains("Cargando") && spanishHtml.Contains("No hay resultados"), "Spanish UI and results.");
    await renderer.Dispatcher.InvokeAsync(() => text.SetCulture(AppLocalization.ValencianCulture));
    var valencianHtml = await renderer.Dispatcher.InvokeAsync(() => WebUtility.HtmlDecode(root.ToHtmlString()));
    Check(valencianHtml.Contains("Comunio</h1>") && valencianHtml.Contains("Admin Tools</p>"), "App name is unchanged in Valencian.");
    Check(valencianHtml.Contains("Usuari") && valencianHtml.Contains("Carregant") && valencianHtml.Contains("No hi ha resultats"), "Runtime switch updates an open results dialog.");
    Check(!valencianHtml.Contains("Herramientas de administración") && retainedError.Message == text[TextKey.SessionExpired], "No stale translated strings.");
}

using var secondClient = serviceCollection.BuildServiceProvider();
Check(secondClient.GetRequiredService<AppLocalization>().Culture.Name == AppLocalization.SpanishCulture, "Language is isolated per client.");
Check(ReferenceEquals(CultureInfo.CurrentUICulture, originalCulture), "Never change process-wide culture.");
Check(AppLocalization.NormalizeCulture("ca-ES") == AppLocalization.ValencianCulture
    && AppLocalization.NormalizeCulture("en-US") == AppLocalization.SpanishCulture, "Supported cultures and Spanish fallback.");
Check(text.ErrorMessage(new Exception("UNTRANSLATED")) == text[TextKey.OperationError], "Unknown errors use a translated fallback.");
Console.WriteLine("Localization checks passed for Spanish and Valencian, including UI and backend errors.");

sealed class TestApi : HttpMessageHandler
{
    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
    public HttpStatusCode LoginStatus { get; set; } = HttpStatusCode.OK;
    public string? Body { get; set; }
    public Exception? TransportError { get; set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        if (path == "/login")
            return Task.FromResult(Json(LoginStatus, "{\"access_token\":\"test\",\"refresh_token\":\"refresh\",\"expires_in\":1800}"));
        if (path == "/")
            return Task.FromResult(Json(HttpStatusCode.OK, "{\"user\":{\"id\":\"admin\",\"isAdmin\":true},\"community\":{\"id\":\"test\"}}"));
        if (TransportError is not null) return Task.FromException<HttpResponseMessage>(TransportError);
        return Task.FromResult(Json(Status, Status == HttpStatusCode.OK ? Body ?? "{\"items\":[]}" : "EXTERNAL ERROR"));
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
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
            content.OpenComponent<MainPage>(0);
            content.AddAttribute(1, "IsLoading", true);
            content.AddAttribute(2, "ResultsContent", (RenderFragment)(results =>
            {
                results.OpenComponent<ActionResults>(0);
                results.CloseComponent();
            }));
            content.CloseComponent();
        }));
        builder.CloseComponent();
    }
}
