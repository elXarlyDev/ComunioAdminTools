using ComunioAdminTools.Core.Authentication;
using ComunioAdminTools.Core.Models;
using ComunioAdminTools.Core.Localization;

namespace ComunioAdminTools.Core.Services;

public interface IPointsService
{
    public Task<IReadOnlyList<PuntosJornadaModel>> GetLastRoundPointsAsync();
}

public sealed class PointsService(IHttpClientFactory factory, ISessionManager sessionManager, AppLocalization text) : CommunityService(factory, sessionManager, text), IPointsService
{
    public async Task<IReadOnlyList<PuntosJornadaModel>> GetLastRoundPointsAsync()
    {
        var standings = await GetStandingsAsync();
        return standings.Teams.Select(team => new PuntosJornadaModel
        {
            UserName = team.TeamInfo.User.Name,
            Points = int.TryParse(team.LastPoints, out var points) ? points : 0
        }).OrderByDescending(item => item.Points).ToList();
    }
}
