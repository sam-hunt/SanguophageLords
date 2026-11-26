using RimWorld;
using Verse;

namespace SanguophageLords;

// Defs of the lair's hemogen pipe network: the Vanilla Races Expanded - Sanguophage buildings
// it is made of, reached by defName so this assembly never references that mod's DLL, and the
// lair rooms it connects. The VRE fields are null when the mod is not loaded; every consumer
// goes through LairHemogenNet.Active first. Each field names its C# consumer.
[DefOf]
public static class SL_HemogenNetDefOf
{
    // LairRoomWorker_DraincasketRoom: the caskets along the side walls.
    [MayRequire(LairHemogenNet.PackageId)]
    public static ThingDef VRE_Draincasket;

    // LairRoomWorker_DraincasketRoom: the network's source vat. LairHemogenNet: the larder's
    // receiving vat.
    [MayRequire(LairHemogenNet.PackageId)]
    public static ThingDef VRE_HemogenVat;

    // LairRoomWorker_DraincasketRoom: the tap beside the vat. LairHemogenNet: the tap by each
    // deathrest casket.
    [MayRequire(LairHemogenNet.PackageId)]
    public static ThingDef VRE_HemogenTap;

    // LairHemogenNet: pipe on bare floor when the layout shows its lines.
    [MayRequire(LairHemogenNet.PackageId)]
    public static ThingDef VRE_HemogenPipe;

    // LairHemogenNet: pipe under carpet, or everywhere when the layout hides its lines.
    [MayRequire(LairHemogenNet.PackageId)]
    public static ThingDef VRE_UndergroundHemogenPipe;

    // LairHemogenNet: the room whose vat feeds the network.
    public static LairRoomDef SL_LairRoom_DraincasketRoom;

    // LairHemogenNet: receives a vat.
    public static LairRoomDef SL_LairRoom_Larder;

    // LairHemogenNet: receives a tap beside its caskets, as the sanctum does.
    public static LairRoomDef SL_LairRoom_DeathrestChamber;

    static SL_HemogenNetDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(SL_HemogenNetDefOf));
    }
}
