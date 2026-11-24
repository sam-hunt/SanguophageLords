using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SanguophageLair;

// Placement helpers the household room workers share: multi-cell furniture addressed by the
// local-frame cells it covers rather than by its engine "centre" cell, mirrored pairs of such
// pieces, a run of shelves along each side wall, and stocking shelves with items.
internal static class HouseholdLayout
{
    // Spawns a piece that covers exactly the local cells (right0, forward0) to (right1,
    // forward1), corners in either order, facing the given local direction. The engine's
    // position of an even-sized building depends on its rotation, so the position is searched
    // for rather than computed. Null when the piece does not fit that footprint or the floor.
    public static Thing Place(LairRoom room, ThingDef def, int right0, int forward0, int right1, int forward1, Rot4 localRot, ThingDef stuff = null, ColorDef paint = null)
    {
        Rot4 rot = room.Turn(localRot);
        Rot4 footprintRot = def.rotatable ? rot : Rot4.North;
        CellRect wanted = CellRect.FromLimits(room.At(right0, forward0), room.At(right1, forward1));
        foreach (IntVec3 c in wanted.ExpandedBy(1))
        {
            if (GenAdj.OccupiedRect(c, footprintRot, def.size) == wanted)
            {
                return room.TrySpawn(def, c, rot, stuff, paint);
            }
        }
        return null;
    }

    // Place on both sides of the centre line, the second the mirror image of the first.
    public static Thing PlacePair(LairRoom room, ThingDef def, int right0, int forward0, int right1, int forward1, Rot4 localRot, out Thing mirrored, ThingDef stuff = null, ColorDef paint = null)
    {
        Thing thing = Place(room, def, right0, forward0, right1, forward1, localRot, stuff, paint);
        mirrored = null;
        if (right0 != 0 || right1 != 0)
        {
            Rot4 other = localRot.IsHorizontal ? localRot.Opposite : localRot;
            mirrored = Place(room, def, -right0, forward0, -right1, forward1, other, stuff, paint);
        }
        return thing;
    }

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
            Thing right = PlacePair(room, def, wall, forward, wall, last, Rot4.West, out Thing left, paint: paint);
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
