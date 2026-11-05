using CoopSleepAfk.Core;

namespace CoopSleepAfk.Net
{
    /// <summary>The message types this mod sends through SMAPI. Every message class needs a parameterless constructor to be read back.</summary>
    internal static class MessageTypes
    {
        public const string State = "CoopSleepAfk.State";
        public const string Rules = "CoopSleepAfk.Rules";
    }

    /// <summary>One player's own report of what they are doing. Sent when it changes and every few seconds after that.</summary>
    internal sealed class StateMessage
    {
        public ActivityState State { get; set; }
        public double IdleSeconds { get; set; }
        public string DayKey { get; set; } = "";
    }

    /// <summary>The host's shared idle rules, sent to every player who has the mod.</summary>
    internal sealed class RulesMessage
    {
        public int AfkMinutes { get; set; }
        public bool AfkCountsAsReady { get; set; }
    }
}
