using ComunioAdminTools.Core.Models;

namespace ComunioAdminTools.Core.Services;

using ComunioAdminTools.Core.Authentication;

public interface ICashService
{
    Task<IReadOnlyList<DineroJornadaModel>> PayRoundPointsAsync();
    Task<IReadOnlyList<DineroJornadaModel>> RevertRoundPointsAsync();
    Task<IReadOnlyList<DineroJornadaModel>> PayInternationalBreakAsync();
}

public sealed class CashService(IHttpClientFactory factory, ISessionManager sessionManager) : CommunityService(factory, sessionManager), ICashService
{
    public Task<IReadOnlyList<DineroJornadaModel>> PayRoundPointsAsync() => ManageRoundPointsAsync(false);
    public Task<IReadOnlyList<DineroJornadaModel>> RevertRoundPointsAsync() => ManageRoundPointsAsync(true);
    public async Task<IReadOnlyList<DineroJornadaModel>> PayInternationalBreakAsync()
    {
        var session = await GetSessionAsync();
        var standings = await GetStandingsAsync();
        var results = new List<DineroJornadaModel>();
        foreach (var team in standings.Teams)
        {
            try
            {
                await PostCashAsync(team.TeamInfo.User.Id, 5_000_000, "PARÓ SELECCIONS");
            }
            catch (Exception ex)
            {
                throw new CashOperationException(team.TeamInfo.User.Name, results.ToArray(), ex);
            }
            results.Add(new() { UserName = team.TeamInfo.User.Name, Amount = 5_000_000 });
        }
        return results.OrderByDescending(item => item.Amount).ToList();
    }

    private async Task<IReadOnlyList<DineroJornadaModel>> ManageRoundPointsAsync(bool revert)
    {
        var standings = await GetStandingsAsync();
        var results = new List<DineroJornadaModel>();
        foreach (var team in standings.Teams)
        {
            var total = 0;
            try
            {
                if (int.TryParse(team.LastPoints, out var points))
                {
                    while (points > 0)
                    {
                        var amount = Math.Min(points, 100) * (revert ? -100_000 : 100_000);
                        await PostCashAsync(team.TeamInfo.User.Id, amount, revert ? "CORREGIR ERROR PUNTS" : "PUNTS");
                        total = checked(total + amount);
                        points -= 100;
                    }
                }
            }
            catch (Exception ex)
            {
                if (total != 0) results.Add(new() { UserName = team.TeamInfo.User.Name, Amount = total });
                throw new CashOperationException(team.TeamInfo.User.Name, results.ToArray(), ex);
            }
            results.Add(new() { UserName = team.TeamInfo.User.Name, Amount = total });
        }
        return results.OrderByDescending(item => item.Amount).ToList();
    }

    private async Task PostCashAsync(string userId, int amount, string reason)
    {
        var session = await GetSessionAsync();

        await PostWithoutResponseAsync($"communities/{Uri.EscapeDataString(session.Community.Id)}/users/{Uri.EscapeDataString(userId)}/penalties",
            new PostCashRequestModel { Amount = amount, Reason = reason });
    }
}

public sealed class CashOperationException(string userName, IReadOnlyList<DineroJornadaModel> confirmedPayments, Exception innerException)
    : Exception($"No se ha podido completar la operación para {userName}. Revisa los movimientos antes de repetirla: la última petición podría haberse aplicado.", innerException)
{
    public IReadOnlyList<DineroJornadaModel> ConfirmedPayments { get; } = confirmedPayments;
}
