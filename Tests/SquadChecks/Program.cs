using System.Net;
using System.Text;

using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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

var api = new SquadApi();
var services = new ServiceCollection();
services.AddComunioCore();
services.AddHttpClient("ComunioAuth").ConfigurePrimaryHttpMessageHandler(() => api);
services.AddHttpClient("Comunio").ConfigurePrimaryHttpMessageHandler(() => api);
using var provider = services.BuildServiceProvider();
await provider.GetRequiredService<ISessionManager>().Load(new("test", "refresh", DateTimeOffset.UtcNow.AddHours(1)));
var squads = provider.GetRequiredService<ISquadService>();

var statusPlayers = await squads.GetSquadStatus();

Check(statusPlayers.Count == 2 && statusPlayers[0].Name == "Gerard Moreno"
    && statusPlayers[0].Club?.Name == "Villarreal" && statusPlayers[0].Position == PlayerPosition.Striker
    && statusPlayers[0].Status == PlayerStatus.Injured && statusPlayers[0].StatusInfo == "Lesión muscular",
    "El detalle conserva nombre, club, type, status y statusInfo del ejemplo de la API.");

Check(statusPlayers[1].Name == "Otro jugador" && statusPlayers[1].Club is null
    && statusPlayers[1].Position is null && statusPlayers[1].StatusInfo is null,
    "Los campos opcionales nulos o ausentes no impiden consultar el resto de jugadores.");

foreach (var (apiValue, expected) in new[]
    {
        ("keeper", PlayerPosition.Keeper), ("defender", PlayerPosition.Defender),
        ("midfielder", PlayerPosition.Midfielder), ("striker", PlayerPosition.Striker)
    })
{
    var player = JsonConvert.DeserializeObject<PlayerDetailsModel>("{\"type\":\"" + apiValue + "\"}");
    Check(player?.Position == expected, "Cada posición de Comunio se deserializa al enumerado correspondiente.");
}

Check(api.OwnSquadRequests == 1 && api.DetailRequests.SequenceEqual(new[]
    {
        "/communities/community/users/77/players/1927",
        "/communities/community/users/77/players/special%20id"
    }), "La plantilla es la del usuario con sesión y cada detalle usa su comunidad, usuario e id escapado.");

var lineupResult = await squads.GetLineup();
var starters = lineupResult.Starters;
Check(lineupResult.Formation == LineupFormation.ThreeFiveTwo, "La formación inicial se conserva desde tactic.");

foreach (var (apiValue, expected) in new[]
    {
        ("ACTIVE", PlayerStatus.Active), ("AWAY", PlayerStatus.Away),
        ("DECEASED", PlayerStatus.Deceased), ("GAME_BREAK", PlayerStatus.GameBreak),
        ("INJURED", PlayerStatus.Injured), ("MISCELLANEOUS", PlayerStatus.Miscellaneous),
        ("RED_BANNED", PlayerStatus.RedBanned), ("REHABILITATION", PlayerStatus.Rehabilitation),
        ("RETIRED", PlayerStatus.Retired), ("SUSPENDED", PlayerStatus.Suspended),
        ("WEAKENED", PlayerStatus.Weakened), ("YELLOW_BANNED", PlayerStatus.YellowBanned),
        ("YELLOW_RED_BANNED", PlayerStatus.YellowRedBanned)
    })
{
    var player = JsonConvert.DeserializeObject<PlayerDetailsModel>("{\"status\":\"" + apiValue + "\"}");
    Check(player?.Status == expected, "Cada estado documentado por Comunio se deserializa al enumerado correspondiente.");
}

Check(starters.Count == 11 && starters[0]?.Id == "200" && starters[0]?.LineupPosition == 11
    && starters[3]?.Id == "201" && starters[10]?.Id == "1927" && starters.Count(player => player is null) == 8,
    "La alineación contiene once huecos fijos y usa position descendente, independientemente del orden del JSON.");

Check(starters[10]?.Status == PlayerStatus.Injured && starters[10]?.StatusInfo == "Lesión muscular",
    "Los titulares conservan el estado obtenido del endpoint de alineación.");

var initialLineup = api.Lineup;
await squads.SaveLineup(LineupFormation.ThreeFiveTwo, starters);
var savedBody = api.SavedLineup!;

Check(api.SaveRequests == 1 && savedBody.Properties().Select(property => property.Name).Order().SequenceEqual(new[] { "lineup", "substitutes", "tactic", "type", "userId" })
    && savedBody["userId"]!.Type == JTokenType.Integer && savedBody.Value<long>("userId") == 77
    && savedBody.Value<string>("tactic") == "352" && savedBody.Value<string>("type") == "default",
    "El PUT contiene userId de sesión numérico, tactic, lineup, substitutes y type default.");

var savedSubstitutes = (JObject)savedBody["substitutes"]!;

Check(savedSubstitutes.Properties().Select(property => property.Name).Order().SequenceEqual(new[] { "defender", "keeper", "midfielder", "striker" })
    && savedSubstitutes.Properties().All(property => property.Value.Type == JTokenType.String && property.Value.Value<string>() == ""),
    "Las cuatro posiciones de substitutes se envían siempre como cadenas vacías.");

var savedPlayers = (JObject)savedBody["lineup"]!;

Check(savedPlayers.Count == 11 && savedPlayers.Value<string>("11") == "200" && savedPlayers.Value<string>("8") == "201"
    && savedPlayers.Value<string>("1") == "1927" && savedPlayers.Value<string>("10") == ""
    && savedPlayers.Properties().All(property => property.Value.Type == JTokenType.String),
    "Las claves 1 a 11 mantienen la posición real y los huecos vacíos se envían como cadenas vacías.");

var repeated = starters.ToArray();
repeated[1] = repeated[3];
var misplaced = starters.ToArray();
misplaced[0] = new PlayerDetailsModel { Id = "other", Position = PlayerPosition.Defender };
var beforeInvalidSaves = api.SaveRequests;

foreach (var invalid in new[] { repeated, misplaced, starters.Take(10).ToArray() })
{
    try
    {
        await squads.SaveLineup(LineupFormation.ThreeFiveTwo, invalid);
        throw new Exception("Se esperaba una alineación inválida antes del PUT.");
    }
    catch (LocalizedInvalidOperationException ex)
    {
        Check(ex.ResourceKey == TextKey.InvalidLineup && api.SaveRequests == beforeInvalidSaves,
            "La validación impide enviar duplicados, jugadores incompatibles o un número de huecos incorrecto.");
    }
}

api.SaveStatus = HttpStatusCode.ServiceUnavailable;

try
{
    await squads.SaveLineup(LineupFormation.ThreeFiveTwo, starters);
    throw new Exception("Se esperaba un fallo del PUT.");
}
catch (LocalizedHttpRequestException ex)
{
    Check(ex.ResourceKey == TextKey.ServiceUnavailable && api.SaveRequests == beforeInvalidSaves + 1,
        "Un fallo del PUT conserva su error localizado y no se reintenta automáticamente.");
}

api.SaveStatus = HttpStatusCode.NoContent;
api.Lineup = """{"tactic":"352","items":{"lineup":{}}}""";
Check((await squads.GetLineup()).Starters.All(player => player is null), "Una alineación vacía mantiene once huecos.");

foreach (var (tactic, expected) in new[]
    {
        ("442", LineupFormation.FourFourTwo), ("343", LineupFormation.ThreeFourThree),
        ("352", LineupFormation.ThreeFiveTwo), ("433", LineupFormation.FourThreeThree),
        ("451", LineupFormation.FourFiveOne)
    })
{
    api.Lineup = "{\"tactic\":\"" + tactic + "\",\"items\":{\"lineup\":{}}}";
    Check((await squads.GetLineup()).Formation == expected, "Las cinco formaciones de la API se reconocen correctamente.");
}

foreach (var invalid in new[]
    {
        """{"tactic":"999","items":{"lineup":{}}}""",
        """{"items":{"lineup":{}}}""",
        """{"tactic":"352","items":{"lineup":{"1":{"id":1,"position":12}}}}""",
        """{"tactic":"352","items":{"lineup":{"1":{"id":1,"position":1},"2":{"id":2,"position":1}}}}""",
        """{"tactic":"352","items":{"lineup":{"1":{"id":1,"position":1},"2":{"id":1,"position":2}}}}"""
    })
{
    api.Lineup = invalid;

    try
    {
        await squads.GetLineup();
        throw new Exception("Se esperaba una alineación inválida.");
    }
    catch (LocalizedInvalidOperationException ex)
    {
        Check(ex.ResourceKey == TextKey.InvalidJsonResponse, "Se rechazan posiciones inválidas, huecos duplicados y jugadores repetidos.");
    }
}

api.Lineup = initialLineup;
api.OwnSquad = """{"items":[]}""";
var previousDetails = api.DetailRequests.Count;

Check((await squads.GetSquadStatus()).Count == 0 && api.DetailRequests.Count == previousDetails,
    "Una plantilla vacía no genera consultas de detalle.");

api.OwnSquad = """{"items":[{"id":"1927"},{"id":"special id"}]}""";
api.FailDetails = true;

try
{
    await squads.GetSquadStatus();
    throw new Exception("Se esperaba un error de detalle.");
}
catch (LocalizedHttpRequestException ex)
{
    Check(ex.ResourceKey == TextKey.ServiceUnavailable, "Un fallo de detalle no devuelve una plantilla parcialmente completa.");
}

api.FailDetails = false;

api.Matchdays = """
[
  {"matchdayKey":"12","timestamp":"2026-11-06T10:00:00+02:00","started":false,"finished":false},
  {"matchdayKey":"10","timestamp":"2026-10-18T21:02:01+02:00","started":true,"finished":true},
  {"matchdayKey":"8","timestamp":"2026-10-09T21:02:01+02:00","started":true,"finished":true},
  {"matchdayKey":"9","timestamp":"2026-10-16T21:02:01+02:00","started":true,"finished":false}
]
""";

var result = await squads.GetSquadExcess();
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

result = await squads.GetSquadExcess();
Check(result.MatchdayKey == "9", "El matchdayKey de fallback pertenece a la jornada iniciada mas reciente.");
Check(result.Squads[0].PlayerCount == 5, "Sin jornada activa, se elige el timestamp mas reciente de las iniciadas, sin depender del orden del JSON.");

api.Matchdays = """
[
  {"matchdayKey":"8","timestamp":"2026-10-09T21:02:01+02:00","started":true,"finished":false},
  {"matchdayKey":"9","timestamp":"2026-10-16T19:02:01+00:00","started":true,"finished":false}
]
""";

result = await squads.GetSquadExcess();
Check(result.Squads[0].PlayerCount == 5, "Si hay varias jornadas activas se elige la mas reciente; los offsets representan instantes equivalentes.");

api.EmptyCommunity = true;
result = await squads.GetSquadExcess();
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
            await squads.GetSquadExcess();
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
    await squads.GetSquadExcess();
    throw new Exception("Se esperaba un error por jornada sin timestamp.");
}
catch (LocalizedInvalidOperationException ex)
{
    Check(ex.ResourceKey == TextKey.InvalidJsonResponse, "Un timestamp ausente no se interpreta como una fecha de corte valida.");
}

api.FailMatchdays = true;

try
{
    await squads.GetSquadExcess();
    throw new Exception("Se esperaba un error HTTP de matchdays.");
}
catch (LocalizedHttpRequestException ex)
{
    Check(ex.ResourceKey == TextKey.ServiceUnavailable, "Los fallos HTTP de /matchdays conservan su error traducido.");
}

await provider.GetRequiredService<ISessionManager>().Clear();
var previousOwnSquadRequests = api.OwnSquadRequests;

try
{
    await squads.GetSquadStatus();
    throw new Exception("Se esperaba una sesión obligatoria.");
}
catch (AuthenticationRequiredException)
{
    Check(api.OwnSquadRequests == previousOwnSquadRequests, "Sin sesión no se consulta la plantilla de ningún usuario.");
}

Console.WriteLine("Squad checks passed: save payload and validation, own squad, formations, authentication and matchday cutoff; simulated API only.");

sealed class SquadApi : HttpMessageHandler
{
    public string Matchdays { get; set; } = "[]";
    public int MatchdayRequests { get; private set; }
    public int SquadRequests { get; private set; }
    public bool FailMatchdays { get; set; }
    public bool EmptyCommunity { get; set; }
    public string OwnSquad { get; set; } = """{"items":[{"id":"1927"},{"id":"special id"}]}""";
    public int OwnSquadRequests { get; private set; }
    public List<string> DetailRequests { get; } = [];
    public bool FailDetails { get; set; }
    public JObject? SavedLineup { get; private set; }
    public int SaveRequests { get; private set; }
    public HttpStatusCode SaveStatus { get; set; } = HttpStatusCode.NoContent;

    public string Lineup { get; set; } = """
        {"tactic":"352","items":{"lineup":{
            "1":{"id":1927,"name":"Gerard Moreno","type":"striker","position":1,"status":"INJURED","statusInfo":"Lesión muscular"},
            "8":{"id":201,"name":"Defensa","type":"defender","position":8,"status":"ACTIVE"},
            "11":{"id":200,"name":"Portero","type":"keeper","position":11,"status":"ACTIVE"}
        }}}
        """;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Headers.Authorization is not { Scheme: "Bearer", Parameter: "test" })
        {
            throw new Exception("La consulta requiere el token de sesion.");
        }

        var path = request.RequestUri!.AbsolutePath;
        var status = HttpStatusCode.OK;
        string body;

        if (path == "/")
            body = """{"user":{"id":"77","name":"Admin","isAdmin":true},"community":{"id":"community"}}""";
        else if (path == "/matchdays")
        {
            MatchdayRequests++;
            status = FailMatchdays ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK;
            body = Matchdays;
        }
        else if (path == "/communities/community/standings")
            body = EmptyCommunity ? "{\"items\":[]}"
                : """{"items":[{"_embedded":{"user":{"id":"alpha","name":"Alpha"}}},{"_embedded":{"user":{"id":"beta","name":"Beta"}}}]}""";
        else if (path == "/communities/community/users/77/lineup")
        {
            if (request.Method == HttpMethod.Put)
            {
                SaveRequests++;

                if (request.Content?.Headers.ContentType?.MediaType != "application/json")
                {
                    throw new Exception("El PUT requiere un body JSON.");
                }

                SavedLineup = JObject.Parse(await request.Content.ReadAsStringAsync(cancellationToken));
                return new HttpResponseMessage(SaveStatus);
            }

            if (request.Method != HttpMethod.Get)
            {
                throw new Exception("La alineación se obtiene mediante GET.");
            }

            body = Lineup;
        }
        else if (path == "/users/77/squad")
        {
            CheckOwnSquadRequest(request);
            OwnSquadRequests++;
            body = OwnSquad;
        }
        else if (path.StartsWith("/communities/community/users/77/players/", StringComparison.Ordinal))
        {
            DetailRequests.Add(path);
            status = FailDetails && !path.EndsWith("/1927", StringComparison.Ordinal) ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK;

            body = path.EndsWith("/1927", StringComparison.Ordinal)
                ? """{"playerId":1927,"name":"Gerard Moreno","club":{"id":19,"name":"Villarreal"},"type":"striker","status":"INJURED","statusInfo":"Lesión muscular","totalPoints":"53"}"""
                : """{"name":"Otro jugador","club":null,"type":null,"status":"ACTIVE","statusInfo":null}""";
        }
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

        return new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
    }

    private static void CheckOwnSquadRequest(HttpRequestMessage request)
    {
        if (request.Method != HttpMethod.Get || request.RequestUri!.Query != "?eid=live")
        {
            throw new Exception("La plantilla usa GET y eid=live.");
        }
    }
}
