# Design notes

Bedtime Roll Call for Stardew Valley 1.6.15 (build 24356) and SMAPI 4.2.1 or newer. The starting point was a research blueprint with a list of open questions. Every row below was read from the decompiled 1.6.15 game code, not taken from the blueprint.

## What the game code says

| Question | Answer from 1.6.15 |
|---|---|
| Is there a network sleep ready object | Yes. `GameLocation.startSleep()` calls `Game1.netReady.SetLocalReady("sleep", true)` and opens `ReadyCheckDialog("sleep")`. `ReadySynchronizer` exposes `GetNumberReady`, `GetNumberRequired` and `IsReady` for an id. Per player states are internal, so only the counts are readable. The blueprint said no such object existed |
| What marks a player as confirmed asleep | `Farmer.timeWentToBed` is set to the time of day in `startSleep()`, set back to 0 when the player cancels, and set to 0 again at the day update. `Farmer.isInBed` is recomputed every tick from the bed tile property or a temporary bed. Both are net fields. `isInBed && timeWentToBed > 0` is the confirmed signal |
| Who is required for the sleep check | `NumberRequired` starts as `Game1.getOnlineFarmers().Count` |
| Is there a built in dedicated server | Yes, new in 1.6.15. `Game1.IsDedicatedHost` and `Farmer.IsDedicatedPlayer`, with its own sleep logic in `DedicatedServer`. The fake host player must be left out of every player list |
| Festival check | `Game1.isFestival()` and `Utility.isFestivalDay()` |
| Kick a player | The host chat command `/kick` (`ChatCommands`, main player and multiplayer only) and `Game1.server.kick(long)` |
| Multiplayer time pause | `Game1.netWorldState.Value.IsTimePaused` |
| SMAPI peer events | `PeerContextReceived`, `PeerConnected`, `PeerDisconnected` and `ModMessageReceived` all exist and compile |
| UI scale for the panel | `Game1.uiViewport` gives the size in the space the HUD draws in. The clock box size is the static `DayTimeMoneyBox.width` and `DayTimeMoneyBox.height` |

## Scope of 0.1.0

Built, from the blueprint's safe ladder:

1. A panel under the clock that lists every online player as in bed and ready, in bed, idle, in an event, up, or not tracked, with the count of players ready for bed and the game's own count of confirmed sleepers.
2. An idle tracker. It counts time without input and without moving, pauses while a menu or event is open, while in bed, and on festival days, warns before it marks the player, and tells the other players. A player whose mod is quiet for 35 seconds, or who reports for another day, shows as not tracked and never as idle or active.
3. The host's idle rules (minutes, and whether idle counts as ready) apply to every player who has the mod.

Not built yet, in this order. Voting. Host advance with a visible countdown. Self sleep, where an idle player's own copy puts that player to bed. The blueprint's rule stays. Nothing writes another player's fields, nothing patches the sleep barrier, no Harmony, nothing saved.

## Rules the code follows

- Bed state is readable by every player, so the panel works with no mod on the other players. Idle state needs the mod on that player, and without it the panel says "not tracked".
- An idle player counts as ready in the panel only when the host allows it, and that never makes the game end the night. Everyone still has to be in bed, passed out or gone.
- A player in an event who is not in bed is held back, and never counted as ready or idle.
- The panel hides during menus and cutscenes, and only shows when another player is online.
- Messages are checked. The sender must be an online player, the day key must match, an unreadable message is dropped with a debug line, and rules are accepted from the host only.

## Open and still to test in the game

- Two game processes with the panel on both, a player going to bed, getting up, going idle, entering an event and leaving. Rendered to an image and read.
- A host with the mod and a farmhand without it, and the reverse.
- Split screen. The idle clock and panel state are not per screen yet.
- The dedicated server mode and how its fake host shows up in `getOnlineFarmers()`.
- Controller only play, with the cursor setting off.
- 125 and 150 percent UI scale.
