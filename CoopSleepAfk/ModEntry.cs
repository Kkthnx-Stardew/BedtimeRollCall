using System.Diagnostics;
using CoopSleepAfk.Core;
using CoopSleepAfk.Hud;
using CoopSleepAfk.Net;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace CoopSleepAfk
{
    /// <summary>
    /// A panel that shows who is in bed in a co-op session and who is idle, and an idle tracker that tells the other players. It reads the
    /// game and changes nothing in it. No Harmony, nothing saved. Uninstalling leaves the save as vanilla.
    /// </summary>
    public sealed class ModEntry : Mod
    {
        private const double HeartbeatSeconds = 10;
        private const double MaxReportAgeSeconds = 35;
        private const int MaxPanelPlayers = 8;

        private ModConfig _config = new();
        private readonly AfkClock _clock = new();
        private readonly PeerActivityBook _book = new();
        private readonly Stopwatch _time = Stopwatch.StartNew();

        private RulesMessage? _hostRules;
        private Point _lastTile;
        private int _lastFacing = -1;
        private bool _hasLastPosition;
        private ActivityState _myActivity = ActivityState.Active;
        private ActivityState _lastSent = ActivityState.Unknown;
        private double _lastSentAt = double.NegativeInfinity;
        private bool _warned;
        private HudModel? _hud;

        public override void Entry(IModHelper helper)
        {
            try
            {
                _config = helper.ReadConfig<ModConfig>() ?? new ModConfig();
            }
            catch (Exception ex)
            {
                // a config.json that cannot be read must not stop the mod, and the file is left alone
                Monitor.Log($"Could not read config.json, so the default settings are used. Fix it or delete it. ({ex.Message})", LogLevel.Warn);
            }
            _config.Normalize();

            helper.Events.GameLoop.SaveLoaded += (_, _) => ResetSession();
            helper.Events.GameLoop.ReturnedToTitle += (_, _) => ResetSession();
            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.GameLoop.OneSecondUpdateTicked += OnOneSecondUpdateTicked;
            helper.Events.Input.ButtonPressed += OnButtonPressed;
            helper.Events.Input.CursorMoved += OnCursorMoved;
            helper.Events.Display.RenderedHud += OnRenderedHud;
            helper.Events.Multiplayer.ModMessageReceived += OnModMessageReceived;
            helper.Events.Multiplayer.PeerConnected += OnPeerConnected;
            helper.Events.Multiplayer.PeerDisconnected += OnPeerDisconnected;
        }

        // ---- shared rules ------------------------------------------------------

        /// <summary>The host's idle rules win for every player who has the mod. Without a host that has the mod, each player's own settings apply.</summary>
        private int AfkMinutes => !Context.IsMainPlayer && _hostRules is not null ? Math.Clamp(_hostRules.AfkMinutes, 1, 30) : _config.AfkMinutes;

        private bool AfkCountsAsReady => !Context.IsMainPlayer && _hostRules is not null ? _hostRules.AfkCountsAsReady : _config.AfkCountsAsReady;

        private double Now => _time.Elapsed.TotalSeconds;

        // ---- state -----------------------------------------------------------------

        private void ResetSession()
        {
            _book.Clear();
            _hostRules = null;
            _clock.NoteActivity();
            _hasLastPosition = false;
            _warned = false;
            _hud = null;
            _lastSent = ActivityState.Unknown;
            _lastSentAt = double.NegativeInfinity;
            _myActivity = ActivityState.Active;
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            // everyone wakes up active, and yesterday's reports no longer match the day key
            _clock.NoteActivity();
            _warned = false;
            if (Context.IsMainPlayer && Context.IsMultiplayer)
                SendRules(null);
        }

        private static BedState ReadBed(Farmer farmer)
        {
            // isInBed follows the bed tile. timeWentToBed is set when the game starts that player's sleep and cleared when they get up
            if (!farmer.isInBed.Value)
                return BedState.Out;
            return farmer.timeWentToBed.Value > 0 ? BedState.Confirmed : BedState.InBed;
        }

        // ---- input -------------------------------------------------------------

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e) => NoteActivity();

        private void OnCursorMoved(object? sender, CursorMovedEventArgs e)
        {
            if (_config.CountCursorMovementAsActivity)
                NoteActivity();
        }

        private void NoteActivity()
        {
            _clock.NoteActivity();
            _warned = false;
        }

        // ---- once a second -------------------------------------------------------

        private void OnOneSecondUpdateTicked(object? sender, OneSecondUpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            try
            {
                var me = Game1.player;
                var tile = me.TilePoint;
                int facing = me.FacingDirection;
                if (_hasLastPosition && (tile != _lastTile || facing != _lastFacing))
                    NoteActivity();
                _lastTile = tile;
                _lastFacing = facing;
                _hasLastPosition = true;

                // waiting in bed, a menu, a cutscene and a festival neither add to the idle time nor reset it
                bool counting = Context.IsPlayerFree && ReadBed(me) == BedState.Out && !Game1.isFestival();
                _clock.Advance(1, counting);

                double threshold = AfkMinutes * 60.0;
                _myActivity = Game1.eventUp
                    ? ActivityState.InEvent
                    : _config.EnableAfkTracking && _clock.IsAfk(threshold) ? ActivityState.Afk : ActivityState.Active;

                bool warning = _config.EnableAfkTracking && counting && _clock.InWarning(threshold, _config.WarnBeforeAfkSeconds);
                if (warning && !_warned)
                {
                    _warned = true;
                    Game1.addHUDMessage(new HUDMessage($"You will show as idle in {(int)Math.Ceiling(_clock.SecondsUntilAfk(threshold))} seconds. Press any key to stay active.") { noIcon = true });
                }

                Broadcast();
                _hud = BuildHud(me, warning ? _clock.SecondsUntilAfk(threshold) : -1);
            }
            catch (Exception ex)
            {
                Monitor.Log($"Sleep panel update failed and will try again next second. {ex.Message}", LogLevel.Debug);
            }
        }

        private void Broadcast()
        {
            if (!Context.IsMultiplayer)
                return;
            if (_myActivity == _lastSent && Now - _lastSentAt < HeartbeatSeconds)
                return;

            _lastSent = _myActivity;
            _lastSentAt = Now;
            Helper.Multiplayer.SendMessage(
                new StateMessage { State = _myActivity, IdleSeconds = _clock.IdleSeconds, DayKey = CurrentDayKey() },
                MessageTypes.State,
                new[] { ModManifest.UniqueID });
        }

        private static string CurrentDayKey() => DayKey.Make(Game1.year, Game1.currentSeason, Game1.dayOfMonth);

        // ---- the panel ----------------------------------------------------------

        private HudModel BuildHud(Farmer me, double warnSeconds)
        {
            string day = CurrentDayKey();
            var farmers = Game1.getOnlineFarmers().Where(f => !f.IsDedicatedPlayer).ToList();
            var activity = new Dictionary<long, (ActivityState State, double Idle)>();
            foreach (var farmer in farmers)
            {
                activity[farmer.UniqueMultiplayerID] = farmer.IsLocalPlayer
                    ? (_myActivity, _clock.IdleSeconds)
                    : _book.Get(farmer.UniqueMultiplayerID, Now, day, MaxReportAgeSeconds);
            }

            var tally = ReadinessTally.Compute(
                farmers.Select(f => new TallyPlayer(f.UniqueMultiplayerID, f.Name, ReadBed(f), activity[f.UniqueMultiplayerID].State)),
                AfkCountsAsReady);

            bool festival = Game1.isFestival();
            string header = festival ? "Festival day, sleep tools paused" : $"{tally.Ready}/{tally.Total} ready for bed";
            Color headerColor = festival ? Color.LightGray : tally.AllReady ? Color.LightGreen : tally.Blocked.Count > 0 ? Color.Orange : Color.White;

            var lines = new List<HudLine>();
            foreach (var farmer in farmers.Take(MaxPanelPlayers))
            {
                var (state, idle) = activity[farmer.UniqueMultiplayerID];
                lines.Add(DescribePlayer(farmer, ReadBed(farmer), state, idle));
            }
            if (farmers.Count > MaxPanelPlayers)
                lines.Add(new HudLine($"and {farmers.Count - MaxPanelPlayers} more", Color.Gray));

            // the game's own count of players who have confirmed sleep, when a sleep check is running
            int required = Game1.netReady.GetNumberRequired("sleep");
            if (required > 0)
                lines.Add(new HudLine($"Game: {Game1.netReady.GetNumberReady("sleep")}/{required} confirmed", Color.LightGray));

            if (warnSeconds >= 0)
                lines.Add(new HudLine($"You show as idle in {(int)Math.Ceiling(warnSeconds)}s", Color.Orange));

            return new HudModel(header, headerColor, lines);
        }

        private static HudLine DescribePlayer(Farmer farmer, BedState bed, ActivityState state, double idleSeconds)
        {
            string name = farmer.Name;
            if (bed == BedState.Confirmed)
                return new HudLine($"{name}, in bed and ready", Color.LightGreen);
            if (bed == BedState.InBed)
                return new HudLine($"{name}, in bed", Color.LightGreen);

            return state switch
            {
                ActivityState.Afk => new HudLine($"{name}, idle {Math.Max(1, (int)(idleSeconds / 60))}m", Color.Orange),
                ActivityState.InEvent => new HudLine($"{name}, in an event", Color.OrangeRed),
                ActivityState.Unknown => new HudLine($"{name}, not tracked", Color.Gray),
                _ => new HudLine($"{name}, up", Color.White)
            };
        }

        private void OnRenderedHud(object? sender, RenderedHudEventArgs e)
        {
            // the panel is for sessions with other players, and it gets out of the way of every menu and cutscene
            if (!_config.EnableHud || _hud is null || !Context.IsWorldReady || !Context.IsMultiplayer || Game1.activeClickableMenu != null || Game1.eventUp)
                return;

            SleepHud.Draw(e.SpriteBatch, _hud, _config.HudScale);
        }

        // ---- messages -----------------------------------------------------------

        private void OnPeerConnected(object? sender, PeerConnectedEventArgs e)
        {
            if (Context.IsMainPlayer)
                SendRules(new[] { e.Peer.PlayerID });
        }

        private void OnPeerDisconnected(object? sender, PeerDisconnectedEventArgs e) => _book.Remove(e.Peer.PlayerID);

        private void SendRules(long[]? playerIds)
        {
            Helper.Multiplayer.SendMessage(
                new RulesMessage { AfkMinutes = _config.AfkMinutes, AfkCountsAsReady = _config.AfkCountsAsReady },
                MessageTypes.Rules,
                new[] { ModManifest.UniqueID },
                playerIds);
        }

        private void OnModMessageReceived(object? sender, ModMessageReceivedEventArgs e)
        {
            if (e.FromModID != ModManifest.UniqueID || !Context.IsWorldReady)
                return;

            try
            {
                // only players the game has fully connected are heard
                if (!Game1.getOnlineFarmers().Any(f => f.UniqueMultiplayerID == e.FromPlayerID))
                    return;

                switch (e.Type)
                {
                    case MessageTypes.State:
                    {
                        var message = e.ReadAs<StateMessage>();
                        if (Enum.IsDefined(message.State))
                            _book.Update(e.FromPlayerID, message.State, Math.Max(0, message.IdleSeconds), message.DayKey ?? "", Now);
                        break;
                    }
                    case MessageTypes.Rules:
                        // shared rules come from the host only
                        if (!Context.IsMainPlayer && e.FromPlayerID == Game1.MasterPlayer.UniqueMultiplayerID)
                            _hostRules = e.ReadAs<RulesMessage>();
                        break;
                }
            }
            catch (Exception ex)
            {
                Monitor.Log($"Dropped a {e.Type} message from player {e.FromPlayerID} that could not be read. {ex.Message}", LogLevel.Debug);
            }
        }
    }
}
