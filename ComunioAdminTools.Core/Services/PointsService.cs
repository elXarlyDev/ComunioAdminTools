using ComunioAdminTools.Core.Authentication;
using ComunioAdminTools.Core.Localization;
using ComunioAdminTools.Core.Models;

namespace ComunioAdminTools.Core.Services;

public interface IPointsService
{
    Task<IReadOnlyList<PuntosJornadaModel>> GetLastRoundPoints();
    Task<StandingsModel> GetMonthlyStandings(int year, int month);
    Task<StandingsModel> GetFirstHalfStandings();
    Task<StandingsModel> GetSecondHalfStandings();
    Task<StandingsModel> GetSeasonStandings();
}

public sealed class PointsService(IHttpClientFactory factory, ISessionManager sessionManager, AppLocalization text) : CommunityService(factory, sessionManager, text), IPointsService
{
    public async Task<IReadOnlyList<PuntosJornadaModel>> GetLastRoundPoints()
    {
        var standings = await GetStandings();

        return standings.Teams.Select(team => new PuntosJornadaModel
        {
            UserName = team.TeamInfo.User.Name,
            Points = int.TryParse(team.LastPoints, out var points) ? points : 0
        }).OrderByDescending(item => item.Points).ToList();
    }

    public Task<StandingsModel> GetMonthlyStandings(int year, int month)
    {
        _ = new DateOnly(year, month, 1);
        return GetPeriodStandings(FormattableString.Invariant($"period=month&year={year}&month={month}"));
    }

    public Task<StandingsModel> GetFirstHalfStandings()
        => GetPeriodStandings("period=firstHalf&wpe=true");

    public Task<StandingsModel> GetSecondHalfStandings()
        => GetPeriodStandings("period=secondHalf&wpe=true");

    public Task<StandingsModel> GetSeasonStandings()
        => GetPeriodStandings("period=total");

    private Task<StandingsModel> GetPeriodStandings(string query)
        => Get<StandingsModel>($"communities/{Uri.EscapeDataString(Session.Community.Id)}/standings?{query}");
}
