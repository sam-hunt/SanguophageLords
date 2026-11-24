using RimWorld;
using Verse;

namespace SanguophageLair;

// The guardroom: where the lair's thralls keep watch on the main hall. A mirrored pair stands
// off the processional near the gate, each entered through its own door in the middle of the
// near wall. Two weapon racks flank a torch on the far wall, a small table with stools stands
// in front of them on the centre line, and plain stone beds line the side walls, heads to
// the far wall. The two guards stand just inside the door, one to each side of it.
//
// Local frame, for a room HalfAcross = a (3 or 4) and HalfDeep = d (2 or 3):
//   forward d          rack (right 1..2) | torch (0) | rack (-2..-1); torches in the corners
//   forward d-2..d-1   the table on the centre line (1x2); beds in the side-wall column
//   forward d-4..d-3   a second pair of beds, when the room is deep enough (d = 3)
//   forward d-2        a stool either side of the table; forward d-3 a stool at its foot
//   forward -d         the door at right 0, torches in the corners either side
// The lane down each side of the table and the row in front of the door stay open.
public class LairRoomWorker_Guardroom : LairRoomWorker
{
    public override void Fill(LairRoom room)
    {
        int wall = room.HalfAcross;
        int far = room.HalfDeep;
        ColorDef paint = room.Style.Paint(1);

        // Weapon racks: each cell holds one weapon.
        Thing rack = room.TrySpawnOverPair(ThingDefOf.Shelf, 1, far, 2, far, Rot4.South, out Thing mirroredRack, paint: paint);
        Stock(room, rack, SL_LairDefOf.MeleeWeapon_Spear, SL_LairDefOf.MeleeWeapon_Mace);
        Stock(room, mirroredRack, SL_LairDefOf.MeleeWeapon_LongSword, SL_LairDefOf.MeleeWeapon_Spear);
        room.TryTorch(room.At(0, far));
        room.TryTorch(room.At(wall, far));
        room.TryTorch(room.At(-wall, far));
        room.TryTorch(room.At(wall, -far));
        room.TryTorch(room.At(-wall, -far));

        // The table and its stools.
        room.TrySpawnOver(ThingDefOf.Table1x2c, 0, far - 2, 0, far - 1, Rot4.North, paint: paint);
        room.TrySpawnPair(ThingDefOf.Stool, 1, far - 2, Rot4.West);
        room.TrySpawn(ThingDefOf.Stool, 0, far - 3, Rot4.North);

        // Beds, heads to the far wall.
        for (int forward = far - 2; forward >= -1; forward -= 2)
        {
            room.TrySpawnOverPair(ThingDefOf.Bed, wall, forward, wall, forward + 1, Rot4.South, out _);
        }

        // Guards either side of the door, looking across the room at whoever comes in.
        room.TrySpawnGuard(LairPawnTier.Thrall, room.At(2, -far + 1));
        room.TrySpawnGuard(LairPawnTier.Thrall, room.At(-2, -far + 1));
    }

    private static void Stock(LairRoom room, Thing rack, ThingDef first, ThingDef second)
    {
        if (rack == null)
        {
            return;
        }
        bool firstCell = true;
        foreach (IntVec3 c in rack.OccupiedRect())
        {
            room.TrySpawnItem(firstCell ? first : second, c, 1, onShelf: true);
            firstCell = false;
        }
    }
}
