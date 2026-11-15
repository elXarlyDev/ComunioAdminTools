using ComunioAdminTools.Core.Models;
using ComunioAdminTools.Core.Localization;

namespace ComunioAdminTools.Core.Services;

using ComunioAdminTools.Core.Authentication;

public interface ICashService
{
    Task<IReadOnlyList<DineroJornadaModel>> PayRoundPoints();
    Task<IReadOnlyList<DineroJornadaModel>> RevertRoundPoints();
    Task<IReadOnlyList<DineroJornadaModel>> PayInternationalBreak();
}

public sealed class CashService(IHttpClientFactory factory, ISessionManager sessionManager, AppLocalization localization) : CommunityService(factory, sessionManager, localization), ICashService
{
    public Task<IReadOnlyList<DineroJornadaModel>> PayRoundPoints() => ManageRoundPoints(false);
    public Task<IReadOnlyList<DineroJornadaModel>> RevertRoundPoints() => ManageRoundPoints(true);

    public async Task<IReadOnlyList<DineroJornadaModel>> PayInternationalBreak()
    {
        var standings = await GetStandings();
        var results = new List<DineroJornadaModel>();

        foreach (var team in standings.Teams)
        {
            try
            {
                await PostCash(team.TeamInfo.User.Id, 5_000_000, "PARÓ SELECCIONS");
            }
            catch (Exception ex)
            {
                throw new CashOperationException(text, team.TeamInfo.User.Name, results.ToArray(), ex);
            }

            results.Add(new() { UserName = team.TeamInfo.User.Name, Amount = 5_000_000 });
        }

        return results.OrderByDescending(item => item.Amount).ToList();
    }

    private async Task<IReadOnlyList<DineroJornadaModel>> ManageRoundPoints(bool revert)
    {
        var standings = await GetStandings();
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
                        await PostCash(team.TeamInfo.User.Id, amount, revert ? "CORREGIR ERROR PUNTS" : "PUNTS");
                        total = checked(total + amount);
                        points -= 100;
                    }
                }
            }
            catch (Exception ex)
            {
                if (total != 0) results.Add(new() { UserName = team.TeamInfo.User.Name, Amount = total });
                throw new CashOperationException(text, team.TeamInfo.User.Name, results.ToArray(), ex);
            }

            results.Add(new() { UserName = team.TeamInfo.User.Name, Amount = total });
        }

        return results.OrderByDescending(item => item.Amount).ToList();
    }

    private async Task PostCash(string userId, int amount, string reason)
    {

        await PostWithoutResponse($"communities/{Uri.EscapeDataString(Session.Community.Id)}/users/{Uri.EscapeDataString(userId)}/penalties",
            new PostCashRequestModel { Amount = amount, Reason = reason });
    }
}

public sealed class CashOperationException(AppLocalization text, string userName, IReadOnlyList<DineroJornadaModel> confirmedPayments, Exception innerException)
    : Exception(text.Format(TextKey.CashOperationError, userName), innerException), ILocalizedError
{
    public IReadOnlyList<DineroJornadaModel> ConfirmedPayments { get; } = confirmedPayments;

    public TextKey ResourceKey => TextKey.CashOperationError;

    public object?[] Arguments => [userName];

    public override string Message => text.Format(ResourceKey, Arguments);
}
