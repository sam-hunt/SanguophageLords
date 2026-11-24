using RimWorld;
using Verse;

namespace SanguophageLair;

// Vanilla defs the lair's shared code and room workers look up by identity and that
// ThingDefOf does not already carry. Core and Biotech only (Biotech is a hard dependency);
// rooms built on optional content keep their handles in SL_OptionalRoomDefOf, and the hemogen
// network's in SL_HemogenNetDefOf, with the matching MayRequire attributes. Each field names
// every C# consumer.
[DefOf]
public static class SL_LairDefOf
{
    // ----- Sculpture -----

    // LairRoomWorker_Processional: the sculpture in every other wall bay.
    // LairRoomWorker_EntranceHall: the pair beside the arch. LairRoomWorker_DiningHall: the
    // pair at the end of each wall rhythm. LairRoomWorker_Mausoleum: the pair kneeling beside
    // the sarcophagus. LairRoomWorker_ThroneRoom: the pairs beside the throne and inside the
    // door. LairRoomWorker_Gallery: the small bays.
    public static ThingDef SculptureSmall;

    // LairRoomWorker_Sanctum: the pieces flanking each face. LairRoomWorker_Vestibule: the
    // pair either side of the runner. LairRoomWorker_DiningHall: the pair flanking the head
    // chair. LairRoomWorker_Vault: the pair on the far wall. LairRoomWorker_Library: the
    // focal piece on the far wall. LairRoomWorker_Mausoleum: the sculptures of the ring around
    // the founder's sarcophagus. LairRoomWorker_Tomb: the piece standing in for the far-wall
    // stele in a room too short for it. LairRoomWorker_Gallery: the large bays.
    public static ThingDef SculptureLarge;

    // LairRoomWorker_Forecourt: the pair flanking the gate. LairRoomWorker_Gallery: the
    // centrepiece at the far end.
    public static ThingDef SculptureGrand;

    // ----- Deathrest (Biotech) -----

    // LairRoomWorker_Sanctum: the accelerators before and behind the casket.
    public static ThingDef DeathrestAccelerator;

    // LairRoomWorker_Sanctum and LairRoomWorker_DeathrestChamber: the pumps and amplifier
    // bound to the sleeper in the casket.
    public static ThingDef Hemopump;
    public static ThingDef HemogenAmplifier;
    public static ThingDef GlucosoidPump;

    // LairRoomWorker_Sanctum: the fourth pump, bound like the others.
    public static ThingDef PsychofluidPump;

    // LairRoomWorker_Vault: the serum on the central display.
    public static ThingDef DeathrestCapacitySerum;

    // ----- Furniture -----

    // LairRoomWorker_ThrallQuarters: the table at the head of each bed.
    public static ThingDef EndTable;

    // LairRoomWorker_DiningHall: the 3x3 sections of the long table. LairRoomWorker_Library:
    // the reading table in the centre of the room (not rotatable).
    public static ThingDef Table3x3c;

    // LairRoomWorker_Library: the stocked cases along the walls (2x1 and 1x1, stony-stuffable).
    public static ThingDef Bookcase;
    public static ThingDef BookcaseSmall;

    // LairRoomWorker_Prison: the bloodletting couch. LairRoomWorker_Laboratory: the subject's
    // bed. Its medical flag is set by the def, so it reads as a clinic bed.
    public static ThingDef HospitalBed;

    // ----- Laboratory (Biotech) -----

    // LairRoomWorker_Laboratory: the research bench on the far wall (5x2, 250 W).
    public static ThingDef HiTechResearchBench;

    // LairRoomWorker_Laboratory: the gene processor beside the assembler.
    public static ThingDef GeneProcessor;

    // ----- Weapons -----

    // LairRoomWorker_Guardroom: the weapons on the racks. LairRoomWorker_Vault: the plasteel
    // longsword and mace on the shelves.
    public static ThingDef MeleeWeapon_LongSword;
    public static ThingDef MeleeWeapon_Mace;

    // LairRoomWorker_Guardroom: the third rack weapon.
    public static ThingDef MeleeWeapon_Spear;

    // ----- Terrain -----

    // LairRoomWorker_Crypt: graves are only dug where the floor accepts a spade.
    public static TerrainAffordanceDef Diggable;

    static SL_LairDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(SL_LairDefOf));
    }
}
