using BedtimeRollCall.Core;
using Microsoft.Xna.Framework;
using Netcode;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;

namespace BedtimeRollCall.Sleep
{
    /// <summary>
    /// Takes the local player to their own bed and starts the game's own sleep. It follows the path the game's dedicated server uses for its
    /// host: stand on the home bed spot, then answer the "Sleep_Yes" question. In co-op that opens the game's ready check, which has a Cancel
    /// button, so the player can always back out. Nothing here changes a save or a game method.
    /// </summary>
    internal sealed class BedTrip
    {
        private const int WarpTimeoutTicks = 600;
        private const int SettleTicks = 45;

        private enum Phase { Idle, Warping, Settling }

        private Phase _phase = Phase.Idle;
        private int _ticks;

        public bool Active => _phase != Phase.Idle;

        public void Cancel() => _phase = Phase.Idle;

        public static SleepContext Describe() => new(
            IsMultiplayer: Context.IsMultiplayer,
            NewDayStarting: Game1.newDay || Game1.killScreen || Game1.player.passedOut,
            InEvent: Game1.eventUp,
            IsFestival: Game1.isFestival(),
            InDungeon: Game1.currentLocation is StardewValley.Locations.MineShaft or VolcanoDungeon,
            Sleeping: Game1.player.isInBed.Value && Game1.player.timeWentToBed.Value > 0,
            PlayerFree: Context.IsPlayerFree && !Game1.fadeToBlack && !Game1.isWarping,
            HasBed: FindBed(FindHome()) is not null);

        private static FarmHouse? FindHome() => Game1.getLocationFromName(Game1.player.homeLocation.Value) as FarmHouse;

        /// <summary>The bed the game treats as the player's own, or any bed in the home. A home with no bed is refused, because the game's fallback spot is the front door.</summary>
        private static BedFurniture? FindBed(FarmHouse? home) => home?.GetPlayerBed() ?? home?.GetBed();

        /// <summary>Starts a trip. Returns why it could not, or <see cref="SleepBlock.None"/> when it began.</summary>
        public SleepBlock Begin()
        {
            if (Active)
                return SleepBlock.Busy;

            var block = SleepPolicy.Check(Describe());
            if (block != SleepBlock.None)
                return block;

            var home = FindHome()!;
            if (Game1.currentLocation == home)
            {
                _phase = Phase.Settling;
            }
            else
            {
                var door = home.getEntryLocation();
                Game1.warpFarmer(home.NameOrUniqueName, door.X, door.Y, false);
                _phase = Phase.Warping;
            }
            _ticks = 0;
            return SleepBlock.None;
        }

        /// <summary>Called every game tick while a trip runs. Returns false when the trip ended or gave up.</summary>
        public bool Tick()
        {
            if (_phase == Phase.Idle)
                return false;

            var home = FindHome();
            if (home is null || Game1.newDay || Game1.eventUp)
            {
                Cancel();
                return false;
            }

            _ticks++;
            if (_phase == Phase.Warping)
            {
                if (_ticks > WarpTimeoutTicks)
                {
                    Cancel();
                    return false;
                }
                if (Game1.currentLocation == home && !Game1.fadeToBlack && !Game1.isWarping)
                {
                    _phase = Phase.Settling;
                    _ticks = 0;
                }
                return true;
            }

            // a short pause after arriving so the player is fully in the room, then the same two calls the game's dedicated server makes
            if (_ticks < SettleTicks)
                return true;

            _phase = Phase.Idle;
            if (SleepPolicy.Check(Describe() with { PlayerFree = true }) != SleepBlock.None || Game1.activeClickableMenu != null)
                return false;

            var bed = FindBed(home);
            if (bed is null)
                return false;
            Game1.player.Position = Utility.PointToVector2(bed.GetBedSpot()) * 64f;
            // a jump, not a walk, so the other players see the farmer on the bed and not gliding there (the game does the same when it places a farmer in a bed)
            (Game1.player.NetFields.Root as NetRoot<Farmer>)?.CancelInterpolation();
            home.answerDialogueAction("Sleep_Yes", null);
            return false;
        }
    }
}
