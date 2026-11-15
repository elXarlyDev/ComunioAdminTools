namespace ComunioAdminTools.Core.Models;

public sealed record LineupResult(LineupFormation Formation, IReadOnlyList<PlayerDetailsModel?> Starters);
