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

var api = new SquadApi();
var services = new ServiceCollection();
services.AddComunioCore();
services.AddHttpClient("ComunioAuth").ConfigurePrimaryHttpMessageHandler(() => api);
services.AddHttpClient("Comunio").ConfigurePrimaryHttpMessageHandler(() => api);
using var provider = services.BuildServiceProvider();
await provider.GetRequiredService<ISessionManager>().LoadAsync(new("test", "refresh", DateTimeOffset.UtcNow.AddHours(1)));
var squads = provider.GetRequiredService<SquadService>();

api.Matchdays = """
[
  {"matchdayKey":"12","timestamp":"2026-11-06T10:00:00+02:00","started":false,"finished":false},
  {"matchdayKey":"10","timestamp":"2026-10-18T21:02:01+02:00","started":true,"finished":true},
  {"matchdayKey":"8","timestamp":"2026-10-09T21:02:01+02:00","started":true,"finished":true},
  {"matchdayKey":"9","timestamp":"2026-10-16T21:02:01+02:00","started":true,"finished":false}
]
""";
var result = await squads.GetSquadExcessAsync();
Check(result.MatchdayKey == "9", "La respuesta incluye el matchdayKey de la jornada activa seleccionada.");
Check(result.Squads.Count == 2 && result.Squads[0].UserName == "Alpha" && result.Squads[0].PlayerCount == 5,
    "La jornada activa tiene prioridad aunque exista otra finalizada con timestamp mas reciente.");
Check(result.Squads[1].UserName == "Beta" && result.Squads[1].PlayerCount == 0,
    "Los usuarios sin jugadores anteriores al corte tambien aparecen con cero.");
Check(api.MatchdayRequests == 1 && api.SquadRequests == 2,
    "Se consulta /matchdays una sola vez para el recuento de todas las plantillas.");

api.Matchdays = """
[
  {"matchdayKey":"8","timestamp":"2026-10-09T21:02:01+02:00","started":true,"finished":true},
  {"matchdayKey":"12","timestamp":"2026-11-06T10:00:00+02:00","started":false,"finished":false},
  {"matchdayKey":"9","timestamp":"2026-10-16T21:02:01+02:00","started":true,"finished":true}
]
""";
result = await squads.GetSquadExcessAsync();
Check(result.MatchdayKey == "9", "El matchdayKey de fallback pertenece a la jornada iniciada mas reciente.");
Check(result.Squads[0].PlayerCount == 5, "Sin jornada activa, se elige el timestamp mas reciente de las iniciadas, sin depender del orden del JSON.");

api.Matchdays = """
[
  {"matchdayKey":"8","timestamp":"2026-10-09T21:02:01+02:00","started":true,"finished":false},
  {"matchdayKey":"9","timestamp":"2026-10-16T19:02:01+00:00","started":true,"finished":false}
]
""";
result = await squads.GetSquadExcessAsync();
Check(result.Squads[0].PlayerCount == 5, "Si hay varias jornadas activas se elige la mas reciente; los offsets representan instantes equivalentes.");

api.EmptyCommunity = true;
result = await squads.GetSquadExcessAsync();
Check(result.Squads.Count == 0 && result.MatchdayKey == "9", "La jornada se conserva en la respuesta aunque no haya usuarios.");
api.EmptyCommunity = false;

var text = provider.GetRequiredService<AppLocalization>();
foreach (var language in new[] { AppLocalization.SpanishCulture, AppLocalization.ValencianCulture })
{
    text.SetCulture(language);
    foreach (var response in new[] { "[]", """[{"matchdayKey":"12","timestamp":"2026-11-06T10:00:00+02:00","started":false,"finished":false}]""" })
    {
        api.Matchdays = response;
        var previousSquadRequests = api.SquadRequests;
        try
        {
            await squads.GetSquadExcessAsync();
            throw new Exception("Se esperaba un error por no haber jornadas iniciadas.");
        }
        catch (LocalizedInvalidOperationException ex)
        {
            Check(ex.ResourceKey == TextKey.NoStartedMatchday && ex.Message == text[TextKey.NoStartedMatchday]
                && api.SquadRequests == previousSquadRequests,
                "Sin jornada iniciada hay un error traducido y no se consultan plantillas ni se muestran ceros falsos.");
        }
    }
}

api.Matchdays = """[{"started":true,"finished":false}]""";
try
{
    await squads.GetSquadExcessAsync();
    throw new Exception("Se esperaba un error por jornada sin timestamp.");
}
catch (LocalizedInvalidOperationException ex)
{
    Check(ex.ResourceKey == TextKey.InvalidJsonResponse, "Un timestamp ausente no se interpreta como una fecha de corte valida.");
}
api.FailMatchdays = true;
try
{
    await squads.GetSquadExcessAsync();
    throw new Exception("Se esperaba un error HTTP de matchdays.");
}
catch (LocalizedHttpRequestException ex)
{
    Check(ex.ResourceKey == TextKey.ServiceUnavailable, "Los fallos HTTP de /matchdays conservan su error traducido.");
}
Console.WriteLine("Squad checks passed: matchday selection, strict purchase cutoff, offsets and missing dates; simulated API only.");

sealed class SquadApi : HttpMessageHandler
{
    public string Matchdays { get; set; } = "[]";
    public int MatchdayRequests { get; private set; }
    public int SquadRequests { get; private set; }
    public bool FailMatchdays { get; set; }
    public bool EmptyCommunity { get; set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Headers.Authorization is not { Scheme: "Bearer", Parameter: "test" })
            throw new Exception("La consulta requiere el token de sesion.");
        var path = request.RequestUri!.AbsolutePath;
        var status = HttpStatusCode.OK;
        string body;
        if (path == "/")
            body = """{"user":{"id":"admin","name":"Admin","isAdmin":true},"community":{"id":"community"}}""";
        else if (path == "/matchdays")
        {
            MatchdayRequests++;
            status = FailMatchdays ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK;
            body = Matchdays;
        }
        else if (path == "/communities/community/standings")
            body = EmptyCommunity ? "{\"items\":[]}"
                : """{"items":[{"_embedded":{"user":{"id":"alpha","name":"Alpha"}}},{"_embedded":{"user":{"id":"beta","name":"Beta"}}}]}""";
        else if (path.EndsWith("/squad"))
        {
            SquadRequests++;
            body = path.Contains("/alpha/") ? """
            {"items":[
              {"id":"before","purchaseInfo":{"date":"2026-10-16T21:02:00+02:00"}},
              {"id":"equal","purchaseInfo":{"date":"2026-10-16T21:02:01+02:00"}},
              {"id":"after","purchaseInfo":{"date":"2026-10-16T21:02:02+02:00"}},
              {"id":"older","purchaseInfo":{"date":"2026-10-09T21:02:01+02:00"}},
              {"id":"future","purchaseInfo":{"date":"2026-10-20T00:00:00+02:00"}},
              {"id":"equal-utc","purchaseInfo":{"date":"2026-10-16T19:02:01+00:00"}},
              {"id":"before-utc","purchaseInfo":{"date":"2026-10-16T19:02:00+00:00"}},
              {"id":"after-utc","purchaseInfo":{"date":"2026-10-16T21:02:00+00:00"}},
              {"id":"missing-info"},
              {"id":"null-info","purchaseInfo":null},
              {"id":"missing-date","purchaseInfo":{}},
              {"id":"null-date","purchaseInfo":{"date":null}}
            ]}
            """ : """{"items":[{"id":"new","purchaseInfo":{"date":"2026-12-01T00:00:00+02:00"}}]}""";
        }
        else throw new Exception($"Consulta inesperada: {path}");
        return Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        });
    }
}
