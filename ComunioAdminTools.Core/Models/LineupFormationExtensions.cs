namespace ComunioAdminTools.Core.Models;

public static class LineupFormationExtensions
{
    public static PlayerPosition GetSlotPosition(this LineupFormation formation, int index)
    {
        if (!Enum.IsDefined(formation))
        {
            throw new ArgumentOutOfRangeException(nameof(formation));
        }

        if (index is < 0 or >= 11)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        if (index == 0)
        {
            return PlayerPosition.Keeper;
        }

        var defenders = (int)formation / 100;
        var midfielders = (int)formation / 10 % 10;

        return index <= defenders ? PlayerPosition.Defender
            : index <= defenders + midfielders ? PlayerPosition.Midfielder : PlayerPosition.Striker;
    }
}
