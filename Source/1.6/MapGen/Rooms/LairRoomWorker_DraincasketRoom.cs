using RimWorld;
using Verse;

namespace SanguophageLair;

// The draincasket room (Vanilla Races Expanded - Sanguophage only): where the lair bleeds its
// captives. Draincaskets stand along both side walls facing each other across the room, a
// blood torch between each pair, and against the far wall the hemogen vat they drain into
// with a tap beside it. The vat is the source of the lair's hemogen pipe network, which
// GenStep_LairUtilities runs from here to every casket of the room and on to the sanctum,
// the deathrest chambers and the larder.
//
// Layout in the room's local frame (the def gives 7~9 square):
//   far wall    vat (2x2) on cells -1..0 across, tap at +1: the room's focal piece
//   side walls  2x2 caskets in the two cells against each wall, facing the middle, from two
//               rows short of the far wall toward the door, one row apart: two a side
//   gaps        a torch against each side wall in the row between caskets
//   middle      the walking lane, onto which every casket opens; a captive stands in it
// The caskets stay empty: an occupant starves within days of the map ticking, which is a
// corpse, not a horror. The captive waiting by them is what makes the point. A missing vat
// (the far wall cut by a door) is placed against any wall instead, nearest the far wall,
// since the network needs it.
//
// Draincasket facts (VRE BloodNet.xml): 2x2, rotatable, interaction cell offset (0,0,2),
// which GenAdj puts on the cell just past the footprint in the facing direction.
public class LairRoomWorker_DraincasketRoom : LairRoomWorker
{
    private const int CasketStep = 3;

    public override void Fill(LairRoom room)
    {
        if (!LairHemogenNet.Active)
        {
            return;
        }
        int across = room.HalfAcross;
        int deep = room.HalfDeep;

        Thing vat = room.TrySpawnOver(SL_HemogenNetDefOf.VRE_HemogenVat, -1, deep - 1, 0, deep, Rot4.North);
        if (vat != null)
        {
            room.TrySpawn(SL_HemogenNetDefOf.VRE_HemogenTap, 1, deep, Rot4.North);
        }
        else
        {
            vat = room.TrySpawnAgainstWall(SL_HemogenNetDefOf.VRE_HemogenVat, room.At(0, deep));
            if (vat != null)
            {
                room.TrySpawnAgainstWall(SL_HemogenNetDefOf.VRE_HemogenTap, vat.Position);
            }
        }

        for (int top = deep - 2; top - 1 >= -deep; top -= CasketStep)
        {
            // Used from the side each faces, and kept only if that opens onto the room's floor.
            room.TrySpawnUsedFrom(SL_HemogenNetDefOf.VRE_Draincasket, across - 1, top - 1, across, top, Rot4.West);
            room.TrySpawnUsedFrom(SL_HemogenNetDefOf.VRE_Draincasket, -across, top - 1, -across + 1, top, Rot4.East);
            int gap = top - CasketStep + 1;
            if (gap > -deep && room.IsFree(across, gap) && room.IsFree(-across, gap))
            {
                room.TryTorch(room.At(across, gap));
                room.TryTorch(room.At(-across, gap));
            }
        }

        room.TrySpawnPrisoner(null, room.At(0, 0));
    }
}
