using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SanguophageLords;

// The one faction every lair's inhabitants belong to: SL_LairSanguophages
// (1.6/Defs/FactionDefs/SL_LairSanguophages.xml), hidden and permanently hostile, created the
// first time a lair is generated in a world and found again by def ever after. Read through
// LairSite.Faction, which caches it for the generation.
//
// Why a def of our own and not vanilla's Sanguophages: Biotech creates a fresh temporary
// Sanguophages faction per quest (QuestNode_Root_SanguophageShip, ...MeetingHost), so a
// lookup by that def could return one of those, owned by a running quest and hostile or
// neutral as the quest chose, and FactionManager removes temporary factions once their last
// pawn is gone. Ours is never temporary, so it persists for the life of the world: hidden
// factions with no settlements are never marked defeated, and a hidden faction needs no
// leader (Faction.ShouldHaveLeader), so no leader death letters either. It is also where a
// soft compat with another mod's sanguophage faction would slot in
// (Docs/Research/Sanguophage-Faction-Lair-Owner.md): FindOrCreate is the single place the
// owner is decided.
//
// Decompile-verified, identical in 1.6 and 1.7 (RimWorld/FactionGenerator.cs,
// RimWorld/FactionManager.cs, RimWorld/Faction.cs):
//  - FactionGenerator.NewGeneratedFactionWithRelations(FactionDef, List<FactionRelation>,
//    bool hidden) builds the faction on the surface layer, gives it an ideoligion (honouring
//    the def's requiredMemes), and runs Faction.TryMakeInitialRelationsWith against every
//    existing faction. FactionDef.permanentEnemy makes each of those relations -100 goodwill,
//    Hostile, so the empty relation list here is enough.
//  - FactionManager.Add registers it and notifies every map; the Biotech quests do the same.
public static class LairFaction
{
    public static Faction FindOrCreate()
    {
        FactionDef def = SL_LairPawnDefOf.SL_LairSanguophages;
        Faction faction = Find.FactionManager.FirstFactionOfDef(def);
        if (faction != null)
        {
            return faction;
        }
        faction = FactionGenerator.NewGeneratedFactionWithRelations(def, new List<FactionRelation>(), hidden: true);
        Find.FactionManager.Add(faction);
        return faction;
    }
}
