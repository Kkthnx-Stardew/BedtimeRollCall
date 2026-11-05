namespace BedtimeRollCall.Core
{
    /// <summary>What a player is doing, as that player's own copy of the mod reports it. The game does not sync any of this.</summary>
    public enum ActivityState
    {
        /// <summary>Nothing was heard from that player's mod, so nothing is known. Never shown as active or idle.</summary>
        Unknown = 0,
        Active = 1,
        /// <summary>No input for the threshold. Only counts as ready when the host allows it.</summary>
        Afk = 2,
        /// <summary>In a cutscene or event, which holds the night back and is never idle.</summary>
        InEvent = 3
    }

    /// <summary>Where a player is in the game's own sleep steps. Every player's copy of the game can read this, mod or not.</summary>
    public enum BedState
    {
        Out = 0,
        /// <summary>Standing on a bed tile or in a tent. Not yet the game's confirmed sleep.</summary>
        InBed = 1,
        /// <summary>The game has started this player's sleep (<c>timeWentToBed</c> is set), so they are waiting for the others.</summary>
        Confirmed = 2
    }
}
