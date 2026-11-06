# Bedtime Roll Call

A Stardew Valley mod for co-op sessions with friends. A small panel under the clock shows who is in bed and who has gone quiet, so nobody waits at the sleep screen wondering. An optional key takes you to your own bed from anywhere, and an optional setting does it for you when you are idle and a friend is already in bed.

It uses no Harmony patches and saves nothing, so removing it leaves the save exactly as vanilla.

Status, `0.1.0`, a first beta. Needs Stardew Valley 1.6.15 and SMAPI 4.2.1 or newer.

## What it does

- **The panel.** Under the clock, one line per online player, such as "Alice, in bed and ready", "Bob, idle 6m", "Carol, in an event" or "Dave, not tracked". The header counts the players whose sleep has started. It also shows while you wait at the game's sleep screen.
- **Idle tracking.** After a few minutes with no input and no movement, your copy of the mod tells the others you are idle. It warns you first. Time spent in a menu, an event, a festival or in bed does not count.
- **Go to bed key.** `F8` by default. Takes you to your own bed and opens the game's normal sleep check, where you can press Cancel. Co-op only.
- **Go to bed when idle.** Off by default. When you are idle and at least one other player has already started sleeping, you are sent to your own bed. Once a day, only for your own character, and you can cancel.
- **Host rules.** The host's idle minutes and the idle counts as ready choice apply to every player who has the mod.

Bed state shows for every player whether or not they have the mod. Idle state needs the mod on that player, and without it the panel says "not tracked".

## What it never does

- It never moves or changes another player's character.
- It never ends the night for people who are not in bed. An idle player can show as ready in the panel if the host allows it, but the game still needs everyone asleep.
- It never sends you to bed from an event, a festival, the mines, a menu, or when your home has no bed.

## Settings

Every setting is in the Generic Mod Config Menu if you have it, or in `config.json`.

| Setting | Default | What it does |
|---|---|---|
| `EnableHud` | on | Show the panel |
| `HudScale` | 1.0 | Panel size, 0.8 to 1.4 |
| `EnableAfkTracking` | on | Track your idle time and tell the others |
| `AfkMinutes` | 5 | Minutes without input before you count as idle, 1 to 30. The host's value is used on a joined game |
| `WarnBeforeAfkSeconds` | 60 | Warn this many seconds before, 0 for no warning |
| `CountCursorMovementAsActivity` | on | Mouse movement counts as input |
| `AfkCountsAsReady` | off | An idle player counts as ready in the panel. The host's value is used on a joined game |
| `GoToBedKey` | F8 | The go to bed key. Controller players can bind a button here |
| `AutoSleepWhenIdle` | off | Go to bed by yourself when idle and a friend is already asleep |

## For other mods

`IModRegistry.GetApi<IBedtimeRollCallApi>("Kkthnx.BedtimeRollCall")` gives the panel text, a way to draw the panel, the go to bed action and the local idle state. The interface is in `BedtimeRollCall/BedtimeRollCallApi.cs`.

## Translations

Text lives in `BedtimeRollCall/i18n/default.json`. A translation is a copy of that file named for the language, such as `fr.json`.

## Design and findings

See `docs/DESIGN.md` for what was checked in the 1.6.15 game code, how going to bed works, and what has and has not been tested.

## Building and testing

```bash
dotnet build BedtimeRollCall.slnx -c Release
dotnet test BedtimeRollCall.Tests/BedtimeRollCall.Tests.csproj
```

The build does not copy into the game's Mods folder. The zip lands in `BedtimeRollCall/bin/Release/net6.0/`.

A real two player test runs with `tools/Run-RollCallTest.ps1`. It starts a host and a joining player on one PC with a throwaway farm, runs a night, and saves the panel as pictures. `-ClientWithoutMod` makes the joiner run without the mod. The live Mods folder and your saves are never touched.
