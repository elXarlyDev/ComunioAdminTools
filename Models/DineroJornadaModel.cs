using System.Globalization;

namespace ComunioAdminTools.Models;

public sealed class DineroJornadaModel
{
    public string UserName { get; init; } = string.Empty;
    public int Amount { get; init; }
    public string FormattedAmount => Amount.ToString("C0", CultureInfo.GetCultureInfo("es-ES"));
}
