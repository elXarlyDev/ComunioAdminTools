using ComunioAdminTools.Core.Authentication;
using ComunioAdminTools.Core.Localization;
using ComunioAdminTools.Core.Models;

namespace ComunioAdminTools.Core.Services;

public sealed class SquadService(IHttpClientFactory factory, ISessionManager sessionManager, AppLocalization text) : CommunityService(factory, sessionManager, text)
{
    public async Task<SquadExcessResult> GetSquadExcessAsync()
    {
        var matchdays = await GetAsync<List<MatchdayModel>>("matchdays");
        var started = matchdays.Where(matchday => matchday.Started)
            .OrderByDescending(matchday => matchday.Timestamp).ToArray();
        var matchday = started.FirstOrDefault(matchday => !matchday.Finished)
            ?? started.FirstOrDefault()
            ?? throw new LocalizedInvalidOperationException(text, TextKey.NoStartedMatchday);
        var timestamp = matchday.Timestamp;

        var standings = await GetStandingsAsync();
        var results = new List<ExcesoPlantillaModel>();
        foreach (var team in standings.Teams)
        {
            var squad = await GetAsync<SquadModel>($"users/{Uri.EscapeDataString(team.TeamInfo.User.Id)}/squad?eid=live");
            var count = squad.Players.Count(player => player.PurchaseInfo == null || player.PurchaseInfo.Date < timestamp);
            results.Add(new() { UserName = team.TeamInfo.User.Name, PlayerCount = count });
        }
        return new(matchday.MatchdayKey, results);
    }
}
