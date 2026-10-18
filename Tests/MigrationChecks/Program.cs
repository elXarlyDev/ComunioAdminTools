using System.Net;
using System.Text;
using ComunioAdminTools.Core;
using ComunioAdminTools.Core.Authentication;
using ComunioAdminTools.Core.Models;
using ComunioAdminTools.Core.Services;
using ComunioAdminTools.UI;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

static void Check(bool value, string message)
{
    if (!value) throw new Exception(message);
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
Check(!await provider.GetRequiredService<ISessionManager>().HasSessionAsync(), "Logout elimina la sesion.");
Check(provider.GetRequiredService<ISessionManager>().Session is null, "Logout borra el perfil en memoria.");
api.FailProfile = true;
try
{
    await auth.LoginAsync("user", "password");
    throw new Exception("Se esperaba un fallo al cargar el perfil.");
}
catch (AuthenticationRequiredException)
{
    Check(!await provider.GetRequiredService<ISessionManager>().HasSessionAsync(), "Un login fallido limpia la sesion en memoria.");
    Check(await provider.GetRequiredService<ITokenStore>().GetTokenAsync() is null,
        "Un login fallido elimina los tokens guardados.");
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
await restoredClient.GetRequiredService<ISessionManager>().StartAsync(new("test", "refresh", DateTimeOffset.UtcNow.AddMinutes(-1)));
using var restoredHttp = restoredClient.GetRequiredService<IHttpClientFactory>().CreateClient("Comunio");
var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => restoredHttp.GetAsync("")));
Check(responses.All(x => x.IsSuccessStatusCode) && restoredApi.TokenRequests == 1,
    "El pipeline renueva una sola vez antes de enviar las peticiones concurrentes.");
foreach (var response in responses) response.Dispose();
Check(await restoredClient.GetRequiredService<ITokenStore>().GetTokenAsync() == "test", "El handler dispone del token renovado como string.");
await restoredClient.GetRequiredService<ISessionManager>().LogoutAsync();
Check(await restoredClient.GetRequiredService<ITokenStore>().GetTokenAsync() is null,
    "Logout elimina el token del handler.");
var leftoverToken = new MemoryTokenStore();
await leftoverToken.SetTokenAsync("old-access-token");
var restartedServices = new ServiceCollection();
restartedServices.AddSingleton<ITokenStore>(leftoverToken);
restartedServices.AddComunioCore();
using var restartedClient = restartedServices.BuildServiceProvider();
Check(!await restartedClient.GetRequiredService<ISessionManager>().HasSessionAsync(),
    "Un proceso nuevo exige login aunque el almacén conserve un access token.");
var dialog = new ResultsDialogController();
var closed = 0;
dialog.CloseRequested += () => closed++;
Check(!dialog.TryClose(), "Atras sin dialogo no se consume.");
dialog.Open();
Check(dialog.TryClose() && !dialog.IsOpen && closed == 1 && !dialog.TryClose(), "Atras cierra resultados una sola vez.");
Console.WriteLine("Comprobaciones de migracion correctas; sin peticiones reales a Comunio.");

sealed class FakeApi : HttpMessageHandler
{
    public List<PostCashRequestModel> Payments { get; } = [];
    public int ProfileRequests { get; private set; }
    public int TokenRequests { get; private set; }
    public int PaymentAttempts { get; private set; }
    public int? FailAtPayment { get; set; }
    public bool FailProfile { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        if (path != "/login")
            CheckBearer(request);
        object response;
        if (path == "/login")
        {
            TokenRequests++;
            response = new { access_token = "test", refresh_token = "refresh", expires_in = 1800 };
        }
        else if (path == "/")
        {
            ProfileRequests++;
            if (FailProfile) return new(HttpStatusCode.ServiceUnavailable);
            response = new { user = new { id = "admin", name = "Admin", isAdmin = true }, community = new { id = "community" } };
        }
        else if (path.EndsWith("/standings"))
            response = new { items = new[] { ("A", "250"), ("B", "50"), ("C", "-3"), ("D", "invalid") }.Select(x => new { lastPoints = x.Item2, _embedded = new { user = new { id = x.Item1, name = x.Item1 } } }) };
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

