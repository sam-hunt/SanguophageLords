using RimWorld;
using Verse;

namespace SanguophageLair;

// The thrall quarters: the servants' dormitory, hewn from the rock and left plain next to
// their masters' halls. Stone beds stand in rows along both side walls, one every other row,
// each lying across the room with its head to the wall and an end table in the gap beside
// the pillow. The aisle down the middle, wide enough for the door and for passing, stays
// empty. A small shelf of rations stands on the far wall between two torches; those are the
// only flames, since the thralls are not trusted with more.
//
// Beds sleep a thrall in a checkerboard: even rows on the right, odd rows on the left, so
// about half the beds are occupied and the two sides differ.
public class LairRoomWorker_ThrallQuarters : LairRoomWorker
{
    public override void Fill(LairRoom room)
    {
        int wall = room.HalfAcross;
        int far = room.HalfDeep;

        int row = 0;
        for (int forward = -far + 1; forward < far; forward += 2, row++)
        {
            Thing right = room.TrySpawnOverPair(ThingDefOf.Bed, wall - 1, forward, wall, forward, Rot4.West, out Thing left);
            room.TrySpawnPair(SL_LairDefOf.EndTable, wall, forward + 1, Rot4.West);
            Thing sleeping = row % 2 == 0 ? right : left;
            if (sleeping != null)
            {
                room.TrySpawnSleeper(LairPawnTier.Thrall, sleeping);
            }
        }

        Thing shelf = room.TrySpawn(ThingDefOf.ShelfSmall, 0, far, Rot4.South);
        if (shelf != null)
        {
            room.TrySpawnItem(ThingDefOf.Pemmican, shelf.Position, 10, onShelf: true);
            room.TrySpawnItem(ThingDefOf.MedicineHerbal, shelf.Position, 4, onShelf: true);
        }
        room.TryTorch(room.At(2, far));
        room.TryTorch(room.At(-2, far));
    }
}
