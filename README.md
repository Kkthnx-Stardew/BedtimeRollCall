# Co-op Sleep and AFK Toolkit

A Stardew Valley mod for co-op sessions with friends. It shows who is in bed and who is idle, and it tells the other players when someone has gone quiet. It changes nothing in the game and saves nothing, so removing it leaves the save exactly as vanilla.

Status, `0.1.0`, in development and not released. Built and unit tested. Not yet tested with two game processes.

## What it does

- A panel under the clock lists every online player, such as "Alice, in bed and ready", "Bob, idle 6m", "Carol, in an event", "Dave, not tracked".
- An idle tracker marks a player after a few minutes with no input and no movement, warns them first, and sends that to the other players.
- The host's idle rules apply to everyone who has the mod.

Bed state works for every player whether or not they have the mod. Idle state needs the mod on that player.

## Settings

| Setting | Default | What it does |
|---|---|---|
| `EnableHud` | on | Show the panel |
| `HudScale` | 1.0 | Panel size, 0.8 to 1.4 |
| `EnableAfkTracking` | on | Track this player's idle time and tell the others |
| `AfkMinutes` | 5 | Minutes without input before a player counts as idle, 1 to 30 |
| `WarnBeforeAfkSeconds` | 60 | Warn this many seconds before, 0 for no warning |
| `CountCursorMovementAsActivity` | on | Mouse movement counts as input |
| `AfkCountsAsReady` | off | An idle player counts as ready in the panel. The game still needs everyone in bed, passed out or gone |

## Design and findings

See `docs/DESIGN.md` for what was checked in the 1.6.15 game code and what is not built yet.

## Building and testing

```bash
dotnet build CoopSleepAfk.slnx -c Release
dotnet test CoopSleepAfk.Tests/CoopSleepAfk.Tests.csproj
```

The build does not copy into the game's Mods folder. The zip lands in `CoopSleepAfk/bin/Release/net6.0/`.
