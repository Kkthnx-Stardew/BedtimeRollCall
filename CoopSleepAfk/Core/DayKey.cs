namespace CoopSleepAfk.Core
{
    /// <summary>Names a game day, so a report from yesterday is never mistaken for today's.</summary>
    public static class DayKey
    {
        public static string Make(int year, string season, int dayOfMonth) => $"Y{year} {season} {dayOfMonth}";
    }
}
