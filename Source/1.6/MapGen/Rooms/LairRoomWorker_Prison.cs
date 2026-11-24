using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SanguophageLair;

// The prison: where the lair keeps its prey. A room hewn from the rock, and the one room whose
// worker builds structure: a central aisle between walled cells, a bloodletting couch at its
// far end, and a thrall or two standing in the aisle.
//
// Layout in the room's local frame (right R, forward F), for the 11 to 13 across and 9 to 11
// deep the def gives it (HalfAcross 5 or 6, HalfDeep 4 or 5):
//   aisle         R -1..1 the whole depth, kept clear
//   cell walls    a wall line down R +-2, with a partition row across R 2..HalfAcross+1 at
//                 F -1 and at F HalfDeep-1, so each side holds two cells
//   cell          R 3..HalfAcross (three or four wide), F -HalfDeep..-2 and 0..HalfDeep-2
//                 (three or four deep); a door in the wall line at the cell's middle row,
//                 a bed against the outer wall with its head toward the far partition
//   far end       the bloodletting couch at R 0, F HalfDeep-1..HalfDeep, head to the far wall,
//                 with a small shelf of hemogen packs each side of it
//   light         one blood torch, beside the entrance: the prison is meant to be dark
// The cell depth is HalfDeep-1, so every depth the def allows leaves exactly one row (the
// alcoves either side of the couch) past the last partition.
//
// A cell is built only if every cell of its enclosure is free floor or already wall: the
// planner may have put a door of the room's where a cell would stand, and hewn outlines can
// bulge, so a cell that does not fit whole is left out rather than half built. The partition
// rows run one cell past the floor's edge (R HalfAcross+1) so a bulge in the outline cannot
// leak one cell into the next. Whatever is built, the aisle and every doorway of the room stay
// clear of it, so the room stays walkable. Prisoners are asked for in most cells (at least
// one), and the prison bed of each is flagged for prisoners here, because bed ownership is
// otherwise decided by whether the bed's region is already a prison cell, which it is not yet
// at generation.
public class LairRoomWorker_Prison : LairRoomWorker
{
    private const float PrisonerChance = 0.75f;
    private const int PacksPerShelf = 5;

    public override void Fill(LairRoom room)
    {
        int halfAcross = room.HalfAcross;
        int halfDeep = room.HalfDeep;
        int depth = halfDeep - 1;
        ColorDef paint = room.Style.Paint(1);

        var beds = new List<Thing>();
        var spots = new List<IntVec3>();
        foreach (int side in new[] { -1, 1 })
        {
            BuildCell(room, side, -halfDeep, -halfDeep + depth - 1, beds, spots);
            BuildCell(room, side, 0, depth - 1, beds, spots);
        }

        StudyRoomKit.Place(room, SL_StudyDefOf.HospitalBed, 0, halfDeep - 1, 0, halfDeep, Rot4.South, StudyRoomKit.Stuff(room, SL_StudyDefOf.HospitalBed), paint);
        foreach (int side in new[] { -1, 1 })
        {
            Thing shelf = StudyRoomKit.Place(room, ThingDefOf.ShelfSmall, side, halfDeep, side, halfDeep, Rot4.South, StudyRoomKit.Stuff(room, ThingDefOf.ShelfSmall), paint);
            if (shelf != null)
            {
                room.TrySpawnItem(ThingDefOf.HemogenPack, shelf.Position, PacksPerShelf, onShelf: true);
            }
        }
        room.TryTorch(room.At(1, -halfDeep));

        bool anyPrisoner = false;
        for (int i = 0; i < beds.Count; i++)
        {
            if ((Rand.Chance(PrisonerChance) || (!anyPrisoner && i == beds.Count - 1)) && room.TrySpawnPrisoner(beds[i], spots[i]) != null)
            {
                anyPrisoner = true;
            }
        }

        room.TrySpawnGuard(LairPawnTier.Thrall, room.At(0, 0));
        if (Rand.Chance(0.5f))
        {
            room.TrySpawnGuard(LairPawnTier.Thrall, room.At(0, -2));
        }
    }

    // Builds one cell whose interior spans the given rows on one side of the aisle, then its
    // bed. Does nothing if the enclosure does not fit.
    private static void BuildCell(LairRoom room, int side, int firstRow, int lastRow, List<Thing> beds, List<IntVec3> spots)
    {
        int halfAcross = room.HalfAcross;
        int doorRow = firstRow + ((lastRow - firstRow) / 2);
        var walls = new List<IntVec3>();
        for (int f = firstRow - 1; f <= lastRow + 1; f++)
        {
            if (f != doorRow)
            {
                walls.Add(room.At(side * 2, f));
            }
        }
        for (int r = 3; r <= halfAcross + 1; r++)
        {
            walls.Add(room.At(side * r, firstRow - 1));
            walls.Add(room.At(side * r, lastRow + 1));
        }
        IntVec3 door = room.At(side * 2, doorRow);

        foreach (IntVec3 c in walls)
        {
            if (!(IsWall(room, c) || (room.Contains(c) && room.IsFree(c))))
            {
                return;
            }
        }
        if (!room.IsFree(door))
        {
            return;
        }
        for (int r = 3; r <= halfAcross; r++)
        {
            for (int f = firstRow; f <= lastRow; f++)
            {
                if (!room.IsFree(r * side, f))
                {
                    return;
                }
            }
        }

        foreach (IntVec3 c in walls)
        {
            if (!room.Contains(c) || c.GetEdifice(room.Map) != null)
            {
                continue;
            }
            room.TrySpawn(ThingDefOf.Wall, c, Rot4.North, paint: room.Style.WallPaint);
        }
        room.TrySpawn(ThingDefOf.Door, door, Rot4.North, paint: room.Style.DoorPaint);

        Thing bed = StudyRoomKit.Place(room, ThingDefOf.Bed, side * halfAcross, lastRow - 1, side * halfAcross, lastRow, Rot4.South, StudyRoomKit.Stuff(room, ThingDefOf.Bed));
        if (bed is Building_Bed prisonerBed)
        {
            prisonerBed.ForPrisoners = true;
            beds.Add(bed);
            spots.Add(room.At(side * 3, doorRow));
        }
    }

    // A wall of the plan, or one already built into the room by an earlier cell.
    private static bool IsWall(LairRoom room, IntVec3 c)
    {
        if (!room.Contains(c))
        {
            return room.IsWall(c);
        }
        Building edifice = c.GetEdifice(room.Map);
        return edifice != null && edifice.def == ThingDefOf.Wall;
    }
}
