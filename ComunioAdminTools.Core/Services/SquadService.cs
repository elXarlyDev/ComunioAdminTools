using System.Globalization;

using ComunioAdminTools.Core.Authentication;
using ComunioAdminTools.Core.Localization;
using ComunioAdminTools.Core.Models;

namespace ComunioAdminTools.Core.Services;

public interface ISquadService
{
    Task<SquadExcessResult> GetSquadExcess();
    Task<IReadOnlyList<PlayerDetailsModel>> GetSquadStatus();
    Task<LineupResult> GetLineup();
    Task SaveLineup(LineupFormation formation, IReadOnlyList<PlayerDetailsModel?> starters);
}

public sealed class SquadService(IHttpClientFactory factory, ISessionManager sessionManager, AppLocalization text)
    : CommunityService(factory, sessionManager, text), ISquadService
{
    public async Task<SquadExcessResult> GetSquadExcess()
    {
        var matchdays = await Get<List<MatchdayModel>>("matchdays");

        var started = matchdays.Where(matchday => matchday.Started)
            .OrderByDescending(matchday => matchday.Timestamp).ToArray();

        var matchday = started.FirstOrDefault(matchday => !matchday.Finished)
            ?? started.FirstOrDefault()
            ?? throw new LocalizedInvalidOperationException(text, TextKey.NoStartedMatchday);

        var timestamp = matchday.Timestamp;

        var standings = await GetStandings();
        var squadExcess = await CalculateSquadExcess(standings, timestamp);

        return new(matchday.MatchdayKey, squadExcess);
    }

    public async Task<IReadOnlyList<PlayerDetailsModel>> GetSquadStatus()
    {
        var userId = Uri.EscapeDataString(Session.User.Id);
        var communityUri = GetCommunityUriPart();
        var squad = await Get<SquadModel>($"users/{userId}/squad?eid=live");
        var players = new List<PlayerDetailsModel>();

        foreach (var player in squad.Players)
        {
            var details = await Get<PlayerDetailsModel>($"{communityUri}/users/{userId}/players/{Uri.EscapeDataString(player.Id)}");
            details.Id = player.Id;
            players.Add(details);
        }

        return players.OrderBy(player => (int?)player.Position ?? int.MaxValue)
            .ThenBy(player => player.Name, StringComparer.Create(text.Culture, ignoreCase: true))
            .ToArray();
    }

    public async Task<LineupResult> GetLineup()
    {
        var userId = Uri.EscapeDataString(Session.User.Id);
        var response = await Get<LineupModel>($"{GetCommunityUriPart()}/users/{userId}/lineup");

        if (!Enum.IsDefined(response.Tactic))
        {
            throw new LocalizedInvalidOperationException(text, TextKey.InvalidJsonResponse);
        }

        var starters = new PlayerDetailsModel?[11];
        var playerIds = new HashSet<string>();

        foreach (var player in response.Items.Lineup.Values)
        {
            if (player is null)
            {
                continue;
            }

            if (player.LineupPosition is not int position || position is < 1 or > 11
                || string.IsNullOrWhiteSpace(player.Id) || !playerIds.Add(player.Id)
                || starters[11 - position] is not null)
            {
                throw new LocalizedInvalidOperationException(text, TextKey.InvalidJsonResponse);
            }

            starters[11 - position] = player;
        }

        return new(response.Tactic, starters);
    }

    public async Task SaveLineup(LineupFormation formation, IReadOnlyList<PlayerDetailsModel?> starters)
    {
        var session = Session;

        if (!long.TryParse(session.User.Id, NumberStyles.None, CultureInfo.InvariantCulture, out var userId) || userId <= 0)
        {
            throw new LocalizedInvalidOperationException(text, TextKey.InvalidJsonResponse);
        }

        if (!Enum.IsDefined(formation) || starters.Count != 11)
        {
            throw new LocalizedInvalidOperationException(text, TextKey.InvalidLineup);
        }

        var lineup = new Dictionary<string, string>();
        var playerIds = new HashSet<string>();

        for (var index = 0; index < starters.Count; index++)
        {
            var player = starters[index];

            if (player is not null && (string.IsNullOrWhiteSpace(player.Id) || !playerIds.Add(player.Id)
                || player.Position != formation.GetSlotPosition(index)))
            {
                throw new LocalizedInvalidOperationException(text, TextKey.InvalidLineup);
            }

            lineup.Add((11 - index).ToString(CultureInfo.InvariantCulture), player?.Id ?? "");
        }

        var body = new
        {
            userId,
            tactic = ((int)formation).ToString(CultureInfo.InvariantCulture),
            lineup,
            substitutes = new { striker = "", midfielder = "", defender = "", keeper = "" },
            type = "default"
        };

        await PutWithoutResponse($"communities/{Uri.EscapeDataString(session.Community.Id)}/users/{Uri.EscapeDataString(session.User.Id)}/lineup", body);
    }

    private async Task<IReadOnlyList<ExcesoPlantillaModel>> CalculateSquadExcess(StandingsModel standings, DateTimeOffset timestamp)
    {
        var results = new List<ExcesoPlantillaModel>();

        foreach (var team in standings.Teams)
        {
            var squad = await Get<SquadModel>($"users/{Uri.EscapeDataString(team.TeamInfo.User.Id)}/squad?eid=live");
            var count = squad.Players.Count(player => player.PurchaseInfo == null || player.PurchaseInfo.Date < timestamp);
            results.Add(new() { UserName = team.TeamInfo.User.Name, PlayerCount = count });
        }

        return results;
    }
}
