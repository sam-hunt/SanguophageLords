using System;
using RimWorld;
using Verse;

namespace SanguophageLair;

// The throne room: the master's audience hall, the end of the lair's axis. Only requested
// when Royalty is active; a bare room is left otherwise.
//
// Layout in the room's local frame (far = HalfDeep, wall = HalfAcross):
//   right -1..1        a scarlet runner from the door to the dais
//   far-3..far         the dais, fine scarlet carpet across right -3..3
//   right -1..1,
//   far-1..far         the grand throne against the far wall, facing the door
//   right +-3, far-2   a sanguophage guard each, flanking the dais
//   right +-3, far     a sculpture each beside the throne; the same pair inside the door
//   right +-(wall-2)   columns every fourth cell down the hall, in mirrored pairs
//   right +-wall       a blood torch level with each column, then a drape in the bay beyond
//                      it; one harpsichord (harp where it does not fit) replaces the drape
//                      in one bay on the right
// Braziers are left out on purpose: Royalty's Brazier is an open wood fire, which the lair
// does not keep, and the vanilla pair is only a title requirement that means nothing in a
// lair. Blood torches are the lair's light everywhere else and carry the room.
//
// Facings (decompile-verified on 1.6): a building's interaction cell is its offset rotated by
// the building's rotation. The grand throne's offset is (0,0,1) and vanilla throne rooms
// place it facing south on the north wall, so local South puts its interaction cell in front
// of it. The harpsichord's offset (0,0,2) points along its rotation, so it takes the local
// direction toward the room's centre; the harp's (0,0,-1) points opposite, so it takes the
// direction of its wall. A drape takes the direction of the wall it hangs on, as vanilla's
// throne room resolver gives it. Footprints of even size are placed by SpawnFootprint, which
// finds the centre cell giving the wanted rectangle, so mirrored pieces mirror exactly
// whichever way the room is turned.
public class LairRoomWorker_ThroneRoom : LairRoomWorker
{
    private const int ColumnSpacing = 4;

    public override void Fill(LairRoom room)
    {
        if (!ModsConfig.RoyaltyActive || SL_OptionalRoomDefOf.GrandThrone == null)
        {
            return;
        }

        int far = room.HalfDeep;
        int wall = room.HalfAcross;
        LairStyle style = room.Style;

        TerrainDef daisFloor = DefDatabase<TerrainDef>.GetNamedSilentFail("CarpetFineScarlet") ?? style.Carpet(0);
        room.Floor(style.Carpet(0), (right, forward) => Math.Abs(right) <= 1 && forward < far - 3);
        room.Floor(daisFloor, (right, forward) => Math.Abs(right) <= 3 && forward >= far - 3);

        SpawnFootprint(room, SL_OptionalRoomDefOf.GrandThrone, Rot4.South, -1, far - 1, 1, far, style.Paint(0));
        room.TrySpawnGuard(LairPawnTier.Sanguophage, room.At(3, far - 2));
        room.TrySpawnGuard(LairPawnTier.Sanguophage, room.At(-3, far - 2));

        room.TrySpawnPair(SL_LairDefOf.SculptureSmall, 3, far, Rot4.South, paint: style.Paint(1));
        room.TrySpawnPair(SL_LairDefOf.SculptureSmall, 3, -far, Rot4.North, paint: style.Paint(1));
        room.TryTorch(room.At(wall, far));
        room.TryTorch(room.At(-wall, far));

        // The hall: a column in each pair of bays, a torch level with it, and the bay beyond.
        int columnRight = wall - 2;
        bool instrumentPlaced = false;
        for (int forward = far - 5; forward >= -far + 3; forward -= ColumnSpacing)
        {
            room.TrySpawnPair(ThingDefOf.Column, columnRight, forward, Rot4.North, paint: style.WallPaint);
            room.TryTorch(room.At(wall, forward));
            room.TryTorch(room.At(-wall, forward));
            if (!instrumentPlaced)
            {
                instrumentPlaced = SpawnFootprint(room, SL_OptionalRoomDefOf.Harpsichord, room.Turn(Rot4.West), wall - 2, forward + 1, wall, forward + 2) != null;
            }
            Drape(room, wall, forward + 1, Rot4.East);
            Drape(room, -wall, forward + 1, Rot4.West);
        }
        if (!instrumentPlaced)
        {
            room.TrySpawn(SL_OptionalRoomDefOf.Harp, wall, far - 6, Rot4.East);
        }

        // Beside the dais, where the hall's last column is too far back to frame it.
        Drape(room, wall, far - 2, Rot4.East);
        Drape(room, -wall, far - 2, Rot4.West);
    }

    // A drape hung on a side wall over two cells from the given forward position on.
    private static void Drape(LairRoom room, int rightward, int forward, Rot4 wallSide)
    {
        SpawnFootprint(room, SL_OptionalRoomDefOf.Drape, room.Turn(wallSide), rightward, forward, rightward, forward + 1);
    }

    // Spawns a thing so that it covers exactly the rectangle between two local cells, trying
    // each cell of the rectangle as the thing's centre until one gives that footprint. Null
    // when none does (the thing's size does not match) or the footprint is not free.
    private static Thing SpawnFootprint(LairRoom room, ThingDef def, Rot4 rot, int rightA, int forwardA, int rightB, int forwardB, ColorDef paint = null)
    {
        if (def == null)
        {
            return null;
        }
        CellRect wanted = CellRect.FromLimits(room.At(rightA, forwardA), room.At(rightB, forwardB));
        foreach (IntVec3 c in wanted)
        {
            CellRect rect = GenAdj.OccupiedRect(c, rot, def.size);
            if (rect.minX == wanted.minX && rect.maxX == wanted.maxX && rect.minZ == wanted.minZ && rect.maxZ == wanted.maxZ)
            {
                return room.TrySpawn(def, c, rot, paint: paint);
            }
        }
        return null;
    }
}
