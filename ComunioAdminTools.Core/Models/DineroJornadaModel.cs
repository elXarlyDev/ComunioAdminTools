using System.Globalization;

namespace ComunioAdminTools.Core.Models;

public sealed class DineroJornadaModel
{
    public string UserName { get; init; } = string.Empty;
    public int Amount { get; init; }
    public string FormattedAmount => FormatAmount(CultureInfo.CurrentCulture);
    public string FormatAmount(CultureInfo culture) => Amount.ToString("C0", culture);
}
