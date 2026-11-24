using RimWorld;
using Verse;

namespace SanguophageLair;

// Vanilla defs of the three optional-DLC rooms (temple, throne room, ritual chamber). Every
// field is null when its DLC is inactive, and each room's worker returns before touching any
// of them in that case: Royalty and Anomaly buildings call ModLister.CheckRoyalty/CheckAnomaly
// on spawn and refuse or log without their DLC. Each field names its C# consumer.
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

    // Reading stands either side of the altar.
    [MayRequireIdeology]
    public static ThingDef Lectern;

    // The congregation's seating (3x1, sitters face the pew's rotation).
    [MayRequireIdeology]
    public static ThingDef Pew;

    // The pair flanking the altar.
    [MayRequireIdeology]
    public static ThingDef Skullspike;

    // The Morbid styles applied through Thing.StyleDef after the thing is spawned.
    [MayRequireIdeology]
    public static ThingStyleDef Morbid_AltarGrand;

    [MayRequireIdeology]
    public static ThingStyleDef Morbid_IdeogramA;

    [MayRequireIdeology]
    public static ThingStyleDef Morbid_IdeogramB;

    [MayRequireIdeology]
    public static ThingStyleDef Morbid_Column;

    // ----- Royalty: LairRoomWorker_ThroneRoom -----

    // The master's seat (3x2, interaction cell in front of its facing).
    [MayRequireRoyalty]
    public static ThingDef GrandThrone;

    // Hangings against the side walls (2x1, rotation names the wall they hang on).
    [MayRequireRoyalty]
    public static ThingDef Drape;

    // The instrument in one bay of the hall; the harp is the fallback where the harpsichord
    // does not fit.
    [MayRequireRoyalty]
    public static ThingDef Harpsichord;

    [MayRequireRoyalty]
    public static ThingDef Harp;

    // ----- Anomaly: LairRoomWorker_RitualChamber -----

    // The ritual spot at the centre of the chamber (3x3).
    [MayRequireAnomaly]
    public static ThingDef PsychicRitualSpot;

    // The beacons (up to four per spot) and sculptures (up to six) that improve a ritual.
    [MayRequireAnomaly]
    public static ThingDef ShardBeacon;

    [MayRequireAnomaly]
    public static ThingDef VoidSculpture;

    // The books left on the chamber's shelves.
    [MayRequireAnomaly]
    public static ThingDef Tome;

    // The plate under the ritual spot.
    [MayRequireAnomaly]
    public static TerrainDef BioferritePlate;

    static SL_OptionalRoomDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(SL_OptionalRoomDefOf));
    }
}
