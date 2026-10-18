namespace ComunioAdminTools.Core.Models;

public static class SeasonCalendar
{
    public static IReadOnlyList<DateOnly> GetMonths(DateOnly currentDate)
    {
        var start = new DateOnly(currentDate.Month >= 8 ? currentDate.Year : currentDate.Year - 1, 8, 1);
        var count = (currentDate.Year - start.Year) * 12 + currentDate.Month - start.Month + 1;
        return Enumerable.Range(0, count).Select(start.AddMonths).ToArray();
    }
}
