namespace ComunioAdminTools.Core.Models;

public sealed record SquadExcessResult(string? MatchdayKey, IReadOnlyList<ExcesoPlantillaModel> Squads);
