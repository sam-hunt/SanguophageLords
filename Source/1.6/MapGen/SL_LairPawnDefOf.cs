using RimWorld;
using Verse;
using Verse.AI;

namespace SanguophageLair;

// Our defs the lair's inhabitants are built from, looked up by identity. All ship in the main
// tree (Biotech is a hard dependency). Each field names its C# consumer.
[DefOf]
public static class SL_LairPawnDefOf
{
    // LairFaction.FindOrCreate: the faction every lair inhabitant belongs to.
    public static FactionDef SL_LairSanguophages;

    // LairPawns.KindFor: one kind per LairPawnTier.
    public static PawnKindDef SL_LairThrall;

    public static PawnKindDef SL_LairSanguophage;

    public static PawnKindDef SL_LairMaster;

    // GenStep_LairDefenders: the lair's general alarm thing (LairAlarm).
    public static ThingDef SL_LairAlarm;

    // LordToil_LairSleep: keeps sleepers asleep in their own beds (JobGiver_LairSleep).
    public static DutyDef SL_LairSleep;

    static SL_LairPawnDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(SL_LairPawnDefOf));
    }
}
