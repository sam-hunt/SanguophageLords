# Sanguophage Faction as an optional lair owner

Status: idea, not decided. Depends on the quest shape and on who owns the lair by default, both
still open.

Surveyed 2026-11-24 from the installed Workshop copy of Sanguophage Faction
(`ocarina.sanguophage.faction`, Workshop id 3007992150, its `1.6/` folder).

## What the mod offers

Sanguophage Faction is a small def-only mod that adds one world faction and nothing else: no
quest, site, map generation, buildings, art or code. It does not overlap with the lair.

- **One faction**, "sanguophage hideout": a medieval-tech reskin of the outlander template
  (outlander name makers, trader kinds, raid loot, default settlement icon), 100% Sanguophage
  xenotype. It settles, raids, sieges and sends caravans. Hostile by default but befriendable.
- **Four melee-only pawn kinds** on vanilla's `SanguophageBase` (villager, guard, trader, lord),
  carrying hemogen packs and wearing dark or blood-red apparel.
- **Ideology:** requires the Bloodfeeding and Cannibal memes.
- **Three gated compat roots:** Vanilla Races Expanded - Sanguophage (adds Strigoi, Ekkimian and
  Bruxa to the xenotype mix, plus Alpha Genes' Malachai), Big and Small - Vampires and the Undead
  (two more xenotypes), and House Sanguin (armour and weapons in the gear pool).
- **No assembly.** A `Source/` folder ships, but it is an unfinished stub (an empty Harmony
  postfix and a settings slider that does nothing) and no DLL is built from it.
- **No conflict with us:** every def is `OCARINA_`-prefixed and it patches only its own defs. The
  one shared surface is vanilla's `SanguophageBase` pawn kind, so a third mod patching that
  parent affects their pawns and any lair inhabitants we derive from it.

Footprint, from the Workshop page on the survey date: about 7,360 subscribers and 205 ratings,
posted July 2023, tagged 1.4 to 1.7, last updated 12 October 2026 (the 1.7 tag) after an August
2026 change to the ideology memes. Its `1.7/` folder is byte-identical to `1.6/`.

The other mods in this space are the ones it patches for, VRE - Sanguophage the most prominent.
None of them has been surveyed.

## The idea

Vanilla Biotech has no sanguophage faction, so the lair's inhabitants need an owner from
somewhere. When Sanguophage Faction is active and its faction exists in the world, the lair could
belong to that faction instead of our default owner. Players running both mods would then see the
lair as part of a faction they already know, at no cost to players without it.

Nothing in our design needs this. It is a soft, one-way compat: we read their def, they know
nothing about us.

## What the other mod gives us to hook

- One `FactionDef`, `OCARINA_SanguophageFaction`, labelled "sanguophage hideout". The defName is
  the same in its 1.4, 1.5, 1.6 and 1.7 folders.
- Four pawn kinds on vanilla's `SanguophageBase`: `OCARINA_SanguophageVillager`,
  `OCARINA_SanguophageTown_Guard`, `OCARINA_SanguophageTown_Trader`, `OCARINA_SanguophageLord`
  (the faction leader). All melee only, medieval weapon tags.
- The faction is `naturalEnemy`, not a permanent enemy, so a player can befriend it. It has
  trader kinds and peaceful pawn groups.
- With Ideology, its ideoligion requires the Bloodfeeding and Cannibal memes.
- It defaults to one instance per world (`requiredCountAtGameStart` 1), but the player can remove
  it at world creation, so the def being loaded does not mean a faction instance exists.
- It ships no assembly and patches only its own defs.

## Suggested shape

- Resolve the def by name with a silent-fail lookup, the same way the quest-weight dial looks up
  the quest def today. No hard reference, no About.xml dependency, no load-order rule.
- Use it as owner only when a live instance exists in the world and it is hostile to the player.
  Otherwise fall back to the default owner.
- Keep our own pawn kinds for the inhabitants and assign them to that faction. The lair's
  difficulty then stays ours to balance and does not move when their def values change.
- No compat root is needed while this stays a pure C# lookup with no strings of its own. If it
  ever grows XML (a patch on their defs, a label that names their faction), that content goes in
  a gated `1.6/Mods/Sanguophage Faction/` root, per the optional-content rule in CLAUDE.md.

## Open questions

- **Allied faction.** If the player has befriended the faction, raiding its lair would be an
  attack on an ally. Falling back to the default owner avoids it, but then the lair's owner
  changes with diplomacy. Decide whether that is acceptable or whether an allied faction should
  suppress the lair quest instead.
- **Ideoligion carry-over.** Pawns generated for a faction take its ideoligion, so captured
  inhabitants would bring Bloodfeeding and Cannibal into the colony. Probably fitting, but it is
  a difference from the default owner.
- **Odyssey landmark entrance.** That entrance has no quest and no site faction. Parenting there
  would only touch the pocket map's inhabitants.
- **Engine paths unverified.** How a site's faction reaches a pocket map's inhabitants, and what
  goodwill effects follow from attacking them, have not been decompile-checked for this purpose.
- **Their compat xenotypes.** With VRE - Sanguophage or Big and Small active, their faction's
  xenotype set gains extra xenotypes. Whether our inhabitants should follow the faction's set or
  stay plain sanguophages is a design call.
- **Maintenance risk.** The mod is lightly maintained (the author said in November 2026 they are
  not much interested in modding at present). A silent-fail lookup degrades to the default owner
  if the def ever disappears, so the risk is low.
