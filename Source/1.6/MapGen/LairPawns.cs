using System.Collections.Generic;
using Verse;

namespace SanguophageLair;

// Who a lair pawn is, from the room workers' point of view. Which PawnKindDef each tier is
// belongs to LairPawns, so the lair's roster can change without touching a single room.
public enum LairPawnTier
{
    // A baseliner servant of the lair.
    Thrall,
    // One of the lair's sanguophages.
    Sanguophage,
    // The sanguophage the lair belongs to.
    Master,
}

// Creates the lair's inhabitants for the room workers (through the LairRoom.TrySpawn*
// methods, which are the only callers). STUB: every method returns null until the defender
// system lands; room workers must already cope with null, since a full room or a missing
// faction gives the same answer.
public static class LairPawns
{
    // An awake defender standing guard near a cell of the room.
    public static Pawn SpawnGuard(LairRoom room, LairPawnTier tier, IntVec3 near)
    {
        return null;
    }

    // A defender asleep in an ordinary bed of the room, who wakes when the lair is alerted.
    public static Pawn SpawnSleeper(LairRoom room, LairPawnTier tier, Thing bed)
    {
        return null;
    }

    // A sanguophage deathresting in a casket, bound to the given deathrest buildings.
    public static Pawn SpawnDeathrester(LairRoom room, LairPawnTier tier, Thing casket, IEnumerable<Thing> boundBuildings)
    {
        return null;
    }

    // A captive kept for blood: a prisoner of the lair, in the given bed when there is one,
    // else standing near the cell.
    public static Pawn SpawnPrisoner(LairRoom room, Thing bed, IntVec3 near)
    {
        return null;
    }
}
