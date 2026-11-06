# Design notes

Bedtime Roll Call for Stardew Valley 1.6.15 (build 24356) and SMAPI 4.2.1 or newer. The starting point was a research blueprint with a list of open questions. Every row below was read from the decompiled 1.6.15 game code, not taken from the blueprint.

## What the game code says

| Question | Answer from 1.6.15 |
|---|---|
| Is there a network sleep ready object | Yes. `GameLocation.startSleep()` calls `Game1.netReady.SetLocalReady("sleep", true)` and opens `ReadyCheckDialog("sleep", allowCancel: true)`. `ReadySynchronizer` exposes `GetNumberReady`, `GetNumberRequired` and `IsReady` for an id. Per player states are internal, so only the counts are readable. The blueprint said no such object existed |
| What marks a player as confirmed asleep | `Farmer.timeWentToBed` is set to the time of day in `startSleep()`, set back to 0 when the player cancels, and set to 0 again at the day update. `Farmer.isInBed` is recomputed every tick from the bed tile property or a temporary bed. Both are net fields. `isInBed && timeWentToBed > 0` is the confirmed signal |
| How does the game put a farmer to bed without a click | The game's own dedicated server does `Game1.player.position.Set(bedSpot * 64)` then `farmHouse.answerDialogueAction("Sleep_Yes", null)` (`DedicatedServer.HostSleepInBed`). The go to bed code makes the same two calls |
| What if the home has no bed | `FarmHouse.GetPlayerBedSpot()` falls back to the front door, so the mod refuses and says so |
| Who is required for the sleep check | `NumberRequired` starts as `Game1.getOnlineFarmers().Count` |
| Is there a built in dedicated server | Yes, new in 1.6.15. `Game1.IsDedicatedHost` and `Farmer.IsDedicatedPlayer`. The fake host player is left out of every player list |
| What sits around the clock | The buffs row runs to the left of the clock along the top (`BuffsDisplay`). The journal button hangs under the clock at 240 pixels down, and its new quest arrow is drawn 8 pixels under the button, 64 pixels tall (`DayTimeMoneyBox`). Nothing in the game uses the top left corner |
| How the game draws a tooltip | The tan `menuTexture` box from source rectangle (0, 256, 60, 60), 16 pixels of padding, dark brown text (`IClickableMenu.drawHoverText`) |

## Scope of 0.1.0

1. **A panel** that lists every online player as in bed and ready, in bed, idle, in an event, up, or not tracked, with the count of players whose sleep has started and the game's own count. It uses the game's tooltip box and colours chosen to read on it. It is also drawn over the game's sleep screen.
2. **An idle tracker.** It counts time without input and without moving, pauses while a menu or event is open, while in bed, and on festival days, warns before it marks the player, and tells the other players. A player whose mod is quiet for 35 seconds, or who reports for another day, shows as not tracked and never as idle or active. The warning never starts in the first half of the idle time.
3. **A go to bed key** that takes your own character to your own bed and starts the game's sleep check, where Cancel works.
4. **Go to bed when idle**, off by default and opt in per player. Needs the player idle, at least one other player whose sleep has really started, and a free player. One try a day.
5. **Host rules.** The host's idle minutes and the idle counts as ready choice apply to every player who has the mod. The host sends them on connect and again after a player's first report, because the first send arrives before that player's world is ready and is dropped.
6. **A public API** for other mods and for the test harness.
7. **A settings page** for the Generic Mod Config Menu, with every setting, and translations in `i18n`.

## Decisions

- **No vote and no host "force advance".** Both mean changing another player's character or patching the game's sleep barrier. The rule is that the mod only ever acts on the local player's own character, through the game's own sleep path. Idle players are put to bed by their own copy of the mod, never by someone else.
- **Ready means the game has started the player's sleep.** Standing on a bed tile does not count, because a player can stand there while still deciding, or after cancelling. The go to bed key works from that spot.
- **Single player is refused.** The sleep check that lets a player back out only exists in co-op.
- **The panel is top left by default.** The top right is crowded with the clock, the journal button and arrow, the buffs and other mods' icons. Under the clock is an option that clears the arrow, and the offsets move the panel anywhere.
- **No Harmony and nothing saved.**

## Rules the code follows

- Bed state is readable by every player, so the panel works with no mod on the other players. Idle state needs the mod on that player, and without it the panel says "not tracked".
- An idle player counts as ready in the panel only when the host allows it, and that never makes the game end the night.
- A player in an event who is not in bed is held back, and never counted as ready or idle.
- The panel hides during menus except the sleep screen, during cutscenes, when the game's HUD is hidden, and only shows when another player is online.
- Messages are checked. The sender must be an online player, the day key must match, an unreadable message is dropped with a debug line, and rules are accepted from the host only.
- The trip to bed is refused in an event, on a festival day, in the mines or the volcano, in a menu, once the night is ending, when already asleep, and when the home has no bed.

## Verified in the real game

Two game processes on one PC, a throwaway farm, run by `tools/Run-RollCallTest.ps1`.

- The panel on both sides, with bed states, the game's own count and the sleep screen showing.
- The go to bed key from the farm. It warps home, opens the sleep check, refuses a second press, and Cancel works.
- The key works again when standing on the bed after cancelling.
- The host's idle rule reaching the joining player, who has a different setting of their own.
- Go to bed when idle. The joining player did nothing for the idle minute, put itself to bed, and the day changed on both players.
- A joining player without the mod. The host sees their bed state, sees "not tracked" while they are up, and the night still ends when both are asleep.
- The panel in the game's tooltip box, drawn in a frame with the clock, the journal button and its new quest arrow, in both positions. Under the clock leaves a clear gap below the arrow. A first picture caught the text hidden under the box, which is fixed.

## Not tested yet

- The settings page in the Generic Mod Config Menu has been built but not opened in the game.
- Split screen. The state is kept per screen with `PerScreen`, but it has not been run.
- A host without the mod, with a joining player who has it.
- The dedicated server mode.
- Controller only play, 125 and 150 percent UI scale, and other mods' icons under the clock.
- Mac and Linux.
