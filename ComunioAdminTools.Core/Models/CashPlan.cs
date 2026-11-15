namespace ComunioAdminTools.Core.Models;

public sealed class CashPlan
{
    private int executed;

    internal CashPlan(string userId, string communityId, CashOperation operation, IEnumerable<CashPayment> payments)
    {
        UserId = userId;
        CommunityId = communityId;
        Operation = operation;
        Payments = Array.AsReadOnly(payments.ToArray());
    }

    public CashOperation Operation { get; }
    public IReadOnlyList<CashPayment> Payments { get; }
    internal string UserId { get; }
    internal string CommunityId { get; }

    internal bool TryExecute() => Interlocked.CompareExchange(ref executed, 1, 0) == 0;
}

public sealed record CashPayment(string UserId, string UserName, int Amount);

public enum CashOperation
{
    RoundPoints,
    RevertRoundPoints,
    InternationalBreak,
    Correction
}
