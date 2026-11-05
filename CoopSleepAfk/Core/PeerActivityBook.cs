namespace CoopSleepAfk.Core
{
    /// <summary>
    /// The last activity report heard from each other player. A report that is too old, or that is from another game day, counts for
    /// nothing, so a player whose mod went quiet shows as unknown instead of keeping a stale idle or active state.
    /// </summary>
    public sealed class PeerActivityBook
    {
        private readonly record struct Entry(ActivityState State, double IdleSeconds, string DayKey, double ReceivedAt);

        private readonly Dictionary<long, Entry> _entries = new();

        public void Update(long playerId, ActivityState state, double idleSeconds, string dayKey, double now)
        {
            _entries[playerId] = new Entry(state, idleSeconds, dayKey, now);
        }

        public void Remove(long playerId) => _entries.Remove(playerId);

        public void Clear() => _entries.Clear();

        /// <summary>True once anything at all has been heard from that player, however old.</summary>
        public bool HasHeardFrom(long playerId) => _entries.ContainsKey(playerId);

        public (ActivityState State, double IdleSeconds) Get(long playerId, double now, string currentDayKey, double maxAgeSeconds)
        {
            if (!_entries.TryGetValue(playerId, out var entry))
                return (ActivityState.Unknown, 0);
            if (entry.DayKey != currentDayKey || now - entry.ReceivedAt > maxAgeSeconds)
                return (ActivityState.Unknown, 0);
            return (entry.State, entry.IdleSeconds);
        }
    }
}
