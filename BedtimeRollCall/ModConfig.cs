using BedtimeRollCall.Hud;
using StardewModdingAPI.Utilities;

namespace BedtimeRollCall
{
    /// <summary>Settings in config.json. The host's idle rules (threshold and whether idle counts as ready) apply to every player who has the mod.</summary>
    public sealed class ModConfig
    {
        /// <summary>Show the sleep panel under the clock.</summary>
        public bool EnableHud { get; set; } = true;

        /// <summary>Size of the panel, 0.8 to 1.4.</summary>
        public float HudScale { get; set; } = 1f;

        /// <summary>TopLeft is the default because the top right holds the clock, the journal button, the buffs and other mods' icons. UnderClock sits below the journal button.</summary>
        public PanelPosition PanelPosition { get; set; } = PanelPosition.TopLeft;

        /// <summary>Moves the panel right by this many pixels from where its position puts it. Negative moves it left.</summary>
        public int PanelOffsetX { get; set; }

        /// <summary>Moves the panel down by this many pixels. Negative moves it up.</summary>
        public int PanelOffsetY { get; set; }

        /// <summary>Track this player's idle time and tell the others. Needed for idle players to show and to count as ready.</summary>
        public bool EnableAfkTracking { get; set; } = true;

        /// <summary>Minutes without input before a player counts as idle, 1 to 30.</summary>
        public int AfkMinutes { get; set; } = 5;

        /// <summary>Seconds before that when the player is warned, 0 for no warning.</summary>
        public int WarnBeforeAfkSeconds { get; set; } = 60;

        /// <summary>Mouse movement counts as input. Turn off if a controller or a bumped mouse keeps a player marked active.</summary>
        public bool CountCursorMovementAsActivity { get; set; } = true;

        /// <summary>An idle player counts as ready in the panel. It does not make the game end the night, everyone still has to be in bed, passed out or gone.</summary>
        public bool AfkCountsAsReady { get; set; } = false;

        /// <summary>Takes you to your own bed from anywhere and starts the game's sleep check, which has a Cancel button. Co-op only.</summary>
        public KeybindList GoToBedKey { get; set; } = KeybindList.Parse("F8");

        /// <summary>Send this player to bed by themselves when they are idle and at least one other player is already in bed. Off unless you turn it on, and it only ever acts on your own character.</summary>
        public bool AutoSleepWhenIdle { get; set; } = false;

        /// <summary>Puts every setting inside its allowed range, so a hand edited file cannot break the mod.</summary>
        public void Normalize()
        {
            HudScale = float.IsFinite(HudScale) ? Math.Clamp(HudScale, 0.8f, 1.4f) : 1f;
            if (!Enum.IsDefined(PanelPosition))
                PanelPosition = PanelPosition.TopLeft;
            PanelOffsetX = Math.Clamp(PanelOffsetX, -1000, 1000);
            PanelOffsetY = Math.Clamp(PanelOffsetY, -1000, 1000);
            AfkMinutes = Math.Clamp(AfkMinutes, 1, 30);
            WarnBeforeAfkSeconds = Math.Clamp(WarnBeforeAfkSeconds, 0, AfkMinutes * 60);
        }
    }
}
