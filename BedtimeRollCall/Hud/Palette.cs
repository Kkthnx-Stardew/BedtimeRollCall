using Microsoft.Xna.Framework;
using StardewValley;

namespace BedtimeRollCall.Hud
{
    /// <summary>Text colours picked to read on the game's tan tooltip box, where the bright colours of a dark panel would wash out.</summary>
    internal static class Palette
    {
        /// <summary>The game's own tooltip text colour.</summary>
        public static Color Text => Game1.textColor;

        /// <summary>In bed and ready, everyone ready.</summary>
        public static readonly Color Good = new(20, 110, 40);

        /// <summary>Idle, or someone holding the night back.</summary>
        public static readonly Color Warn = new(176, 92, 0);

        /// <summary>In an event.</summary>
        public static readonly Color Alert = new(165, 35, 35);

        /// <summary>Not tracked, festival, and the game's own count.</summary>
        public static readonly Color Muted = new(105, 85, 65);
    }
}
