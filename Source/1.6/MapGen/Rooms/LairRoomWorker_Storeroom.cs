using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SanguophageLords;

// The storeroom: general stores, deliberately unglamorous. Plain unpainted shelves hold
// cloth, steel, components and wood for the torches; stacks of the lair's own stone blocks
// and logs sit on the floor by the door, and a few chunks of its rock lie in the corner
// opposite. One torch burns on the far wall. Worth little.
//
// Local frame, for a room HalfAcross = a and HalfDeep = d (both 2 or 3):
//   forward d, right 1..2   a shelf either side of the torch at (0, d)
//   right +-a, forward 0..d-1   shelves down the side walls (a small one for an odd count)
//   forward -d, -d+1        the right-hand door corner takes rock chunks, the left log and
//                           block stacks; the door's own cells stay clear
// The shelves are stocked in turn from one list, so each carries a single kind of goods.
public class LairRoomWorker_Storeroom : LairRoomWorker
{
    public override void Fill(LairRoom room)
    {
        int wall = room.HalfAcross;
        int far = room.HalfDeep;

        var shelves = new List<Thing>();
        Thing farShelf = room.TrySpawnOverPair(ThingDefOf.Shelf, 1, far, 2, far, Rot4.South, out Thing mirrored);
        AddIfPlaced(shelves, farShelf);
        AddIfPlaced(shelves, mirrored);
        var rightSide = new List<Thing>();
        var leftSide = new List<Thing>();
        HouseholdLayout.SideShelves(room, wall, 0, far - 1, null, rightSide, leftSide);
        for (int i = 0; i < rightSide.Count; i++)
        {
            shelves.Add(rightSide[i]);
            if (i < leftSide.Count)
            {
                shelves.Add(leftSide[i]);
            }
        }

        ThingDef[] goods = { ThingDefOf.Cloth, ThingDefOf.ComponentIndustrial, ThingDefOf.WoodLog, ThingDefOf.Steel };
        int[] counts = { 40, 8, 60, 50 };
        for (int i = 0; i < shelves.Count; i++)
        {
            HouseholdLayout.StockShelf(room, shelves[i], goods[i % goods.Length], counts[i % goods.Length]);
        }

        room.TryTorch(room.At(0, far));

        room.TrySpawnItem(room.Style.Blocks, room.At(-wall, -far), 75);
        room.TrySpawnItem(ThingDefOf.WoodLog, room.At(-wall, -far + 1), 75);
        room.TrySpawnItem(ThingDefOf.WoodLog, room.At(-wall + 1, -far), 50);

        ThingDef chunk = room.Style.Rock.building != null ? room.Style.Rock.building.mineableThing : null;
        if (chunk != null && chunk.category == ThingCategory.Item)
        {
            room.TrySpawnItem(chunk, room.At(wall, -far));
            room.TrySpawnItem(chunk, room.At(wall - 1, -far));
            room.TrySpawnItem(chunk, room.At(wall, -far + 1));
        }
    }

    private static void AddIfPlaced(List<Thing> shelves, Thing shelf)
    {
        if (shelf != null)
        {
            shelves.Add(shelf);
        }
    }
}
