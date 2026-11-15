using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

using ComunioAdminTools.Core.Authentication;
using ComunioAdminTools.Core.Localization;
using ComunioAdminTools.Core.Models;

namespace ComunioAdminTools.Core.Services;

public interface ICashService
{
    Task<CashPlan> Prepare(CashOperation operation, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CashResultModel>> Apply(CashPlan plan);
}

public sealed class CashService(IHttpClientFactory factory, ISessionManager sessionManager, AppLocalization localization)
    : CommunityService(factory, sessionManager, localization), ICashService
{
    private const int NewsPageSize = 20;

    private static readonly TimeSpan NewsRequestTimeout = TimeSpan.FromSeconds(30);

    private static readonly Regex BonusPattern = new(
        @"^(?:[^:]*:\s*)?(?<amount>[+-]?(?:\d{1,3}(?:\.\d{3})+|\d+))\s*(?:€\s*)?(?<action>serán abonados|han sido abonados|han sido restados|serán restados)\s+a\s+(?<user>.+?)\s+por el administrador de la comunidad\s+\(",
        RegexOptions.CultureInvariant);

    public async Task<CashPlan> Prepare(CashOperation operation, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        var session = Session;
        var standings = await GetStandings(cancellationToken);
        var correctionAmounts = operation == CashOperation.Correction ? await GetCorrectionAmounts(standings, cancellationToken) : null;
        var payments = new List<CashPayment>();

        foreach (var team in standings.Teams)
        {
            var amount = correctionAmounts is not null ? correctionAmounts[team.TeamInfo.User.Id]
                : operation == CashOperation.InternationalBreak ? 5_000_000
                : int.TryParse(team.LastPoints, out var points) && points > 0
                    ? checked(points * (operation == CashOperation.RevertRoundPoints ? -100_000 : 100_000)) : 0;

            if (operation == CashOperation.Correction && amount == 0)
            {
                continue;
            }

            payments.Add(new(team.TeamInfo.User.Id, team.TeamInfo.User.Name, amount));
        }

        var plan = new CashPlan(session.User.Id, session.Community.Id, operation, payments);
        EnsurePlanSession(plan);
        return plan;
    }

    public async Task<IReadOnlyList<CashResultModel>> Apply(CashPlan plan)
    {
        EnsurePlanSession(plan);

        if (!plan.TryExecute())
        {
            throw new LocalizedInvalidOperationException(text, TextKey.CashPlanExpired);
        }

        var reason = plan.Operation switch
        {
            CashOperation.RoundPoints => "PUNTS",
            CashOperation.RevertRoundPoints => "REVERTIR PUNTS",
            CashOperation.Correction => "CORRECCIÓ PUNTS",
            _ => "PARÓ SELECCIONS"
        };

        var results = new List<CashResultModel>();

        foreach (var payment in plan.Payments)
        {
            var total = 0;
            var remaining = payment.Amount;

            try
            {
                while (remaining != 0)
                {
                    EnsurePlanSession(plan);
                    var amount = Math.Clamp(remaining, -10_000_000, 10_000_000);

                    await PostWithoutResponse($"communities/{Uri.EscapeDataString(plan.CommunityId)}/users/{Uri.EscapeDataString(payment.UserId)}/penalties",
                        new PostCashRequestModel { Amount = amount, Reason = reason });

                    total += amount;
                    remaining -= amount;
                }
            }
            catch (Exception ex)
            {
                if (total != 0) results.Add(new() { UserName = payment.UserName, Amount = total });
                throw new CashOperationException(text, payment.UserName, results.ToArray(), ex);
            }

            results.Add(new() { UserName = payment.UserName, Amount = total });
        }

        return results.OrderByDescending(item => item.Amount).ToArray();
    }

    private async Task<IReadOnlyDictionary<string, int>> GetCorrectionAmounts(StandingsModel standings, CancellationToken cancellationToken)
    {
        var matchdays = await Get<List<MatchdayModel>>("matchdays", cancellationToken);

        var matchday = matchdays.Where(matchday => matchday.Started && matchday.Finished)
            .OrderByDescending(matchday => matchday.Timestamp).FirstOrDefault()
            ?? throw new LocalizedInvalidOperationException(text, TextKey.NoFinishedMatchday);

        if (string.IsNullOrWhiteSpace(matchday.MatchdayKey))
        {
            throw new LocalizedInvalidOperationException(text, TextKey.InvalidJsonResponse);
        }

        var nextMatchday = matchdays.Where(candidate => candidate.Timestamp > matchday.Timestamp)
            .OrderBy(candidate => candidate.Timestamp).FirstOrDefault()
            ?? throw new LocalizedInvalidOperationException(text, TextKey.NoNextMatchday);

        var bonuses = await GetRoundBonuses(matchday.MatchdayKey, nextMatchday.Timestamp, cancellationToken);

        var users = standings.Teams.GroupBy(team => NormalizeText(team.TeamInfo.User.Name), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);

        var totals = standings.Teams.ToDictionary(team => team.TeamInfo.User.Id, _ => 0);

        foreach (var bonus in bonuses)
        {
            var source = NormalizeText(bonus.Message?.Text ?? "");
            var match = BonusPattern.Match(source);

            if (!match.Success || !int.TryParse(match.Groups["amount"].Value.Replace(".", ""), NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out var amount))
            {
                throw new LocalizedInvalidOperationException(text, TextKey.UnreadableBonusNews, arguments: [bonus.Id, source]);
            }

            var recipient = NormalizeText(match.Groups["user"].Value);

            if (!users.TryGetValue(recipient, out var recipients))
            {
                throw new LocalizedInvalidOperationException(text, TextKey.BonusRecipientNotFound, arguments: [recipient, bonus.Id]);
            }

            if (recipients.Length != 1)
            {
                throw new LocalizedInvalidOperationException(text, TextKey.AmbiguousBonusRecipient, arguments: [recipient, bonus.Id]);
            }

            if (match.Groups["action"].Value.EndsWith("restados", StringComparison.Ordinal) && amount > 0)
            {
                amount = -amount;
            }

            var userId = recipients[0].TeamInfo.User.Id;
            totals[userId] = checked(totals[userId] + amount);
        }

        return standings.Teams.ToDictionary(team => team.TeamInfo.User.Id, team =>
        {
            if (!int.TryParse(team.LastPoints, out var points))
            {
                throw new LocalizedInvalidOperationException(text, TextKey.InvalidJsonResponse);
            }

            return checked(Math.Max(points, 0) * 100_000 - totals[team.TeamInfo.User.Id]);
        });
    }

    private async Task<IReadOnlyList<CommunityNewsModel.NewsEntry>> GetRoundBonuses(string matchday, DateTimeOffset nextMatchdayStart, CancellationToken cancellationToken)
    {
        var userId = Uri.EscapeDataString(Session.User.Id);
        var communityUri = GetCommunityUriPart();
        var bonuses = new List<CommunityNewsModel.NewsEntry>();
        var seen = new HashSet<string>();
        DateTimeOffset? calculationDate = null;

        for (var start = 0; ; start = checked(start + NewsPageSize))
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(NewsRequestTimeout);
            CommunityNewsModel response;

            try
            {
                response = await Get<CommunityNewsModel>($"{communityUri}/users/{userId}/news?originaltypes=true&start={start}&limit={NewsPageSize}", timeout.Token)
                    .WaitAsync(timeout.Token);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && timeout.IsCancellationRequested)
            {
                throw new LocalizedHttpRequestException(text, TextKey.NewsRequestTimeout, inner: ex);
            }

            var page = response.NewsList;
            var newEntries = 0;

            foreach (var entry in page.Entries)
            {
                if (!seen.Add(entry.Id))
                {
                    continue;
                }

                newEntries++;

                if (calculationDate is null && entry.Type == "SYSTEM_ADMINISTRATION"
                    && entry.Message?.Type == "POINTS_CALCULATION" && entry.Message.Matchday == matchday)
                    calculationDate = entry.Date;

                if (entry.Type == "TRANSACTION_BONUS" && entry.Date < nextMatchdayStart)
                    bonuses.Add(entry);
            }

            if (calculationDate is { } date && (!page.HasMore || page.Entries.LastOrDefault()?.Date < date))
            {
                return bonuses.Where(entry => entry.Date >= date).ToArray();
            }

            if (!page.HasMore)
            {
                throw new LocalizedInvalidOperationException(text, TextKey.PointsCalculationNewsNotFound);
            }

            if (newEntries == 0)
            {
                throw new LocalizedInvalidOperationException(text, TextKey.InvalidJsonResponse);
            }
        }
    }

    private static string NormalizeText(string value)
        => Regex.Replace(WebUtility.HtmlDecode(value), @"\s+", " ").Trim().Normalize(NormalizationForm.FormC);

    private void EnsurePlanSession(CashPlan plan)
    {
        var session = Session;

        if (session.User.Id != plan.UserId || session.Community.Id != plan.CommunityId)
        {
            throw new LocalizedInvalidOperationException(text, TextKey.CashPlanExpired);
        }
    }
}

public sealed class CashOperationException(AppLocalization text, string userName, IReadOnlyList<CashResultModel> confirmedPayments, Exception innerException)
    : Exception(text.Format(TextKey.CashOperationError, userName), innerException), ILocalizedError
{
    public IReadOnlyList<CashResultModel> ConfirmedPayments { get; } = confirmedPayments;

    public TextKey ResourceKey => TextKey.CashOperationError;

    public object?[] Arguments => [userName];

    public override string Message => text.Format(ResourceKey, Arguments);
}
