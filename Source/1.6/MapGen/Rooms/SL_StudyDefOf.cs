using RimWorld;
using Verse;

namespace SanguophageLair;

// Vanilla defs the library, laboratory and prison workers look up that ThingDefOf lacks. All
// are Core except the Biotech gene buildings, which the lair's hard Biotech dependency keeps
// non-null; the MayRequire marks only keep the DefOf loader quiet without the DLC.
[DefOf]
public static class SL_StudyDefOf
{
    // LairRoomWorker_Library: the stocked cases along the walls (2x1 and 1x1, stony-stuffable).
    public static ThingDef Bookcase;

    public static ThingDef BookcaseSmall;

    // LairRoomWorker_Library: the focal piece against the far wall.
    public static ThingDef SculptureLarge;

    // LairRoomWorker_Library: the reading table in the centre of the room (3x3, not rotatable).
    public static ThingDef Table3x3c;

    // LairRoomWorker_Prison: the bloodletting couch. Its default medical flag is set by the
    // def, so it reads as a clinic bed.
    public static ThingDef HospitalBed;

    // LairRoomWorker_Laboratory: the research bench on the far wall (5x2, 250 W).
    public static ThingDef HiTechResearchBench;

    // LairRoomWorker_Laboratory: the gene processor and extractor beside the assembler.
    [MayRequireBiotech]
    public static ThingDef GeneProcessor;

    [MayRequireBiotech]
    public static ThingDef GeneExtractor;

    static SL_StudyDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(SL_StudyDefOf));
    }
}
