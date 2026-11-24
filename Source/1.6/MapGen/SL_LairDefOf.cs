using RimWorld;
using Verse;

namespace SanguophageLair;

// Vanilla defs the lair's shared code and spine rooms look up by identity and that
// ThingDefOf does not already carry. Core and Biotech only (Biotech is a hard dependency);
// rooms built on optional content keep their handles in their own DefOf beside the worker,
// with the matching MayRequire attribute. Each field names its C# consumer.
[DefOf]
public static class SL_LairDefOf
{
    // LairRoomWorker_Processional: the sculpture in every other wall bay.
    public static ThingDef SculptureSmall;

    static SL_LairDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(SL_LairDefOf));
    }
}
