# Lair mapgen harness

A throwaway dev mod and launcher that show what the lair generator actually builds. It boots
the installed game on a pinned mod list with no human input, starts a quick-test colony,
spawns an ancient stairwell, generates the `SL_Lair` pocket map behind it for a given seed,
writes a screenshot of the whole map, and quits. Nothing here ships (the staging manifest
only takes game content folders) and nothing here is built by the solution.

```bash
# The game must be closed. Build and deploy the mod first (any normal build does).
Scripts/lair-harness/try.sh 4242 150                       # seed, map size
SL_DLC=royalty,ideology,anomaly Scripts/lair-harness/try.sh 77 150
```

`try.sh` wraps `run.sh` with retries, because the 1.7 game sometimes freezes during startup.
One run takes 35 to 45 seconds. Outputs land in `/tmp/sl-harness/out/` (override the folder
with `SL_HARNESS_WORK`):

| File | What it is |
|---|---|
| `view.png` | The rendered map, fog cleared and lit, cropped and scaled to 2000 px. The one to look at. |
| `sl-lair.schematic.png` | One coloured block per cell by terrain and thing category. Works even if rendering does not. |
| `sl-lair.ascii.txt` | The same as text. |
| `harness.log` | The harness's own lines: map stats, whether the exit linked, cells reachable from the exit. |
| `Player-full.log` | The game's whole log. Grep it for `[Sanguophage Lords]`, `ERROR` and `Exception`. |

In dev mode the mod itself logs, per generated lair, any room that needed its doorways
unblocked or roof columns added, and the power net's supply against demand.

Switches (environment variables, see the head of `run.sh`): `SL_DLC` adds DLC to the pinned
list (Biotech is always on), `SL_PR=1` adds Harmony and Progress Renderer, `SL_PORTAL=0`
generates without a stairwell, `SL_TIMEOUT`, `SL_MAXPX`, `SL_ROOFS`, `SL_BRIGHT`,
`SL_CLEARFOG`.

How it works: `run.sh` builds `SLHarness.csproj` against the installed game's assemblies,
copies the mod into the game's `Mods/SLHarness`, swaps `ModsConfig.xml` for a pinned list
(the original is restored on exit, and by the next run if this one was killed), launches the
game with `-quicktest`, waits for the harness's `DONE` line, then crops the screenshot.

`SL_MODS` adds other installed mods to the pinned list by packageId, in load order, ahead of
the lair. The hemogen pipe network, for example, needs Vanilla Races Expanded - Sanguophage
and what it depends on:

```bash
SL_MODS=brrainz.harmony,oskarpotocki.vanillafactionsexpanded.core,vanillaracesexpanded.sanguophage \
  Scripts/lair-harness/try.sh 77 150
```
