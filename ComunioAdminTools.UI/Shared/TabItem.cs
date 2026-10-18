using Microsoft.AspNetCore.Components;

namespace ComunioAdminTools.UI.Shared;

public sealed record TabItem(string Title, RenderFragment Content);
