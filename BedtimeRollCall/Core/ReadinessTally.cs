namespace BedtimeRollCall.Core
{
    /// <summary>One online player as the tally sees them.</summary>
    public readonly record struct TallyPlayer(long Id, string Name, BedState Bed, ActivityState Activity);

    /// <summary>
    /// How many players are ready for the night, by this mod's rules. The game's own answer is separate (<c>Game1.netReady</c>). This one also
    /// knows who is idle, and it never counts a player who is in an event as ready.
    /// </summary>
    public sealed record Tally(int Ready, int Total, int Untracked, IReadOnlyList<string> Blocked, IReadOnlyList<string> Idle)
    {
        /// <summary>Every player is ready and nobody is held up by an event.</summary>
        public bool AllReady => Total > 0 && Ready == Total && Blocked.Count == 0;
    }

    public static class ReadinessTally
    {
        public static Tally Compute(IEnumerable<TallyPlayer> players, bool afkCountsAsReady)
        {
            int ready = 0, total = 0, untracked = 0;
            var blocked = new List<string>();
            var idle = new List<string>();
            foreach (var player in players)
            {
                total++;
                if (player.Activity == ActivityState.Unknown)
                    untracked++;

                bool inBed = player.Bed != BedState.Out;
                if (player.Activity == ActivityState.InEvent && !inBed)
                {
                    blocked.Add(player.Name);
                    continue;
                }

                if (player.Activity == ActivityState.Afk && !inBed)
                    idle.Add(player.Name);

                if (inBed || (player.Activity == ActivityState.Afk && afkCountsAsReady))
                    ready++;
            }

            return new Tally(ready, total, untracked, blocked, idle);
        }
    }
}
