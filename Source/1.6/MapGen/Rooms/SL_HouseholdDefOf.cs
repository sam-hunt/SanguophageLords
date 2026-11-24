using RimWorld;
using Verse;

namespace SanguophageLair;

// Vanilla defs the household rooms (guardroom, thrall quarters, dining hall, larder,
// storeroom, vault) look up and that ThingDefOf does not carry. Core and Biotech only. Each
// field names its C# consumer.
[DefOf]
public static class SL_HouseholdDefOf
{
    // LairRoomWorker_ThrallQuarters: the table at the head of each bed.
    public static ThingDef EndTable;

    // LairRoomWorker_DiningHall: the 3x3 sections of the long table.
    public static ThingDef Table3x3c;

    // LairRoomWorker_DiningHall, LairRoomWorker_Vault: the large sculptures flanking the head
    // chair and the vault's far wall.
    public static ThingDef SculptureLarge;

    // LairRoomWorker_Guardroom (weapon rack), LairRoomWorker_Vault (plasteel display pieces).
    public static ThingDef MeleeWeapon_LongSword;

    public static ThingDef MeleeWeapon_Mace;

    // LairRoomWorker_Guardroom: the third rack weapon.
    public static ThingDef MeleeWeapon_Spear;

    // LairRoomWorker_Vault: the serum on the central display.
    public static ThingDef DeathrestCapacitySerum;

    static SL_HouseholdDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(SL_HouseholdDefOf));
    }
}
