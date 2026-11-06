namespace BedtimeRollCall.Hud
{
    /// <summary>Where the panel sits. The top right is crowded, with the clock, the journal button and its new quest arrow, the buffs row and often other mods' icons.</summary>
    public enum PanelPosition
    {
        /// <summary>The top left corner, which nothing in the game uses.</summary>
        TopLeft = 0,

        /// <summary>Under the clock, below the journal button and the space its new quest arrow needs.</summary>
        UnderClock = 1
    }
}
