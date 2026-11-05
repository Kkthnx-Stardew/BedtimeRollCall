using BedtimeRollCall.Core;
using Microsoft.Xna.Framework.Graphics;

namespace BedtimeRollCall
{
    /// <summary>What other mods and test tools can ask of Bedtime Roll Call through <c>IModRegistry.GetApi</c>.</summary>
    public interface IBedtimeRollCallApi
    {
        /// <summary>The panel as plain text, the header first and then one line per player. Empty before the first update.</summary>
        string[] GetPanelLines();

        /// <summary>Draws the panel where the game would draw it, even while a menu is open.</summary>
        void DrawPanel(SpriteBatch spriteBatch);

        /// <summary>Does what the go to bed key does. Returns "None" when the trip started, or the reason it did not.</summary>
        string GoToBed();

        /// <summary>True while the local player is on the way to bed.</summary>
        bool IsGoingToBed { get; }

        /// <summary>Seconds the local player has been idle, counted by this mod's own rules.</summary>
        double IdleSeconds { get; }

        /// <summary>The local player's state as the others see it, one of Unknown, Active, Afk and InEvent.</summary>
        string Activity { get; }
    }

    public sealed class BedtimeRollCallApi : IBedtimeRollCallApi
    {
        private readonly ModEntry _mod;

        public BedtimeRollCallApi(ModEntry mod) => _mod = mod;

        public string[] GetPanelLines() => _mod.PanelLines();

        public void DrawPanel(SpriteBatch spriteBatch) => _mod.DrawPanel(spriteBatch);

        public string GoToBed() => _mod.GoToBedNow().ToString();

        public bool IsGoingToBed => _mod.IsGoingToBed;

        public double IdleSeconds => _mod.IdleSeconds;

        public string Activity => _mod.Activity.ToString();
    }
}
