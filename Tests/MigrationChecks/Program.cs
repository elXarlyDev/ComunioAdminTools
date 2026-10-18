using System.Net;
using System.Text;
using ComunioAdminTools.Core;
using ComunioAdminTools.Core.Authentication;
using ComunioAdminTools.Core.Localization;
using ComunioAdminTools.Core.Models;
using ComunioAdminTools.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

static void Check(bool value, string message)
{
    if (!value) throw new Exception(message);
}

static async Task CheckSessionClearedAsync(ISessionManager session, string message)
{
    Check(session.Session is null, message);
    try
    {
        await session.GetValidAccessTokenAsync();
        throw new Exception(message);
    }
    catch (AuthenticationRequiredException ex)
    {
        Check(ex.ResourceKey == TextKey.SignInRequired, message);
    }
}

foreach (var (today, expectedStart, expectedCount) in new[]
{
    (new DateOnly(2026, 10, 18), new DateOnly(2026, 8, 1), 3),
    (new DateOnly(2027, 1, 10), new DateOnly(2026, 8, 1), 6),
    (new DateOnly(2027, 8, 20), new DateOnly(2027, 8, 1), 1),
    (new DateOnly(2027, 7, 31), new DateOnly(2026, 8, 1), 12)
})
{
    var months = SeasonCalendar.GetMonths(today);
    Check(months.Count == expectedCount && months[0] == expectedStart
        && months[^1] == new DateOnly(today.Year, today.Month, 1)
        && months.SequenceEqual(Enumerable.Range(0, expectedCount).Select(expectedStart.AddMonths)),
        "Los meses de temporada van de agosto al mes actual, incluidos y sin saltos.");
}

var api = new FakeApi();
var services = new ServiceCollection();
services.AddComunioCore();
services.AddHttpClient("ComunioAuth").ConfigurePrimaryHttpMessageHandler(() => api);
services.AddHttpClient("Comunio").ConfigurePrimaryHttpMessageHandler(() => api);
using var provider = services.BuildServiceProvider();
var auth = provider.GetRequiredService<AuthService>();
await auth.LoginAsync("user", "password");
var cash = provider.GetRequiredService<CashService>();
var paid = await cash.PayRoundPointsAsync();
Check(api.Payments.Select(x => x.Amount).SequenceEqual([10_000_000, 10_000_000, 5_000_000, 5_000_000]), "Pagos por puntos y bloques de 100.");
Check(api.Payments.All(x => x.Reason == "PUNTS"), "Motivo de los abonos.");
Check(paid.Count == 4 && paid[0].Amount == 25_000_000 && paid.Count(x => x.Amount == 0) == 2, "Resultados ordenados e importes cero.");
api.Payments.Clear();
await cash.RevertRoundPointsAsync();
Check(api.Payments.Select(x => x.Amount).SequenceEqual([-10_000_000, -10_000_000, -5_000_000, -5_000_000]), "Reversion de puntos.");
Check(api.Payments.All(x => x.Reason == "CORREGIR ERROR PUNTS"), "Motivo de la reversion.");
api.Payments.Clear();
await cash.PayInternationalBreakAsync();
Check(api.Payments.Count == 4 && api.Payments.All(x => x.Amount == 5_000_000 && x.Reason == "PARÓ SELECCIONS"), "Abono de 5M para todos.");
api.Payments.Clear();
var points = await provider.GetRequiredService<PointsService>().GetLastRoundPointsAsync();
Check(points.Select(x => x.Points).SequenceEqual([250, 50, 0, -3]), "Consulta y ordenacion de puntos.");
var excess = await provider.GetRequiredService<SquadService>().GetSquadExcessAsync();
Check(excess.Count == 1 && excess[0].UserName == "A" && excess[0].PlayerCount == 26, "Limite estricto de 25 jugadores.");
Check(api.Payments.Count == 0 && api.ProfileRequests == 1, "Consultas sin pagos y reutilizacion del perfil.");
var pointsService = provider.GetRequiredService<IPointsService>();
await pointsService.GetMonthlyStandingsAsync(2026, 8);
Check(api.LastStandingsQuery == "?period=month&year=2026&month=8", "Clasificacion mensual con year y month, sin wpe.");
await pointsService.GetMonthlyStandingsAsync(2027, 1);
Check(api.LastStandingsQuery == "?period=month&year=2027&month=1", "La consulta mensual conserva el año del mes seleccionado.");
await pointsService.GetFirstHalfStandingsAsync();
Check(api.LastStandingsQuery == "?period=firstHalf&wpe=true", "Clasificacion de primera mitad.");
await pointsService.GetSecondHalfStandingsAsync();
Check(api.LastStandingsQuery == "?period=secondHalf&wpe=true", "Clasificacion de segunda mitad.");
Check(api.Payments.Count == 0 && api.ProfileRequests == 1, "Clasificaciones sin pagos ni recarga del perfil.");
api.FailAtPayment = 3;
try
{
    await cash.PayRoundPointsAsync();
    throw new Exception("Se esperaba un fallo parcial.");
}
catch (CashOperationException ex)
{
    Check(ex.ConfirmedPayments.Single().Amount == 20_000_000 && api.PaymentAttempts == 3, "Conservar bloques confirmados, detenerse y no reintentar.");
}
await auth.LogoutAsync();
await CheckSessionClearedAsync(provider.GetRequiredService<ISessionManager>(), "Logout elimina tokens y perfil.");
api.FailProfile = true;
try
{
    await auth.LoginAsync("user", "password");
    throw new Exception("Se esperaba un fallo al cargar el perfil.");
}
catch (AuthenticationRequiredException)
{
    await CheckSessionClearedAsync(provider.GetRequiredService<ISessionManager>(),
        "Un login fallido elimina tokens y perfil.");
}
try
{
    await provider.GetRequiredService<ISessionManager>().LoadAsync(new("test", "refresh", DateTimeOffset.UtcNow.AddMinutes(30)));
    throw new Exception("Se esperaba un fallo al cargar el perfil directamente.");
}
catch (HttpRequestException)
{
    await CheckSessionClearedAsync(provider.GetRequiredService<ISessionManager>(),
        "LoadAsync limpia tokens y perfil por si mismo cuando falla la carga.");
}
api.FailProfile = false;
await auth.LoginAsync("user", "password");
Check(provider.GetRequiredService<ISessionManager>().Session!.User.Name == "Admin",
    "Se puede volver a iniciar sesion tras un fallo de perfil.");
await auth.LogoutAsync();
using var otherClient = services.BuildServiceProvider();
Check(!ReferenceEquals(provider.GetRequiredService<ISessionManager>(), otherClient.GetRequiredService<ISessionManager>()), "Sesiones aisladas por cliente.");
var restoredServices = new ServiceCollection();
var restoredApi = new FakeApi();
restoredServices.AddComunioCore();
restoredServices.AddHttpClient("ComunioAuth").ConfigurePrimaryHttpMessageHandler(() => restoredApi);
restoredServices.AddHttpClient("Comunio").ConfigurePrimaryHttpMessageHandler(() => restoredApi);
using var restoredClient = restoredServices.BuildServiceProvider();
restoredApi.ExpireNextToken = true;
await restoredClient.GetRequiredService<AuthService>().LoginAsync("user", "password").WaitAsync(TimeSpan.FromSeconds(10));
Check(restoredApi.TokenRequests == 1 && restoredApi.ProfileRequests == 1,
    "El perfil inicial usa el token explicito sin pasar por el handler ni renovar.");
using var restoredHttp = restoredClient.GetRequiredService<IHttpClientFactory>().CreateClient("Comunio");
var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => restoredHttp.GetAsync("")));
Check(responses.All(x => x.IsSuccessStatusCode) && restoredApi.TokenRequests == 2,
    "El pipeline renueva una sola vez antes de enviar las peticiones concurrentes.");
foreach (var response in responses) response.Dispose();
var restoredSession = restoredClient.GetRequiredService<ISessionManager>();
Check(await restoredSession.GetValidAccessTokenAsync() == "test", "El handler dispone del token renovado.");
restoredApi.TokenLifetimeSeconds = 30;
await restoredClient.GetRequiredService<AuthService>().LoginAsync("user", "password");
var cachedProfile = restoredSession.Session;
var profileRequests = restoredApi.ProfileRequests;
await restoredSession.GetValidAccessTokenAsync();
Check(ReferenceEquals(cachedProfile, restoredSession.Session) && restoredApi.ProfileRequests == profileRequests,
    "Renovar conserva el perfil sin volver a cargarlo.");
restoredApi.FailTokenTransport = true;
try
{
    await restoredSession.GetValidAccessTokenAsync();
    throw new Exception("Se esperaba un error de transporte al renovar.");
}
catch (HttpRequestException)
{
    Check(ReferenceEquals(cachedProfile, restoredSession.Session),
        "Un fallo de transporte al renovar conserva el perfil.");
}
restoredApi.FailTokenTransport = false;
Check(await restoredSession.GetValidAccessTokenAsync() == "test",
    "Tras recuperarse la conexion se puede renovar sin iniciar sesion otra vez.");
restoredApi.RejectTokens = true;
try
{
    await restoredSession.GetValidAccessTokenAsync();
    throw new Exception("Se esperaba el rechazo de la renovacion.");
}
catch (AuthenticationRequiredException)
{
    await CheckSessionClearedAsync(restoredSession,
        "Una renovacion rechazada elimina tokens y perfil juntos.");
}
await restoredClient.GetRequiredService<AuthService>().LogoutAsync();
await CheckSessionClearedAsync(restoredSession,
    "Logout elimina el token del handler.");
try
{
    await restoredSession.LoadAsync(new("test", null, DateTimeOffset.UtcNow.AddMinutes(-1)));
    await restoredSession.GetValidAccessTokenAsync();
    throw new Exception("Se esperaba el rechazo de un token caducado sin refresh token.");
}
catch (AuthenticationRequiredException ex)
{
    Check(ex.ResourceKey == TextKey.SessionExpired,
        "La caducidad sin refresh token devuelve el error de sesion expirada.");
    await CheckSessionClearedAsync(restoredSession,
        "La caducidad sin refresh token elimina la sesion.");
}
restoredApi.RejectTokens = false;
restoredApi.OmitRefreshToken = true;
restoredApi.TokenLifetimeSeconds = 1800;
var renewedToken = await restoredClient.GetRequiredService<IComunioTokenClient>().RefreshAsync("previous-refresh");
Check(renewedToken.RefreshToken == "previous-refresh"
    && renewedToken.ExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(29),
    "El cliente calcula la caducidad y conserva el refresh token si la API no lo devuelve.");
await CheckSessionClearedAsync(restoredSession,
    "Solicitar tokens no modifica ni almacena la sesion.");
var restartedServices = new ServiceCollection();
restartedServices.AddComunioCore();
using var restartedClient = restartedServices.BuildServiceProvider();
await CheckSessionClearedAsync(restartedClient.GetRequiredService<ISessionManager>(),
    "Un proceso nuevo exige login y no dispone de los tokens de otra instancia.");
Console.WriteLine("Comprobaciones de migracion correctas; sin peticiones reales a Comunio.");

sealed class FakeApi : HttpMessageHandler
{
    public string? LastStandingsQuery { get; private set; }
    public List<PostCashRequestModel> Payments { get; } = [];
    public int ProfileRequests { get; private set; }
    public int TokenRequests { get; private set; }
    public int PaymentAttempts { get; private set; }
    public int? FailAtPayment { get; set; }
    public bool FailProfile { get; set; }
    public bool RejectTokens { get; set; }
    public bool FailTokenTransport { get; set; }
    public bool ExpireNextToken { get; set; }
    public bool OmitRefreshToken { get; set; }
    public int TokenLifetimeSeconds { get; set; } = 1800;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        if (path != "/login")
            CheckBearer(request);
        object response;
        if (path == "/login")
        {
            if (request.Headers.Authorization is not null)
                throw new Exception("Las peticiones de tokens no deben incluir Bearer.");
            TokenRequests++;
            if (FailTokenTransport) throw new HttpRequestException("Simulated transport failure.");
            if (RejectTokens) return new(HttpStatusCode.Unauthorized);
            response = new { access_token = "test", refresh_token = OmitRefreshToken ? null : "refresh", expires_in = ExpireNextToken ? -60 : TokenLifetimeSeconds };
            ExpireNextToken = false;
        }
        else if (path == "/")
        {
            ProfileRequests++;
            if (FailProfile) return new(HttpStatusCode.ServiceUnavailable);
            response = new { user = new { id = "admin", name = "Admin", isAdmin = true }, community = new { id = "community" } };
        }
        else if (path.EndsWith("/standings"))
        {
            LastStandingsQuery = request.RequestUri.Query;
            response = new { items = new[] { ("A", "250"), ("B", "50"), ("C", "-3"), ("D", "invalid") }.Select(x => new { lastPoints = x.Item2, _embedded = new { user = new { id = x.Item1, name = x.Item1 } } }) };
        }
        else if (path.EndsWith("/squad"))
            response = new { items = Enumerable.Range(0, path.Contains("/A/") ? 26 : 25).Select(x => new { id = x }) };
        else if (path.EndsWith("/penalties"))
        {
            PaymentAttempts = FailAtPayment.HasValue ? PaymentAttempts + 1 : 0;
            if (PaymentAttempts == FailAtPayment) return new(HttpStatusCode.InternalServerError);
            Payments.Add(JsonConvert.DeserializeObject<PostCashRequestModel>(await request.Content!.ReadAsStringAsync(cancellationToken))!);
            response = new { };
        }
        else throw new Exception($"Peticion inesperada: {request.RequestUri}");
        return new(HttpStatusCode.OK) { Content = new StringContent(JsonConvert.SerializeObject(response), Encoding.UTF8, "application/json") };
    }

    private static void CheckBearer(HttpRequestMessage request)
    {
        if (request.Headers.Authorization is not { Scheme: "Bearer", Parameter: "test" })
            throw new Exception("La petición debe llevar el token vigente.");
    }
}

