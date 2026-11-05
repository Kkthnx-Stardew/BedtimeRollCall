namespace CoopSleepAfk.Core
{
    /// <summary>
    /// Counts how long the local player has gone without input. The game has no signal for this. Time only counts while the player is free
    /// to act, so a menu, a cutscene or waiting in bed neither adds to it nor resets it.
    /// </summary>
    public sealed class AfkClock
    {
        public double IdleSeconds { get; private set; }

        /// <summary>Any input, or a change in where the player stands, starts the count again.</summary>
        public void NoteActivity() => IdleSeconds = 0;

        /// <summary>Adds elapsed time to the count, but only while <paramref name="counting"/> is true.</summary>
        public void Advance(double seconds, bool counting)
        {
            if (counting && seconds > 0)
                IdleSeconds += seconds;
        }

        public bool IsAfk(double thresholdSeconds) => thresholdSeconds > 0 && IdleSeconds >= thresholdSeconds;

        /// <summary>True in the last <paramref name="warnSeconds"/> before the threshold, so the player can be told before they are marked.</summary>
        public bool InWarning(double thresholdSeconds, double warnSeconds)
        {
            return thresholdSeconds > 0 && warnSeconds > 0 && !IsAfk(thresholdSeconds) && IdleSeconds >= thresholdSeconds - warnSeconds;
        }

        public double SecondsUntilAfk(double thresholdSeconds) => Math.Max(0, thresholdSeconds - IdleSeconds);
    }
}
