# CLAUDE.md

Guidance for Claude Code (claude.ai/code) when working in this repository.

## Project Overview

**Sanguophage Lords** is a RimWorld 1.6/1.7 mod adding a rare world quest site: a sanguophage lair
entered through a custom portal onto a generated pocket map (a custom MapGeneratorDef subtree).
Requires the Biotech DLC (hard dependency — the lair is built on Biotech's
sanguophage xenotype, genes, and hemogen systems).

**One tree serves both game versions.** `1.6/` and the `Source/1.6/` net472 assembly are loaded
by 1.6 and by 1.7 (whose Waylith runtime maps every `UnityEngine` reference onto its
`UnityStubs`), with `LoadFolders.xml` naming the same roots under each version. This is Phase A
of the family guide `../RIMWORLD_1.7_MIGRATION.md`; do not add a `1.7/` folder or retarget the
project outside that plan. The plan for this mod (decided 2026-11-24): stay dual-version while
1.7 is on Steam's `unstable` branch only, and go 1.7-only (the guide's Phase C) if 1.7 reaches
Steam's main branch before the 1.0.0 release. A change must therefore work on both versions, and
a `<v1.7>` root must be added wherever a `<v1.6>` one is.

**Key technologies:** C# (.NET Framework 4.7.2), RimWorld modding API, XML defs.

### Where documentation lives

**This file holds only cross-cutting rules and rationale.** Per-item values, tuning numbers and
decompile-verified call paths live in the header comment of the file they describe — every `.cs`
file and every non-obvious def carries one. When adding or changing something, put the *why*
there and only add a line here if it constrains work in other files. Do not restate def values
or call paths here; they drift.

**Comments describe the present, git describes the past.** A header or code comment explains the
current state where the code does not make it obvious: engine facts it relies on, what a number is
balanced against, cross-file coupling. It carries no dates, no previous values, no record of what
was tried and dropped, and no "seen in game" notes; that history belongs in the commit message
and diff. Comments never ship (see Deployment), so a def header can carry its full rationale at
no download cost.

## Build Commands

```bash
# Build the mod (outputs to 1.6/Assemblies/ AND atomically redeploys to the RimWorld Mods folder)
dotnet build SanguophageLords.sln -c Release

# Build only the main project (also triggers the deploy)
dotnet build Source/1.6/SanguophageLords.csproj

# Run tests
dotnet test Tests/1.6/SanguophageLords.Tests.csproj

# Clean build artifacts
dotnet clean SanguophageLords.sln

# Stage the mod into an arbitrary folder (used by CI; same manifest as the local deploy)
dotnet build Source/1.6/SanguophageLords.csproj -c Release \
  -t:StageMod -p:StageDir=/path/to/output/SanguophageLords

# Override RimWorld install path
RIMWORLD_PATH="/path/to/RimWorld" dotnet build SanguophageLords.sln -c Release
# Or: dotnet build -p:RimWorldPath="/path/to/RimWorld"
```

The build compiles against the pinned `Krafs.Rimworld.Ref` 1.6 package everywhere, locally and in
CI, never against the local install (a 1.7 install carries no 1.6 assemblies). The RimWorld
installation path, auto-detected on Windows/Linux/Mac or set by `RIMWORLD_PATH` (in WSL the
Windows path), only decides deployment and serves as the decompile target for API inspection
(monodis, ilspycmd), where it is the source of truth for the version it is.

### Deployment

The repo lives in `~/dev/SanguophageLords`, separate from the RimWorld Mods folder. Every local
build redeploys automatically and atomically — there is no separate clean step to remember.

- **Single source of truth:** what ships is the `_ModFiles` ItemGroup in the `StageMod` target
  (`Source/1.6/SanguophageLords.csproj`) — the only place to edit the manifest. It whitelists by
  file type per content folder (`About`, `Assemblies`, `Defs`, `Patches`, `Languages`,
  `Textures`, plus `Sounds`/`AssetBundles` if ever added), matched at the root and under any
  version folder, so a new content folder of an existing type deploys automatically and only a
  brand-new file type needs a new line. Gated compat load roots (see LoadFolders.xml) sit two
  levels deep and need an explicit pattern per content type: `Defs`/`Patches`/`Languages`/
  `AssetBundles` are matched under the version-specific `<version>/Mods/<Mod Name>/`, and
  `Textures` under the version-independent root `Mods/<Mod Name>/` (only the first flavour
  exists here today). A type a compat root does not yet cover deploys nothing, with no build
  error. Only game-loaded types are listed (e.g. `.xml`), so stray dev notes (`README.md`,
  `RESEARCH.md`) never ship.
- **Comments never ship:** after the copy, `StageMod` rewrites every staged `.xml` without its
  comments (the inline `StripXmlComments` task beside it), so def headers cost players nothing
  and can be as long as they need to be. The repo files are untouched: the translation checker
  and the tests read the repo, while the game, and so the smoke test, loads the stripped copy.
  The Steam upload is this same local deploy, so it ships stripped too.
- **Self-cleaning:** `StageMod` wipes `$(StageDir)` and recopies from source, so renamed/deleted
  files never linger. The post-build `DeployToModFolder` target calls it with
  `StageDir = $RIMWORLD_PATH/Mods/SanguophageLords` (only when a local RimWorld install is
  detected).
- **CI reuses the same target:** `.github/workflows/release.yml` invokes `StageMod` with
  `-p:StageDir=<release dir>` instead of its own `cp` list, so the release zip can't drift from
  the local deploy. Triggers on `v*.*.*` tags.
- **Stop hook (`.claude/hooks/sync-mod.sh`):** after each turn,
  rebuilds+redeploys only when mod source/content actually changed (doc-only turns are a fast
  no-op) and, on build failure, exits 2 with the errors on stderr so Claude Code feeds them back and the
  turn continues (a second failure in the same turn, `stop_hook_active`, only warns, so it cannot
  loop). Mechanism details are in the script's own header. Its `find` watch list must cover every content root `StageMod` ships (root,
  any version folder, and the compat roots `Mods/` and `*/Mods/`), or edits under a missed root
  silently stop redeploying. Logs to `$TMPDIR/SanguophageLords-build.log`. Tracked and wired by
  the tracked `.claude/settings.json`; the script is byte-identical across the mod family and
  derives the solution, project folder and mod name itself, so change it in the template and
  copy it verbatim, never per repo. It bails when no RimWorld install is found, so CI and
  contributors without the game are unaffected.

**Releases:** Push a tag matching `v*.*.*` to trigger the release workflow
(`.github/workflows/release.yml`), or use the `release` skill which walks the whole process.

**`.claude/` is only partly gitignored.** `.gitignore` carries `.claude/*` followed by
`!.claude/skills/`, `!.claude/hooks/` and `!.claude/settings.json`, so the skills, the Stop hook
and its wiring are tracked and shared while `settings.local.json` (personal permissions) stays
local per machine. Editing a skill is therefore a committed, team-visible change and must keep in step
with whatever it automates: `/release`'s step 7 encodes this repo's CHANGELOG layout and the
version scheme (release candidates are `X.Y.Z-rc.N` tags, CHANGELOG-less and Workshop-less, with
the suffix in `modVersion` and `AssemblyInformationalVersion` only; `release.yml` treats any
suffixed tag as a prerelease to match), and `/translate`'s glossary encodes per-language
terminology decisions. Changing the thing without changing the skill leaves an instruction
pointing at something that no longer exists, and nothing fails until the next release run.

**WSL Setup:** Requires `RIMWORLD_PATH` env var in `~/.bashrc` pointing to the Windows RimWorld
install (e.g., `/mnt/c/Program Files (x86)/Steam/steamapps/common/RimWorld`). Deployment keys
on that folder's `Mods/` directory, which every game version and platform has.

### Tests

xUnit suite under `Tests/1.6/` (a separate project, never shipped). Run natively:

```bash
dotnet test Tests/1.6/SanguophageLords.Tests.csproj
```

vstest hosts the net472 suite via mono automatically. CI builds but doesn't run it. The release
skill runs the suite and a Release build as its first gate.

Run tests natively from WSL — never build from the Windows toolchain: it corrupts the WSL-side
incremental state (shared `obj/` seen under different path roots). The test csproj copies the
game's real 1.6 runtime DLLs beside the test DLL so tests may load Verse types (see the
Assembly-CSharp-firstpass comment there — mono resolves field types eagerly where the Windows CLR
is lazy). It finds them in `RIMWORLD_MANAGED_1_6`, a 1.6 install's `Managed/` folder, or the
frozen copy at `~/.local/share/RimWorld-1.6/Managed` (the csproj documents the lookup); a 1.7
install has none, and without any source the build stays green on reference-only stubs while
tests that load game types fail. `OutputPath` is Release-gated so Debug `dotnet test` builds
can't overwrite the shipped DLL in `1.6/Assemblies`.

If a native test failure looks runtime-flavored, suspect assembly resolution first: a DLL missing
from the test bin copy target throws `BadImageFormatException`/`TypeLoadException` under mono
only — and one such failure can poison static state and surface as unrelated value mismatches
downstream.

**Startup smoke test (pre-release):** `python3 Scripts/integration-smoke-test.py` (game closed)
boots the mod on its pinned minimal list, on whichever game version the selected Steam branch
installs, then classifies Player.log errors by origin and fails on anything attributed to this
mod. Run before every release (wired into the release skill); thin shim over the shared engine
in `l10n/smoke/` (born from the BetterTradersGuild v1.1.0 CWTL incident).

## Architecture

### Entry Point

`Source/1.6/Core/SanguophageLordsMod.cs` — `Mod` subclass whose constructor wires settings.
`Source/1.6/Core/SL_Startup.cs` (`[StaticConstructorOnStartup]`) runs after defs load and applies
settings that override def fields. **Settings access:** `SanguophageLordsMod.Settings`.

**Harmony-free by policy:** the mod ships no Harmony wiring at all — no package reference, no
About.xml dependency, no `PatchAll`. The planned feature set is achievable through defs and
vanilla extension points. If a patch ever becomes genuinely unavoidable, the same change that
adds the first patch class must re-add Lib.Harmony (`ExcludeAssets=runtime`) to the csproj and
`brrainz.harmony` to About.xml (modDependencies + loadAfter), and observe the hazard below. The
patch-pattern rules later in this file (Prefix discipline, private patch targets, `*Patches`
namespaces) apply from that moment.

**Patch-timing hazard (other mods' methods):** `PatchAll()` run from a `Mod` subclass
constructor executes BEFORE any defs are loaded. Applying a detour JIT-compiles the target and
runs its declaring type's static ctor, so a patch targeting ANOTHER MOD's method can permanently
break that mod when its cctor resolves defs (the BetterTradersGuild v1.1.0 CWTL incident). If
patches ever land here, keep foreign-target patches off the ctor-time pass — worked example:
BetterTradersGuild's `Core/DeferredModPatches.cs`.

### Naming conventions

- Def prefix and Keyed prefix: `SL_`. Defs live at `1.6/Defs/<DefTypeName>Defs/<Name>.xml`, one
  def (or small tight group) per file. Patches at `1.6/Patches/<Target>_<OptionalModSuffix>.xml`.
- English Keyed strings: `1.6/Languages/English/Keyed/SL_UI.xml`; split by surface
  (`SL_Combat.xml`, ...) only when a second surface appears.
- Settings are partial classes: `Core/SanguophageLordsSettings.cs` owns the window frame and row
  helpers; `Core/Settings/Settings_<Area>.cs` each own their section's fields, scribe entries,
  defaults, Apply* def-writes and draw method.
- DefOf handles: `Core/SL_DefOf.cs` for general defs; a feature-scoped `SL_<Feature>DefOf` beside
  the feature when one grows large (UMW precedent). Only add defs the code actually looks up,
  each field commented with its exact C# consumer.

### Key Patterns

**Namespace Convention:** Use `*Patches` suffix for patch namespaces to avoid RimWorld type
conflicts (e.g., `SitePatches`, `MapGenerationPatches`).

**Comments:** In `Source/`, use plain `//` comments only — do **not** write XML doc comments
(`///`, `<summary>`, `<param>`, etc.); nothing consumes them there, so they add ceremony without
benefit. `Tests/` is exempt: XML doc comments are fine there (they read cleanly on the test
helpers and are the most likely place to adopt a tool that parses them).

**No `?.`/`??`/`??=` on Unity objects:** Never use null propagation or null coalescing on receivers
deriving from `UnityEngine.Object` (`Material`, `Texture`, `RenderTexture`, `GameObject`, ...).
Unity overloads `==` so destroyed objects compare equal to null; `?.` bypasses the overload with
a raw reference check and then throws `MissingReferenceException` on the member access. Use
explicit `== null`/`!= null` guards for those types. Verse types (`Thing`, `Pawn`, `ThingComp`,
defs) are plain classes where `?.` is fine. Enforced at build time by UNT0007/UNT0008/UNT0023
(Microsoft.Unity.Analyzers). Corollary: never bulk-apply Roslynator's RCS1146 (use conditional
access) fixer to Unity-typed receivers; see the note in `.editorconfig`.

**Warnings are build errors.** The csproj files set `TreatWarningsAsErrors`, so every compiler and
analyzer warning fails the build, locally, in the Stop hook and in CI. `.editorconfig` severities
at `warning` block the build; `suggestion` is IDE-only.

**Logging:** Prefix mod-specific logs with the mod name — `Log.Message("[Sanguophage Lords] ...")`.

**Serialized Fields:** Use camelCase for fields serialized via `Scribe_Values.Look` to match save
file XML element names (per .editorconfig). PascalCase for all other public members.

**Settings Triple Invariant:** Every settings field must appear in three places with matching
defaults: (1) field declaration, (2) `Reset*Settings()`, (3) `Expose*Settings()`'s
`Scribe_Values.Look` default. Missing a spot fails silently — drops from save, skips reset, or
drifts from declared default. Each section's partial file keeps all three in the UI's display
order so a diff across the three blocks lines up row-for-row.

**Label casing (vanilla convention):** thing/def labels placed mid-sentence in player-facing text
use the lowercase form (`LabelShort`, `.label`) — vanilla renders "Pick up revolver x1", never
"Pick up Revolver x1". Keyed strings carry their own sentence-start capital; where a `{0}`
placeholder can begin the sentence, `CapitalizeFirst()` the composed string instead of
capitalizing the argument. `LabelCap`/`LabelShortCap` is for standalone display (list rows, name
fields) and proper nouns.

**Player-facing text follows `l10n/languages/English.md`** (def labels and descriptions,
`Keyed/`, `About.xml`, `.steamworkshop/`): read it before writing or changing any. Its
mechanical rules (no dashes, semicolons, "Whether" openers or parenthetical asides) are checked
by `Scripts/check-translations.py` as `[player text]` warnings, which `--strict` turns into
release errors. This file, code comments and def comments are unaffected.

**Prefix discipline:** Prefer Postfix. A Prefix should only set flags or pre-align state,
returning void/true. Skipping the original (`return false`) is a mod-compat hazard — other mods'
transpilers and the original's side effects silently die with it — so before writing one, check
whether an additive Postfix can express the change. When a skip is genuinely unavoidable, scope
it tightly to SL-owned defs/state and preserve any vanilla effects other patches may rely on.

**Private patch targets:** resolve via a cached `AccessTools.Method` with
`[HarmonyPrepare]`/`[HarmonyTargetMethod]`, never a string-named attribute: on API drift, Prepare
skips just that one patch instead of `PatchAll` throwing and aborting every later patch.

**Reflection self-checks:** when the first string-named reflection site lands, adopt the family
pattern (see BetterTradersGuild's `Core/ReflectionVerification.cs` and its CLAUDE.md section):
each reflecting class caches its own `FieldInfo`/`MethodInfo` and exposes `VerifyReflection()`;
one central `VerifyAll()` triggers them from `SL_Startup` so API drift surfaces at startup.

### Headline feature: the lair quest site

The lair itself is built; the quest that leads to it is not. The planned shape: a rare opportunity-site quest (`QuestScriptDef` +
`SitePartDef` + `WorldObjectDef`) places the lair on the world map; the surface map holds a
custom portal building (a `MapPortal` subclass) that generates and links a pocket map; a
dedicated `MapGeneratorDef` subtree (custom `GenStepDef`s, layout defs) builds the lair itself.

**Shipped so far:** the portal pair (`SL_AncientStairwell`/`SL_AncientStairwellExit`, stony-
stuffable — `1.6/Defs/ThingDefs/AncientStairwell.xml` + `Source/1.6/MapPortals/`); the
lair pocket map itself (`SL_Lair`, see "Lair map generation" below); and
the **Odyssey landmark entrance**: the `SL_SanguophageLair` tile mutator (compat root
`1.6/Mods/Odyssey/`, offered on Odyssey landmarks by `Patches/Landmarks_Odyssey.xml`) whose
worker (`Source/1.6/TileMutators/`) spawns the `SL_StairwellSurface` prefab, re-stuffed in the
map's dominant rock (`Source/1.6/MapGen/LairRock.cs`), on every map generated from a tile
carrying it. This is the Odyssey analogue of the ancient-stockpile hatch and exists so the
lair mapgen can be iterated before the quest shape is decided. Decompile-verified call paths
live in those files' headers. Still pending: the quest/site defs (shape undecided; they also
bring the non-Odyssey surface spawner), and the surface variant of the lair (the same
`LairPlan` set into a mountain, with static defences in place of the underground approach).

### Lair map generation

The lair is planned, then stamped, then furnished, and each stage only knows the one before it.
Details live in the headers of the files named; the rules here are the ones that cross files.

- **Plan (`Source/1.6/MapGen/Plan/`) is pure.** `LairPlanner` turns a `LairPlanParams` into a
  `LairPlan` (an abstract cell grid plus a room graph) using only `Verse.Rand`, `IntVec3`,
  `CellRect` and `Rot4`. Nothing under `Plan/` may reference `Map`, a def or a `Thing`: that
  purity is what lets `Tests/1.6/LairPlannerTests.cs` check plans over many seeds, and what will
  let the surface variant stamp the same plan into a mountain. Set `SL_PLAN_DUMP=<folder>` when
  running the tests to get each seed's plan as ASCII.
- **Stamp, then furnish, as gensteps** (`1.6/Defs/GenStepDefs/LairSteps.xml` is the pipeline
  and its order table): `GenStep_LairStructure` plans and stamps and publishes a `LairSite`
  (generation-time only, never saved); later steps read `LairSite.Current` and must do nothing
  when it is null (a map too small for a lair still generates, as a bare cave).
- **Room kinds and the lair's program are data.** A `LairRoomDef` (one file per room under
  `1.6/Defs/LairRoomDefs/`) says how the planner may shape and site a room and names the
  `LairRoomWorker` that furnishes it; `LairLayoutDef` (`SL_Lair`) lists the spine and the
  priority-ordered program. Room and layout defs carry no player-facing strings, which is why
  an optional room needs no compat root: its program line is gated with `MayRequire`, its def
  loads everywhere, and its worker reaches DLC content through a `[MayRequire...]` DefOf field
  and returns early when the DLC is inactive. Adding a room is a def file, a worker and a
  program line (`Tests/1.6/LairDefXmlTests.cs` fails if any of the three is missing or
  misnamed).
- **Workers place only through `LairRoom`** (`Source/1.6/MapGen/Rooms/LairRoom.cs`): its local
  frame (`At`, `Turn`), its `TrySpawn*` family, floors and torches. Those calls are what keep
  doorways clear, footprints inside the room and every lair in one style (`LairStyle`), so a
  worker never calls `GenSpawn` for furniture and never hardcodes a stone, colour or map
  direction. A worker lays out its ideal room and lets `TrySpawn` drop what does not fit.
- **Thick roof is a design constraint.** Every cell of the pocket map is `RoofRockThick`, which
  falls where it is more than 6.9 cells from a wall or column once a nearby holder is lost. The
  planner leaves rock pillars in the cavern, workers put columns in rooms 13 or more across,
  and the contents step adds columns to any room that still has an unsupported cell.
- **Seeing it:** a room's look can only be judged in the game. `Scripts/lair-harness/`
  (README there) is a dev-only harness mod and launcher that boots the game on a pinned list,
  generates `SL_Lair` for a seed and writes a full-map screenshot; it is not part of the
  solution and never ships. Unit tests cover the plan, the conduit router and the def
  cross-references, not appearance.

**In-family precedent — read before designing:** BetterTradersGuild's smuggler's den quest →
cargo vault pocket map is the full worked example
(`../BetterTradersGuild/Source/1.6/`): `QuestNodes/QuestNode_BTG_SmugglersDen_CreateSite.cs`,
`MapPortals/CargoVaultHatch.cs` (a `MapPortal` that overrides `GetExtraGenSteps` and tears the
pocket map down on `DeSpawn`), `LayoutWorkers/CargoVault/` (vanilla layout workers fail at
pocket-map sizes), `GenSteps/`, and the `Patches/MapParent/MapParentMapGeneratorDef.cs` swap.
UniqueMeleeWeapons' warband quest (`../UniqueMeleeWeapons/Source/1.6/Quests/`) is the cleaner
minimal opportunity-site quest. The quest-weight settings dial is already wired
(`Core/Settings/Settings_Quests.cs`) and switches from `GetNamedSilentFail` to an `SL_DefOf`
handle once the quest def ships.

## Localization

English (Keyed files + def fields) is the source of truth; other languages derive from it via the
`/translate` skill (`.claude/skills/translate/SKILL.md` — this mod's translation surface,
grounding domain, and glossary; family-wide process lives in the `l10n/` submodule, see below)
and are validated deterministically by `python3 Scripts/check-translations.py` (also a CI release
gate). The DefInjected expected set is the checked-in sidecar `Scripts/expected-injections.json`:
a dump of every injection point the *live* game sees for this mod — including vanilla-inherited
fields and C#-default comp strings that never appear in this repo's XML — produced by
`Scripts/refresh-translation-expectations.py` driving the L10nProbe dev mod (source lives at
`l10n/probe/`; build/deploy it only from the canonical `~/dev/rimworld-l10n` checkout) through
the game's own walker. The checker refuses to run against stale expectations, so new content
forces a regen; the release skill regenerates every release. The public language roster lives in
CONTRIBUTING.md and must move in the same commit as any language change. New-machine note: the
probe only dumps mods ticked in its own settings
(`Config/Mod_L10nProbe_L10nProbeMod.xml` on the Windows side) — an absent dump despite a correct
pinned list means the packageId isn't registered there.

- **Shared l10n toolkit (`l10n/` submodule):** the family-wide translation process, per-language
  mechanics references, cross-language lessons, Workshop conventions, and the checker/refresh
  script engines live in the `rimworld-l10n` repo, consumed here as the `l10n/` git submodule
  (canonical working checkout: `~/dev/rimworld-l10n`). `Scripts/check-translations.py` and
  `Scripts/refresh-translation-expectations.py` are thin per-repo config shims over its engines.
  If `l10n/` is empty, run `git submodule update --init`. Never edit `l10n/` in place here:
  mod-independent learnings go upstream in the canonical checkout; mod-specific learnings go in
  this repo's skill/glossary. Upstream ships as semver release tags (`vMAJOR.MINOR.PATCH`; a
  major means this repo's shim or flow needs an edit), and the pin here moves only at release
  (release skill step 3), at the start of a translation pass, or when a new major lands, never
  per upstream commit, so `git submodule status` names the pinned tag and a stable repo's log
  stays free of pin bumps.
- **Workshop title coupling:** each language's `SL_SettingsCategory` Keyed value is the localized
  Steam Workshop title and must equal the title line (line 1) of
  `.steamworkshop/Description/<Language>.txt` — always change the two together (English keeps
  `Sanguophage Lords` in both).
- **Optional-DLC content:** MayRequire is honored on defs but IGNORED on DefInjected entries, so
  content whose strings depend on an optional DLC/mod must ship from a LoadFolders-gated compat
  root (`1.6/Mods/<Name>/` with its own `Defs`/`Patches`/`Languages` inside). One exists today:
  `1.6/Mods/Odyssey/` (the landmark tile mutator and its landmark patch; Biotech, the hard
  dependency, needs none). A compat root's language files must never reuse a main-tree file's
  language-relative path (the game dedups per mod by that path and silently skips one whole
  file); suffix compat-root filenames with the gate's name. The pinned mod lists in
  `Scripts/integration-smoke-test.py` and `Scripts/refresh-translation-expectations.py` carry
  every gate's DLC so the smoke boot and the probe actually see the gated content.
- **Policy:** translation generation passes run only on explicit request (they are
  token-expensive), one language at a time. Infra/tooling changes are always fine.

## Debugging

For reading `Player.log` or disassembling the RimWorld API, use the `rimworld-logs` skill, which
carries both versions' log and assembly paths. Logging convention:
`Log.Message("[Sanguophage Lords] ...")`.

The local install is whichever Steam branch is selected, so its assembly is the decompile target
for that version only. A "decompile-verified" call path in a def or code header was verified on
1.6 unless it says otherwise; when one is re-verified on 1.7 and differs, record both in the
header.

**Iterating on the lair mapgen (dev mode, Odyssey active):** on a colony map, Dev menu → Map →
`AddMutatorToCurrentMapTile` → `SL_SanguophageLair`, then `RegenerateCurrentMap`, forces the
surface entrance onto the current map; `RegenerateMapWithLandmark` (any landmark patched in
`Landmarks_Odyssey.xml`) instead rerolls the tile's mutators as world gen would. On the world
map, `Set landmark` / `Clear Landmark` place landmarks by hand. `GeneratePocketMap` → `SL_Lair`
is no use for the lair itself: that action makes a 100-cell map, too small for the layout, so
it logs a warning and generates a bare cave (and with no portal the exit falls back to slate
and vanilla `PocketMapExit` logs "could not find map portal", both expected there). To see a
real lair, enter a stairwell or use the harness under `Scripts/lair-harness/`.
