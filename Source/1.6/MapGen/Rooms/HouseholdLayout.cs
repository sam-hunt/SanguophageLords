using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SanguophageLair;

// Shelf and table helpers the household rooms (larder, storeroom, vault, dining hall) share: a
// run of shelves along each side wall and stocking shelves with items. They stay out of
// LairRoom because only these rooms keep shelves in runs and loaded by cell; placing the
// pieces themselves goes through LairRoom.TrySpawnOver and TrySpawnOverPair.
internal static class HouseholdLayout
{
    // Shelves along both side walls, standing at the given column and facing into the room,
    // over the forward rows from..to: two-cell shelves, with a small shelf for an odd last
    // cell. The shelves come back in order from the start row, each side in its own list.
    public static void SideShelves(LairRoom room, int wall, int from, int to, ColorDef paint, List<Thing> rightSide, List<Thing> leftSide)
    {
        int forward = from;
        while (forward <= to)
        {
            bool full = forward + 1 <= to;
            ThingDef def = full ? ThingDefOf.Shelf : ThingDefOf.ShelfSmall;
            int last = full ? forward + 1 : forward;
            Thing right = room.TrySpawnOverPair(def, wall, forward, wall, last, Rot4.West, out Thing left, paint: paint);
            if (right != null)
            {
                rightSide.Add(right);
            }
            if (left != null)
            {
                leftSide.Add(left);
            }
            forward = last + 1;
        }
    }

    // One stack of an item on each cell of a shelf. A null shelf (one that did not fit)
    // stocks nothing.
    public static void StockShelf(LairRoom room, Thing shelf, ThingDef def, int count)
    {
        if (shelf == null)
        {
            return;
        }
        foreach (IntVec3 c in shelf.OccupiedRect())
        {
            room.TrySpawnItem(def, c, count, onShelf: true);
        }
    }

    // True when a building (a table, say) stands on the cell, so an item there rests on it.
    public static bool HasBuilding(LairRoom room, IntVec3 c)
    {
        return c.GetFirstBuilding(room.Map) != null;
    }
}
