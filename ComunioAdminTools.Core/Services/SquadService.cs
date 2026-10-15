using ComunioAdminTools.Core.Models;
using ComunioAdminTools.Core.Localization;

namespace ComunioAdminTools.Core.Services;

using ComunioAdminTools.Core.Authentication;

public sealed class SquadService(IHttpClientFactory factory, ISessionManager sessionManager, AppLocalization text) : CommunityService(factory, sessionManager, text)
{
    public async Task<IReadOnlyList<ExcesoPlantillaModel>> GetSquadExcessAsync()
    {
        var standings = await GetStandingsAsync();
        var results = new List<ExcesoPlantillaModel>();
        foreach (var team in standings.Teams)
        {
            var squad = await GetAsync<SquadModel>($"users/{Uri.EscapeDataString(team.TeamInfo.User.Id)}/squad?eid=live");
            var count = squad.Players.Count();
            if (count > 25)
                results.Add(new() { UserName = team.TeamInfo.User.Name, PlayerCount = count });
        }
        return results;
    }
}
