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
    // LairRoomWorker_EntranceHall: the pair beside the arch.
    public static ThingDef SculptureSmall;

    // LairRoomWorker_Sanctum: the pieces flanking each face. LairRoomWorker_Vestibule: the
    // pair either side of the runner.
    public static ThingDef SculptureLarge;

    // LairRoomWorker_Forecourt: the pair flanking the gate.
    public static ThingDef SculptureGrand;

    // LairRoomWorker_Sanctum and LairRoomWorker_DeathrestChamber: the deathrest buildings
    // bound to the sleeper in the casket (Biotech).
    public static ThingDef DeathrestAccelerator;
    public static ThingDef Hemopump;
    public static ThingDef HemogenAmplifier;
    public static ThingDef GlucosoidPump;
    public static ThingDef PsychofluidPump;

    static SL_LairDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(SL_LairDefOf));
    }
}
