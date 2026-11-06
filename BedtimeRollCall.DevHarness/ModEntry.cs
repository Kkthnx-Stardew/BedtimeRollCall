using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Network;

namespace BedtimeRollCall.DevHarness
{
    /// <summary>The slice of the Bedtime Roll Call API the scenario uses.</summary>
    public interface IRollCallApi
    {
        string[] GetPanelLines();
        void DrawPanel(SpriteBatch spriteBatch);
        string GoToBed();
        bool IsGoingToBed { get; }
        double IdleSeconds { get; }
        string Activity { get; }
    }

    /// <summary>
    /// Two player scenario for Bedtime Roll Call, run inside the real game. One process hosts a throwaway farm (BRC_ROLE=host) and one joins it
    /// (BRC_ROLE=client). They meet at named barriers through small files in BRC_SYNC. Results go to BRC_OUT, panel pictures next to it.
    /// Does nothing when BRC_OUT is not set.
    /// </summary>
    public sealed class ModEntry : Mod
    {
        private const string TargetMod = "Kkthnx.BedtimeRollCall";
        private static string Role => Environment.GetEnvironmentVariable("BRC_ROLE") ?? "";
        private static bool IsHost => Role == "host";
        private static string OtherRole => IsHost ? "client" : "host";

        private string? _out;
        private IEnumerator<int>? _run;
        private int _wait, _ticks, _exitAt = -1;
        private readonly Dictionary<string, object?> _results = new();
        private IRollCallApi _api = null!;
        private int _cursorMoves, _buttons;

        public override void Entry(IModHelper helper)
        {
            _out = Environment.GetEnvironmentVariable("BRC_OUT");
            if (string.IsNullOrWhiteSpace(_out))
            {
                Monitor.Log("BRC_OUT is not set, so the harness is idle.", LogLevel.Info);
                return;
            }
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            // counted so a stretch of unexpected activity can be told apart from a fault in the idle clock
            helper.Events.Input.CursorMoved += (_, _) => _cursorMoves++;
            helper.Events.Input.ButtonPressed += (_, _) => _buttons++;
        }

        // ---- plumbing -----------------------------------------------------------

        private string SyncFile(string name, string role) => Path.Combine(Environment.GetEnvironmentVariable("BRC_SYNC")!, name + "." + role);
        private void Mark(string name) => File.WriteAllText(SyncFile(name, Role), DateTime.Now.ToString("O"));
        private bool OtherHas(string name) => File.Exists(SyncFile(name, OtherRole));

        private IEnumerable<int> Barrier(string name, int limitTicks = 7200)
        {
            Mark(name);
            for (int t = 0; t < limitTicks && !OtherHas(name); t += 5)
                yield return 5;
            if (!OtherHas(name))
                _results["barrier_timeout_" + name] = true;
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            _ticks++;

            // the game skips its update while its window is inactive, which would ruin every timed step. Runtime only, not saved
            if (Game1.options != null && Game1.options.pauseWhenOutOfFocus)
                Game1.options.pauseWhenOutOfFocus = false;
            if (Game1.musicCategory != null && _ticks % 30 == 0)
            {
                Game1.musicCategory.SetVolume(0f);
                Game1.soundCategory?.SetVolume(0f);
                Game1.ambientCategory?.SetVolume(0f);
                Game1.footstepCategory?.SetVolume(0f);
            }

            if (_exitAt >= 0)
            {
                if (_ticks >= _exitAt)
                    Environment.Exit(0);
                return;
            }

            if (_run is null)
            {
                if (Game1.activeClickableMenu is TitleMenu title && _ticks > 240)
                {
                    var api = Helper.ModRegistry.GetApi<IRollCallApi>(TargetMod);
                    // the joining player may run without the mod on purpose (BRC_NOMOD), to check what the host sees
                    if (api is null && !(Environment.GetEnvironmentVariable("BRC_NOMOD") == "1" && !IsHost))
                    {
                        _results["error"] = "Bedtime Roll Call API not found";
                        Finish("no api");
                        return;
                    }
                    _api = api!;
                    _run = (IsHost ? HostMain(title) : ClientMain(title)).GetEnumerator();
                }
                else if (_ticks > 3600)
                    Finish("timeout waiting for the title menu");
                return;
            }

            if (_wait > 0)
            {
                _wait--;
                return;
            }

            try
            {
                if (!_run.MoveNext())
                    Finish(null);
                else
                    _wait = Math.Max(0, _run.Current - 1);
            }
            catch (Exception ex)
            {
                _results["harness_error"] = ex.ToString();
                Finish("harness error");
            }
        }

        private void Finish(string? note)
        {
            _results["note"] = note;
            _results["mods"] = Helper.ModRegistry.GetAll().Select(m => m.Manifest.UniqueID + " " + m.Manifest.Version).ToList();
            File.WriteAllText(_out!, JsonSerializer.Serialize(_results, new JsonSerializerOptions { WriteIndented = true }));
            Monitor.Log($"Harness results written to {_out}.", LogLevel.Info);
            Game1.quit = true;
            _exitAt = _ticks + 300;
        }

        // ---- starting the two games ----------------------------------------------

        private IEnumerable<int> HostMain(TitleMenu title)
        {
            Game1.player.Name = "Host";
            Game1.player.displayName = "Host";
            Game1.player.farmName.Value = "BRCTest";
            Game1.player.favoriteThing.Value = "Testing";
            Game1.player.isCustomized.Value = true;
            Game1.player.ConvertClothingOverrideToClothesItems();
            Game1.options.ipConnectionsEnabled = true;
            Game1.multiplayerMode = 2;
            title.createdNewCharacter(skipIntro: true);

            for (int t = 0; t < 3600 && !(Context.IsWorldReady && Context.CanPlayerMove && !Game1.fadeToBlack); t++)
                yield return 1;
            if (!Context.IsWorldReady)
            {
                _results["world"] = "host never became ready";
                yield break;
            }
            yield return 120;

            var cabin = PrepareCabin();
            _results["cabin"] = cabin is null ? "none" : "ready for " + cabin.owner?.UniqueMultiplayerID;
            if (cabin is null)
                yield break;
            Mark("hostready");

            for (int t = 0; t < 9000 && Game1.otherFarmers.Count == 0; t += 15)
                yield return 15;
            _results["clientJoined"] = Game1.otherFarmers.Count > 0;
            yield return 300;

            foreach (int w in Night()) yield return w;
        }

        private IEnumerable<int> ClientMain(TitleMenu title)
        {
            title.skipToTitleButtons();
            for (int t = 0; t < 9000 && !OtherHas("hostready"); t += 30)
                yield return 30;
            yield return 120;

            var multiplayer = Helper.Reflection.GetField<Multiplayer>(typeof(Game1), "multiplayer").GetValue();
            var client = multiplayer.InitClient(new LidgrenClient("localhost"));
            var menu = new FarmhandMenu(client);
            TitleMenu.subMenu = menu;

            int sinceTry = 9999;
            for (int t = 0; t < 9000 && !(Context.IsWorldReady && Context.CanPlayerMove); t += 15)
            {
                sinceTry += 15;
                if (menu.MenuSlots.Count > 0 && sinceTry > 300)
                {
                    sinceTry = 0;
                    menu.MenuSlots[0].Activate();
                }
                yield return 15;
            }
            if (!Context.IsWorldReady)
            {
                _results["world"] = "client never reached the world";
                yield break;
            }
            yield return 240;

            foreach (int w in Night()) yield return w;
        }

        /// <summary>A cabin with a customized farmhand in it, the way a host who invited a friend would have it.</summary>
        private Cabin? PrepareCabin()
        {
            var farm = Game1.getFarm();
            var cabin = farm.buildings.Select(b => b.GetIndoors() as Cabin).FirstOrDefault(c => c is not null);
            if (cabin is null)
            {
                foreach (var tile in new[] { new Vector2(58, 20), new Vector2(52, 20), new Vector2(46, 20), new Vector2(40, 22), new Vector2(60, 28) })
                {
                    if (farm.buildStructure("Cabin", tile, Game1.player, out var built, magicalConstruction: true, skipSafetyChecks: true))
                    {
                        cabin = built.GetIndoors() as Cabin;
                        break;
                    }
                }
            }
            if (cabin is null)
                return null;
            if (!cabin.HasOwner)
                cabin.CreateFarmhand();
            var hand = cabin.owner;
            if (hand is not null && !hand.isCustomized.Value)
            {
                hand.isCustomized.Value = true;
                hand.Name = "Client";
                hand.displayName = "Client";
                hand.farmName.Value = Game1.player.farmName.Value;
                hand.hair.Value = 2;
                hand.skin.Value = 1;
            }
            return cabin;
        }

        // ---- the scenario --------------------------------------------------------

        private void Snapshot(string step, Dictionary<string, object?> info)
        {
            info[step + "_lines"] = _api.GetPanelLines();
            info[step + "_sleepCheck"] = $"{Game1.netReady.GetNumberReady("sleep")}/{Game1.netReady.GetNumberRequired("sleep")}";
            info[step + "_me"] = $"{Game1.player.Name} in {Game1.player.currentLocation?.NameOrUniqueName}, isInBed {Game1.player.isInBed.Value}, timeWentToBed {Game1.player.timeWentToBed.Value}, menu {Game1.activeClickableMenu?.GetType().Name ?? "none"}";
            Shot(step);
        }

        /// <summary>Draws the clock box and the panel into an off-screen target and saves it, so the result can be read the way a player sees it.</summary>
        private void Shot(string step)
        {
            int width = Game1.uiViewport.Width, height = Game1.uiViewport.Height;
            var b = Game1.spriteBatch;
            var target = new RenderTarget2D(Game1.graphics.GraphicsDevice, width, height);
            Game1.graphics.GraphicsDevice.SetRenderTarget(target);
            Game1.graphics.GraphicsDevice.Clear(new Color(70, 100, 70));
            b.Begin(SpriteSortMode.FrontToBack, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, null);
            Game1.dayTimeMoneyBox.draw(b);
            // the worst case under the clock, the journal button with its new quest arrow, drawn the way the game draws them
            var quest = Game1.dayTimeMoneyBox.questButton;
            quest.draw(b);
            b.Draw(Game1.mouseCursors, new Vector2(quest.bounds.Left - 16, quest.bounds.Bottom + 8), new Rectangle(128, 208, 16, 16), Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.9f);
            Game1.activeClickableMenu?.draw(b);
            _api.DrawPanel(b);
            b.End();
            Game1.graphics.GraphicsDevice.SetRenderTarget(null);
            string dir = Path.GetDirectoryName(_out!)!;
            using (var fs = File.Create(Path.Combine(dir, $"rc-{Role}-{step}.png")))
                target.SaveAsPng(fs, width, height);
            target.Dispose();
        }

        private IEnumerable<int> WaitForDialog(int limitTicks, Dictionary<string, object?> info, string key)
        {
            int t = 0;
            while (t < limitTicks && Game1.activeClickableMenu is not ReadyCheckDialog)
            {
                t++;
                yield return 1;
            }
            info[key] = Game1.activeClickableMenu is ReadyCheckDialog ? $"dialog after {t} ticks" : "no dialog";
        }

        /// <summary>The game's own way into bed, the one a player without the mod uses: stand on the bed spot and answer the sleep question.</summary>
        private static void VanillaSleep()
        {
            var home = (FarmHouse)Game1.currentLocation;
            var bed = home.GetPlayerBed() ?? home.GetBed();
            Game1.player.Position = Utility.PointToVector2(bed.GetBedSpot()) * 64f;
            home.answerDialogueAction("Sleep_Yes", null);
        }

        /// <summary>The joining player's half of the night when it has no mod. The host's half is the normal one and runs the same barriers.</summary>
        private IEnumerable<int> NoModNight(Dictionary<string, object?> info, int day0)
        {
            yield return 700;
            foreach (int w in Barrier("a")) yield return w;
            VanillaSleep();
            foreach (int w in WaitForDialog(900, info, "b_dialog")) yield return w;
            foreach (int w in Barrier("b")) yield return w;
            foreach (int w in Barrier("b2")) yield return w;

            yield return 120;
            if (Game1.activeClickableMenu is ReadyCheckDialog dialog)
            {
                var center = dialog.cancelButton.bounds.Center;
                dialog.receiveLeftClick(center.X, center.Y, true);
                yield return 90;
            }
            var door = (Game1.currentLocation as FarmHouse)?.getEntryLocation() ?? new Point(3, 11);
            Game1.player.setTileLocation(new Vector2(door.X, door.Y));
            yield return 90;
            foreach (int w in Barrier("c")) yield return w;
            foreach (int w in Barrier("c2")) yield return w;

            // the host is in bed now, so this player goes too and the night ends
            yield return 120;
            VanillaSleep();
            for (int t = 0; t < 7200 && Game1.dayOfMonth == day0; t += 60)
                yield return 60;
            info["d_day_changed"] = Game1.dayOfMonth != day0;
            info["d_day_now"] = Game1.dayOfMonth;
            yield return 600;
            foreach (int w in Barrier("e")) yield return w;
        }

        private IEnumerable<int> Night()
        {
            var info = new Dictionary<string, object?>();
            _results["night_" + Role] = info;
            int day0 = Game1.dayOfMonth;
            info["day_at_start"] = day0;
            info["time_at_start"] = Game1.timeOfDay;
            if (_api is null)
            {
                foreach (int w in NoModNight(info, day0)) yield return w;
                yield break;
            }

            // A. both up, a few heartbeats exchanged
            yield return 700;
            Snapshot("a_start", info);
            foreach (int w in Barrier("a")) yield return w;

            // B. the client goes to bed with the go to bed key's own code, starting from the farm so the warp is covered
            if (!IsHost)
            {
                Game1.warpFarmer("Farm", 64, 15, false);
                for (int t = 0; t < 600 && !(Game1.currentLocation is Farm && !Game1.fadeToBlack && !Game1.isWarping); t++)
                    yield return 1;
                yield return 60;
                info["b_before_trip"] = Game1.currentLocation?.NameOrUniqueName;
                info["b_trip_result"] = _api.GoToBed();
                foreach (int w in WaitForDialog(1200, info, "b_dialog")) yield return w;
                yield return 90;
                Snapshot("b_client_in_bed", info);
                info["b_second_press"] = _api.GoToBed();
            }
            foreach (int w in Barrier("b")) yield return w;
            if (IsHost)
            {
                yield return 240;
                Snapshot("b_host_sees_client", info);
            }
            foreach (int w in Barrier("b2")) yield return w;

            // C. the client cancels on the game's own cancel button, then walks out of the bed
            if (!IsHost && Game1.activeClickableMenu is ReadyCheckDialog dialog)
            {
                var center = dialog.cancelButton.bounds.Center;
                dialog.receiveLeftClick(center.X, center.Y, true);
                yield return 90;
                Snapshot("c_cancelled", info);

                // still standing on the bed after cancelling, the key has to work again and not say the player is already in bed
                info["c_press_on_bed"] = _api.GoToBed();
                foreach (int w in WaitForDialog(900, info, "c_dialog_again")) yield return w;
                yield return 120;
                if (Game1.activeClickableMenu is ReadyCheckDialog again)
                {
                    var again2 = again.cancelButton.bounds.Center;
                    again.receiveLeftClick(again2.X, again2.Y, true);
                    yield return 90;
                }
                info["c_after_second_cancel"] = $"timeWentToBed {Game1.player.timeWentToBed.Value}, menu {Game1.activeClickableMenu?.GetType().Name ?? "none"}";
                var door = (Game1.currentLocation as FarmHouse)?.getEntryLocation() ?? new Point(3, 11);
                Game1.player.setTileLocation(new Vector2(door.X, door.Y));
                yield return 90;
                Snapshot("c_out_of_bed", info);
            }
            foreach (int w in Barrier("c")) yield return w;
            if (IsHost)
            {
                yield return 120;
                Snapshot("c_host_sees_client_up", info);
                // the host spawned on its bed, so it steps off before going to bed on purpose
                var hostDoor = (Game1.currentLocation as FarmHouse)?.getEntryLocation() ?? new Point(3, 11);
                Game1.player.setTileLocation(new Vector2(hostDoor.X, hostDoor.Y));
                yield return 90;
            }
            foreach (int w in Barrier("c2")) yield return w;

            // D. the host goes to bed, the client does nothing and has to put itself to bed after the idle minute
            int clientIdleStartTick = _ticks;
            if (IsHost)
            {
                info["d_trip_result"] = _api.GoToBed();
                foreach (int w in WaitForDialog(1200, info, "d_dialog")) yield return w;
            }
            var seen = new List<string>();
           
            for (int t = 0; t < 7200 && Game1.dayOfMonth == day0; t += 60)
            {
                if (t % 600 == 0)
                    seen.Add($"+{t / 60}s {string.Join(" | ", _api.GetPanelLines())} | trip {_api.IsGoingToBed} | idle {_api.IdleSeconds:0}s {_api.Activity} | cursor {_cursorMoves} keys {_buttons} | menu {Game1.activeClickableMenu?.GetType().Name ?? "none"}");
                if (!IsHost && _api.IsGoingToBed && !info.ContainsKey("d_auto_trip_started_after_s"))
                    info["d_auto_trip_started_after_s"] = (_ticks - clientIdleStartTick) / 60;
                yield return 60;
            }
            info["d_timeline"] = seen;
            info["d_day_changed"] = Game1.dayOfMonth != day0;
            info["d_day_now"] = Game1.dayOfMonth;

            // E. the new day, the panel starts over
            yield return 600;
            Snapshot("e_new_day", info);
            foreach (int w in Barrier("e")) yield return w;
        }
    }
}
