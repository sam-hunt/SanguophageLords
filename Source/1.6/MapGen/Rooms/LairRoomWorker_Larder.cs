using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SanguophageLair;

// The larder: the hemogen store, a small hewn room lined with shelves painted the strongest
// red, like the red shelves of the reference image. Hemogen packs fill the far wall and every
// third side shelf; the shelves between them hold a little ordinary food and medicine for the
// thralls and prisoners.
//
// Local frame, for a room HalfAcross = a and HalfDeep = d (both 2 or 3):
//   right +-a, forward -d+1..d   a run of shelves down each side wall
//   forward d, right 1..a-1      the far wall's shelves, either side of a torch at (0, d)
//   forward -d, right +-a        a torch in each corner beside the door
// Shelf rows follow a three-shelf cycle from the door: hemogen, food on one cell and medicine
// on the other, hemogen. Both sides are stocked alike.
//
// Hemogen packs have no rot comp (the def's description: a container that prevents
// spoilage) and only deteriorate in the open, which a roofed shelf prevents, so the stock
// needs no cooling; the room stands on the pocket map's 15C regardless. Pemmican rots after
// 70 days, far longer than a visit.
public class LairRoomWorker_Larder : LairRoomWorker
{
    private const int HemogenPerCell = 8;

    public override void Fill(LairRoom room)
    {
        int wall = room.HalfAcross;
        int far = room.HalfDeep;
        ColorDef paint = room.Style.Paint(0);

        var rightSide = new List<Thing>();
        var leftSide = new List<Thing>();
        HouseholdLayout.SideShelves(room, wall, -far + 1, far, paint, rightSide, leftSide);
        Stock(room, rightSide);
        Stock(room, leftSide);

        // The far wall stops short of the side shelves' last column.
        bool full = wall - 1 >= 2;
        ThingDef def = full ? ThingDefOf.Shelf : ThingDefOf.ShelfSmall;
        Thing farShelf = HouseholdLayout.PlacePair(room, def, 1, far, full ? 2 : 1, far, Rot4.South, out Thing mirrored, paint: paint);
        HouseholdLayout.StockShelf(room, farShelf, ThingDefOf.HemogenPack, HemogenPerCell);
        HouseholdLayout.StockShelf(room, mirrored, ThingDefOf.HemogenPack, HemogenPerCell);

        room.TryTorch(room.At(0, far));
        room.TryTorch(room.At(wall, -far));
        room.TryTorch(room.At(-wall, -far));
    }

    private static void Stock(LairRoom room, List<Thing> shelves)
    {
        for (int i = 0; i < shelves.Count; i++)
        {
            Thing shelf = shelves[i];
            if (i % 3 != 1)
            {
                HouseholdLayout.StockShelf(room, shelf, ThingDefOf.HemogenPack, HemogenPerCell);
                continue;
            }
            bool food = true;
            foreach (IntVec3 c in shelf.OccupiedRect())
            {
                if (food)
                {
                    room.TrySpawnItem(ThingDefOf.Pemmican, c, 10, onShelf: true);
                    room.TrySpawnItem(ThingDefOf.MealSurvivalPack, c, 2, onShelf: true);
                }
                else
                {
                    room.TrySpawnItem(ThingDefOf.MedicineHerbal, c, 6, onShelf: true);
                    room.TrySpawnItem(ThingDefOf.MedicineIndustrial, c, 2, onShelf: true);
                }
                food = false;
            }
        }
    }
}
