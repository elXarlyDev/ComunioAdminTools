using ComunioAdminTools.Core.Authentication;
using ComunioAdminTools.Core.Localization;
using ComunioAdminTools.Core.Models;

namespace ComunioAdminTools.Core.Services;

public interface IPointsService
{
    Task<IReadOnlyList<PuntosJornadaModel>> GetLastRoundPointsAsync();
    Task<StandingsModel> GetMonthlyStandingsAsync(int year, int month);
    Task<StandingsModel> GetFirstHalfStandingsAsync();
    Task<StandingsModel> GetSecondHalfStandingsAsync();
    Task<StandingsModel> GetSeasonStandingsAsync();
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

    public Task<StandingsModel> GetMonthlyStandingsAsync(int year, int month)
    {
        _ = new DateOnly(year, month, 1);
        return GetPeriodStandingsAsync(FormattableString.Invariant($"period=month&year={year}&month={month}"));
    }

    public Task<StandingsModel> GetFirstHalfStandingsAsync()
        => GetPeriodStandingsAsync("period=firstHalf&wpe=true");

    public Task<StandingsModel> GetSecondHalfStandingsAsync()
        => GetPeriodStandingsAsync("period=secondHalf&wpe=true");

    public Task<StandingsModel> GetSeasonStandingsAsync()
        => GetPeriodStandingsAsync("period=total");

    private Task<StandingsModel> GetPeriodStandingsAsync(string query)
        => GetAsync<StandingsModel>($"communities/{Uri.EscapeDataString(Session.Community.Id)}/standings?{query}");
}
