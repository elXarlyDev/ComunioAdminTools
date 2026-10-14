using System.Globalization;
using ComunioAdminTools.Core.Localization;
using Microsoft.AspNetCore.Components;

namespace ComunioAdminTools.UI;

public abstract class LocalizedComponentBase : ComponentBase
{
    [Inject] protected AppLocalization Text { get; set; } = default!;
    [CascadingParameter] public CultureInfo? UiCulture { get; set; }
}
