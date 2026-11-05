namespace BedtimeRollCall.Core
{
    /// <summary>Why a trip to bed cannot start right now. <see cref="None"/> means it can.</summary>
    public enum SleepBlock
    {
        None,
        NotMultiplayer,
        NewDay,
        Event,
        Festival,
        Dungeon,
        AlreadyInBed,
        Busy,
        NoBed
    }

    /// <summary>What the game is doing at the moment a trip to bed is asked for.</summary>
    public readonly record struct SleepContext(
        bool IsMultiplayer,
        bool NewDayStarting,
        bool InEvent,
        bool IsFestival,
        bool InDungeon,
        bool Sleeping,
        bool PlayerFree,
        bool HasBed);

    /// <summary>
    /// The rules for sending the local player to their own bed. Kept free of game types so every branch is covered by a unit test. A trip is
    /// refused in single player because the confirmation screen that lets a player back out only exists in co-op.
    /// </summary>
    public static class SleepPolicy
    {
        public static SleepBlock Check(SleepContext c)
        {
            if (!c.IsMultiplayer)
                return SleepBlock.NotMultiplayer;
            if (c.NewDayStarting)
                return SleepBlock.NewDay;
            if (c.InEvent)
                return SleepBlock.Event;
            if (c.IsFestival)
                return SleepBlock.Festival;
            if (c.InDungeon)
                return SleepBlock.Dungeon;
            if (c.Sleeping)
                return SleepBlock.AlreadyInBed;
            if (!c.PlayerFree)
                return SleepBlock.Busy;
            if (!c.HasBed)
                return SleepBlock.NoBed;
            return SleepBlock.None;
        }

        /// <summary>
        /// Whether an idle player is sent to bed by themselves. Needs the player's own opt in, an idle state, at least one other player already
        /// in bed (so nobody is ever put to bed alone), and one try per day so a cancelled sleep is not forced again.
        /// </summary>
        public static bool ShouldAutoSleep(bool enabled, bool idle, int othersInBed, bool triedToday, SleepBlock block) =>
            enabled && idle && othersInBed > 0 && !triedToday && block == SleepBlock.None;

        /// <summary>The translation key that explains a refusal, or an empty string when there was none.</summary>
        public static string ReasonKey(SleepBlock block) => block switch
        {
            SleepBlock.NotMultiplayer => "block.not-multiplayer",
            SleepBlock.NewDay => "block.new-day",
            SleepBlock.Event => "block.event",
            SleepBlock.Festival => "block.festival",
            SleepBlock.Dungeon => "block.dungeon",
            SleepBlock.AlreadyInBed => "block.already-sleeping",
            SleepBlock.Busy => "block.busy",
            SleepBlock.NoBed => "block.no-bed",
            _ => ""
        };
    }
}
