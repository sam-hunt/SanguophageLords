using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SanguophageLair;

// Placement of multi-cell buildings by the cells they should cover, on top of
// LairRoom.TrySpawn. A building's Position is not the corner of its footprint, and for even
// sizes it sits off-centre in a way that depends on the rotation (GenAdj.OccupiedRect and
// AdjustForRotation), so a worker that wants "a 2x2 casket in this corner, facing the middle"
// would otherwise have to redo that arithmetic for every rotation of the room. Here the
// caller names the footprint in the room's local frame and the Position is searched for.
public static class LairFootprint
{
    // Spawns def, facing localRot, so that its footprint covers exactly the local rectangle
    // between the two corners (inclusive, in any order). Null when no Position gives that
    // footprint or LairRoom.TrySpawn refuses it.
    public static Thing TrySpawnCovering(LairRoom room, ThingDef def, int right0, int forward0, int right1, int forward1, Rot4 localRot, ThingDef stuff = null, ColorDef paint = null)
    {
        if (def == null)
        {
            return null;
        }
        Rot4 rot = def.rotatable ? room.Turn(localRot) : Rot4.North;
        CellRect wanted = CellRect.FromLimits(room.At(right0, forward0), room.At(right1, forward1));
        foreach (IntVec3 c in wanted.ExpandedBy(1))
        {
            if (GenAdj.OccupiedRect(c, rot, def.size) == wanted)
            {
                return room.TrySpawn(def, c, rot, stuff, paint);
            }
        }
        return null;
    }

    // Spawns a non-rotating def with its footprint touching one of the room's walls, at the
    // free spot nearest the given cell. Null when no wall of the room has room for it.
    public static Thing TrySpawnAgainstWall(LairRoom room, ThingDef def, IntVec3 near)
    {
        if (def == null)
        {
            return null;
        }
        var candidates = new List<IntVec3>();
        foreach (KeyValuePair<IntVec3, Rot4> side in room.WallSideCells())
        {
            // Every Position whose footprint covers the wall-side cell.
            for (int dz = -def.size.z; dz <= def.size.z; dz++)
            {
                for (int dx = -def.size.x; dx <= def.size.x; dx++)
                {
                    IntVec3 c = side.Key + new IntVec3(dx, 0, dz);
                    if (GenAdj.OccupiedRect(c, Rot4.North, def.size).Contains(side.Key) && !candidates.Contains(c))
                    {
                        candidates.Add(c);
                    }
                }
            }
        }
        // Nearest footprint centre first; ties by cell so the choice is reproducible.
        candidates.Sort((a, b) =>
        {
            int da = DoubledDistanceSquared(GenAdj.OccupiedRect(a, Rot4.North, def.size), near);
            int db = DoubledDistanceSquared(GenAdj.OccupiedRect(b, Rot4.North, def.size), near);
            if (da != db)
            {
                return da.CompareTo(db);
            }
            return a.x != b.x ? a.x.CompareTo(b.x) : a.z.CompareTo(b.z);
        });
        foreach (IntVec3 c in candidates)
        {
            Thing thing = room.TrySpawn(def, c, Rot4.North);
            if (thing != null)
            {
                return thing;
            }
        }
        return null;
    }

    // Squared distance from a footprint's centre to a cell, in half cells so even-sized
    // footprints stay in integers.
    private static int DoubledDistanceSquared(CellRect rect, IntVec3 c)
    {
        int dx = rect.minX + rect.maxX - (2 * c.x);
        int dz = rect.minZ + rect.maxZ - (2 * c.z);
        return (dx * dx) + (dz * dz);
    }
}
