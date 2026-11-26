using RimWorld;
using Verse;

namespace SanguophageLords;

// Vanilla defs of the three optional-DLC rooms (temple, throne room, ritual chamber) that
// ThingDefOf does not carry; the ones it does (lectern, skullspike, drape, harp, harpsichord,
// ritual spot, tome) are read from there, carrying the same MayRequire marks. Every field is
// null when its DLC is inactive, and each room's worker returns before touching any of them in
// that case: Royalty and Anomaly buildings call ModLister.CheckRoyalty/CheckAnomaly on spawn
// and refuse or log without their DLC. Each field names its C# consumer.
[DefOf]
public static class SL_OptionalRoomDefOf
{
    // ----- Ideology: LairRoomWorker_Temple -----

    // The far-end altar (3x3, interaction cell two cells in front of its centre).
    [MayRequireIdeology]
    public static ThingDef Altar_Grand;

    // The floor mural before the altar (3x3).
    [MayRequireIdeology]
    public static ThingDef Ideogram;

    // The congregation's seating (3x1, sitters face the pew's rotation).
    [MayRequireIdeology]
    public static ThingDef Pew;

    // The Morbid styles applied through Thing.StyleDef after the thing is spawned.
    [MayRequireIdeology]
    public static ThingStyleDef Morbid_AltarGrand;

    [MayRequireIdeology]
    public static ThingStyleDef Morbid_IdeogramA;

    [MayRequireIdeology]
    public static ThingStyleDef Morbid_Column;

    // ----- Royalty: LairRoomWorker_ThroneRoom -----

    // The master's seat (3x2, interaction cell in front of its facing).
    [MayRequireRoyalty]
    public static ThingDef GrandThrone;

    // ----- Anomaly: LairRoomWorker_RitualChamber -----

    // The beacons (up to four per spot) and sculptures (up to six) that improve a ritual.
    [MayRequireAnomaly]
    public static ThingDef ShardBeacon;

    [MayRequireAnomaly]
    public static ThingDef VoidSculpture;

    // The plate under the ritual spot.
    [MayRequireAnomaly]
    public static TerrainDef BioferritePlate;

    static SL_OptionalRoomDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(SL_OptionalRoomDefOf));
    }
}
