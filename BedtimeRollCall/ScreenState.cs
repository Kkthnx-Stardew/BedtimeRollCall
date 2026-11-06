using BedtimeRollCall.Core;
using BedtimeRollCall.Hud;
using BedtimeRollCall.Net;
using BedtimeRollCall.Sleep;
using Microsoft.Xna.Framework;

namespace BedtimeRollCall
{
    /// <summary>
    /// Everything that belongs to one local player. With split screen each local player gets their own copy through <c>PerScreen</c>,
    /// so one player's idle time, trip to bed and panel never leak into the other's.
    /// </summary>
    internal sealed class ScreenState
    {
        public readonly AfkClock Clock = new();
        public readonly PeerActivityBook Book = new();
        public readonly BedTrip Trip = new();
        public readonly HashSet<long> RulesSentTo = new();

        public RulesMessage? HostRules;
        public Point LastTile;
        public int LastFacing = -1;
        public bool HasLastPosition;
        public ActivityState MyActivity = ActivityState.Active;
        public ActivityState LastSent = ActivityState.Unknown;
        public double LastSentAt = double.NegativeInfinity;
        public bool Warned;
        public bool AutoTriedToday;
        public HudModel? Hud;

        /// <summary>Back to a fresh session, for a loaded save or a return to the title screen.</summary>
        public void Reset()
        {
            Book.Clear();
            RulesSentTo.Clear();
            Clock.NoteActivity();
            Trip.Cancel();
            HostRules = null;
            HasLastPosition = false;
            MyActivity = ActivityState.Active;
            LastSent = ActivityState.Unknown;
            LastSentAt = double.NegativeInfinity;
            Warned = false;
            AutoTriedToday = false;
            Hud = null;
        }
    }
}
