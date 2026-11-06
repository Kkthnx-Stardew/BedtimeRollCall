using System.Diagnostics;
using BedtimeRollCall.Core;
using BedtimeRollCall.Hud;
using BedtimeRollCall.Integration;
using BedtimeRollCall.Net;
using BedtimeRollCall.Sleep;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;

namespace BedtimeRollCall
{
    /// <summary>
    /// A panel that shows who is in bed in a co-op session and who is idle, an idle tracker that tells the other players, and an optional
    /// way to take your own character to bed through the game's own sleep check. No Harmony and nothing saved, so uninstalling leaves the
    /// save as vanilla.
    /// </summary>
    public sealed class ModEntry : Mod
    {
        private const double HeartbeatSeconds = 10;
        private const double MaxReportAgeSeconds = 35;
        private const int MaxPanelPlayers = 8;

        private ModConfig _config = new();
        private readonly PerScreen<ScreenState> _screen = new(() => new ScreenState());
        private readonly Stopwatch _time = Stopwatch.StartNew();

        private ScreenState S => _screen.Value;

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

            helper.Events.GameLoop.GameLaunched += (_, _) => ConfigMenu.Register(Helper, ModManifest, _config, Monitor);
            helper.Events.GameLoop.SaveLoaded += (_, _) => S.Reset();
            helper.Events.GameLoop.ReturnedToTitle += (_, _) => S.Reset();
            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.GameLoop.OneSecondUpdateTicked += OnOneSecondUpdateTicked;
            helper.Events.GameLoop.UpdateTicked += (_, _) => S.Trip.Tick();
            helper.Events.Input.ButtonPressed += OnButtonPressed;
            helper.Events.Input.CursorMoved += OnCursorMoved;
            helper.Events.Display.RenderedHud += OnRenderedHud;
            helper.Events.Display.RenderedActiveMenu += OnRenderedActiveMenu;
            helper.Events.Multiplayer.ModMessageReceived += OnModMessageReceived;
            helper.Events.Multiplayer.PeerConnected += OnPeerConnected;
            helper.Events.Multiplayer.PeerDisconnected += OnPeerDisconnected;
        }

        private string T(string key, object? tokens = null) => Helper.Translation.Get(key, tokens);

        // ---- shared rules ------------------------------------------------------

        /// <summary>The host's idle rules win for every player who has the mod. Without a host that has the mod, each player's own settings apply.</summary>
        private int AfkMinutes => !Context.IsMainPlayer && S.HostRules is not null ? Math.Clamp(S.HostRules.AfkMinutes, 1, 30) : _config.AfkMinutes;

        private bool AfkCountsAsReady => !Context.IsMainPlayer && S.HostRules is not null ? S.HostRules.AfkCountsAsReady : _config.AfkCountsAsReady;

        private double Now => _time.Elapsed.TotalSeconds;

        // ---- state -----------------------------------------------------------------

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            // everyone wakes up active, and yesterday's reports no longer match the day key
            S.Clock.NoteActivity();
            S.Warned = false;
            S.Trip.Cancel();
            S.AutoTriedToday = false;
            // yesterday's reports no longer match the day key, so the first report of the new day goes out at once and not at the next heartbeat
            S.LastSent = ActivityState.Unknown;
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

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            NoteActivity();
            if (Context.IsWorldReady && _config.GoToBedKey.JustPressed())
                GoToBedNow();
        }

        private void OnCursorMoved(object? sender, CursorMovedEventArgs e)
        {
            if (_config.CountCursorMovementAsActivity)
                NoteActivity();
        }

        private void NoteActivity()
        {
            S.Clock.NoteActivity();
            S.Warned = false;
        }

        // ---- going to bed --------------------------------------------------------

        /// <summary>The go to bed key. A refusal from the rules is shown to the player.</summary>
        internal SleepBlock GoToBedNow()
        {
            var block = S.Trip.Begin();
            if (block != SleepBlock.None)
                Game1.addHUDMessage(new HUDMessage(T(SleepPolicy.ReasonKey(block)), HUDMessage.error_type));
            return block;
        }

        /// <summary>The idle player's own opt in trip. A refusal is only logged because the player did not ask for it.</summary>
        private void StartAutoTrip()
        {
            var block = S.Trip.Begin();
            if (block == SleepBlock.None)
                Game1.addHUDMessage(new HUDMessage(T("message.auto-sleep")) { noIcon = true });
            else
                Monitor.Log($"Auto sleep did not start ({block}).", LogLevel.Trace);
        }

        // ---- the mod's API ----------------------------------------------------------

        public override object GetApi() => new BedtimeRollCallApi(this);

        internal bool IsGoingToBed => S.Trip.Active;

        internal double IdleSeconds => S.Clock.IdleSeconds;

        internal ActivityState Activity => S.MyActivity;

        internal string[] PanelLines() => S.Hud is null ? Array.Empty<string>() : S.Hud.Lines.Select(l => l.Text).Prepend(S.Hud.Header).ToArray();

        internal void DrawPanel(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch)
        {
            if (S.Hud is not null)
                SleepHud.Draw(spriteBatch, S.Hud, _config);
        }

        // ---- once a second -------------------------------------------------------

        private void OnOneSecondUpdateTicked(object? sender, OneSecondUpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            try
            {
                var s = S;
                var me = Game1.player;
                var tile = me.TilePoint;
                int facing = me.FacingDirection;
                if (s.HasLastPosition && (tile != s.LastTile || facing != s.LastFacing))
                    NoteActivity();
                s.LastTile = tile;
                s.LastFacing = facing;
                s.HasLastPosition = true;

                // waiting in bed, a menu, a cutscene and a festival neither add to the idle time nor reset it
                bool counting = Context.IsPlayerFree && ReadBed(me) == BedState.Out && !Game1.isFestival();
                s.Clock.Advance(1, counting);

                double threshold = AfkMinutes * 60.0;
                s.MyActivity = Game1.eventUp
                    ? ActivityState.InEvent
                    : _config.EnableAfkTracking && s.Clock.IsAfk(threshold) ? ActivityState.Afk : ActivityState.Active;

                // the warning never starts in the first half of the idle time, so a short host rule cannot warn a player the moment they stop
                bool warning = _config.EnableAfkTracking && counting && s.Clock.InWarning(threshold, Math.Min(_config.WarnBeforeAfkSeconds, threshold / 2));
                if (warning && !s.Warned)
                {
                    s.Warned = true;
                    Game1.addHUDMessage(new HUDMessage(T("message.idle-warning", new { seconds = (int)Math.Ceiling(s.Clock.SecondsUntilAfk(threshold)) })) { noIcon = true });
                }

                Broadcast();
                s.Hud = BuildHud(warning ? s.Clock.SecondsUntilAfk(threshold) : -1);

                if (_config.AutoSleepWhenIdle && s.MyActivity == ActivityState.Afk)
                {
                    // only players whose sleep has really started count, so nobody is sent to bed because someone is standing on a bed tile
                    int othersAsleep = Game1.getOnlineFarmers().Count(f => !f.IsLocalPlayer && !f.IsDedicatedPlayer && ReadBed(f) == BedState.Confirmed);
                    if (SleepPolicy.ShouldAutoSleep(true, true, othersAsleep, s.AutoTriedToday, SleepPolicy.Check(BedTrip.Describe())))
                    {
                        s.AutoTriedToday = true;
                        StartAutoTrip();
                    }
                }
            }
            catch (Exception ex)
            {
                Monitor.Log($"Sleep panel update failed and will try again next second. {ex.Message}", LogLevel.Debug);
            }
        }

        private void Broadcast()
        {
            var s = S;
            if (!Context.IsMultiplayer)
                return;
            if (s.MyActivity == s.LastSent && Now - s.LastSentAt < HeartbeatSeconds)
                return;

            s.LastSent = s.MyActivity;
            s.LastSentAt = Now;
            Helper.Multiplayer.SendMessage(
                new StateMessage { State = s.MyActivity, IdleSeconds = s.Clock.IdleSeconds, DayKey = CurrentDayKey() },
                MessageTypes.State,
                new[] { ModManifest.UniqueID });
        }

        private static string CurrentDayKey() => DayKey.Make(Game1.year, Game1.currentSeason, Game1.dayOfMonth);

        // ---- the panel ----------------------------------------------------------

        private HudModel BuildHud(double warnSeconds)
        {
            var s = S;
            string day = CurrentDayKey();
            var farmers = Game1.getOnlineFarmers().Where(f => !f.IsDedicatedPlayer).ToList();
            var activity = new Dictionary<long, (ActivityState State, double Idle)>();
            foreach (var farmer in farmers)
            {
                activity[farmer.UniqueMultiplayerID] = farmer.IsLocalPlayer
                    ? (s.MyActivity, s.Clock.IdleSeconds)
                    : s.Book.Get(farmer.UniqueMultiplayerID, Now, day, MaxReportAgeSeconds);
            }

            var tally = ReadinessTally.Compute(
                farmers.Select(f => new TallyPlayer(f.UniqueMultiplayerID, f.Name, ReadBed(f), activity[f.UniqueMultiplayerID].State)),
                AfkCountsAsReady);

            bool festival = Game1.isFestival();
            string header = festival ? T("panel.festival") : T("panel.header", new { ready = tally.Ready, total = tally.Total });
            Color headerColor = festival ? Palette.Muted : tally.AllReady ? Palette.Good : tally.Blocked.Count > 0 ? Palette.Warn : Palette.Text;

            var lines = new List<HudLine>();
            foreach (var farmer in farmers.Take(MaxPanelPlayers))
            {
                var (state, idle) = activity[farmer.UniqueMultiplayerID];
                lines.Add(DescribePlayer(farmer, ReadBed(farmer), state, idle));
            }
            if (farmers.Count > MaxPanelPlayers)
                lines.Add(new HudLine(T("panel.more", new { count = farmers.Count - MaxPanelPlayers }), Palette.Muted));

            // the game's own count of players who have confirmed sleep, when a sleep check is running
            int required = Game1.netReady.GetNumberRequired("sleep");
            if (required > 0)
                lines.Add(new HudLine(T("panel.game", new { ready = Game1.netReady.GetNumberReady("sleep"), required }), Palette.Muted));

            if (warnSeconds >= 0)
                lines.Add(new HudLine(T("panel.warning", new { seconds = (int)Math.Ceiling(warnSeconds) }), Palette.Warn));

            return new HudModel(header, headerColor, lines);
        }

        private HudLine DescribePlayer(Farmer farmer, BedState bed, ActivityState state, double idleSeconds)
        {
            var name = new { name = farmer.Name };
            if (bed == BedState.Confirmed)
                return new HudLine(T("panel.asleep", name), Palette.Good);
            if (bed == BedState.InBed)
                return new HudLine(T("panel.in-bed", name), Palette.Good);

            return state switch
            {
                ActivityState.Afk => new HudLine(T("panel.idle", new { name = farmer.Name, minutes = Math.Max(1, (int)(idleSeconds / 60)) }), Palette.Warn),
                ActivityState.InEvent => new HudLine(T("panel.event", name), Palette.Alert),
                ActivityState.Unknown => new HudLine(T("panel.untracked", name), Palette.Muted),
                _ => new HudLine(T("panel.up", name), Palette.Text)
            };
        }

        private void OnRenderedHud(object? sender, RenderedHudEventArgs e)
        {
            // the panel is for sessions with other players, and it gets out of the way of every menu and cutscene
            if (!ShouldDraw() || Game1.activeClickableMenu != null || Game1.eventUp)
                return;

            SleepHud.Draw(e.SpriteBatch, S.Hud!, _config);
        }

        /// <summary>The one menu that keeps the panel, the game's own sleep check, because waiting there is when the player wants to see who is still up.</summary>
        private void OnRenderedActiveMenu(object? sender, RenderedActiveMenuEventArgs e)
        {
            if (ShouldDraw() && Game1.activeClickableMenu is ReadyCheckDialog)
                SleepHud.Draw(e.SpriteBatch, S.Hud!, _config);
        }

        private bool ShouldDraw() => _config.EnableHud && S.Hud is not null && Context.IsWorldReady && Context.IsMultiplayer && Game1.displayHUD;

        // ---- messages -----------------------------------------------------------

        private void OnPeerConnected(object? sender, PeerConnectedEventArgs e)
        {
            if (Context.IsMainPlayer)
                SendRules(new[] { e.Peer.PlayerID });
        }

        private void OnPeerDisconnected(object? sender, PeerDisconnectedEventArgs e)
        {
            S.Book.Remove(e.Peer.PlayerID);
            S.RulesSentTo.Remove(e.Peer.PlayerID);
        }

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

                var s = S;
                switch (e.Type)
                {
                    case MessageTypes.State:
                    {
                        var message = e.ReadAs<StateMessage>();
                        if (Enum.IsDefined(message.State))
                            s.Book.Update(e.FromPlayerID, message.State, Math.Max(0, message.IdleSeconds), message.DayKey ?? "", Now);

                        // the rules sent when a player connects arrive before their world is ready and are dropped, so the first report from
                        // a player with the mod is the cue to send them again
                        if (Context.IsMainPlayer && s.RulesSentTo.Add(e.FromPlayerID))
                            SendRules(new[] { e.FromPlayerID });
                        break;
                    }
                    case MessageTypes.Rules:
                        // shared rules come from the host only
                        if (!Context.IsMainPlayer && e.FromPlayerID == Game1.MasterPlayer.UniqueMultiplayerID)
                            s.HostRules = e.ReadAs<RulesMessage>();
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
