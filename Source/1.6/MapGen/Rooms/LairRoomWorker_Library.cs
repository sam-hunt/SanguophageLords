using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SanguophageLords;

// The library: the centuries-old minds of the lair keep their reading here. A reading table
// under a round of carpet, ringed by chairs, with a great sculpture on the far wall between
// two blood torches, and bookcases lining every wall that is left. The books are the room's
// loot: a dozen across the cases, a couple of them fine.
//
// Layout in the room's local frame (right R, forward F, centre 0,0):
//   far wall      large sculpture at R 0, a torch either side of it
//   near wall     a torch either side of the entrance, which is at R 0
//   side walls    a torch at F 0 on each
//   centre        3x3 table (R,F -1..1), chairs at (+-1,-2), (+-1,+2) and (+-2,0), facing it
//   carpet        three concentric rounds, the brightest under the table
//   everywhere else along the walls: bookcases, in whatever stretches of wall the above leave
// Bookcases are not placed from coordinates. WallSideCells names every free floor cell with a
// wall beside it and the map direction of that wall; cells sharing a wall direction and a
// distance from the room's centre line form a run, and each run is cut into two-cell cases
// from the end nearest the centre line, with a small case for a single cell left over. That
// is what makes the cases follow an octagon's stepped corners, and it needs no knowledge of
// the room's size or shape. Cases face away from the wall.
//
// Rooms 13 across and deep have a centre more than 6.9 cells from any wall, so they get four
// columns at (+-3,+-3), outside the ring of chairs. Smaller rooms are all within reach of a
// wall and have none. Books are spread round-robin over the cases in the order placed, so a
// room with many cases has a book or two in each and one with few has them fuller.
public class LairRoomWorker_Library : LairRoomWorker
{
    private const int BookCount = 12;
    private const int FineBooks = 2;

    public override void Fill(LairRoom room)
    {
        int halfAcross = room.HalfAcross;
        int halfDeep = room.HalfDeep;
        LairStyle style = room.Style;

        int rim = Math.Min(halfAcross, halfDeep) - 1;
        room.Floor(style.Carpet(2), (right, forward) => (right * right) + (forward * forward) <= (rim * rim) + 1);
        room.Floor(style.Carpet(1), (right, forward) => (right * right) + (forward * forward) <= ((rim - 1) * (rim - 1)) + 2);
        room.Floor(style.Carpet(0), (right, forward) => (right * right) + (forward * forward) <= 6);

        room.TrySpawnOver(SL_LairDefOf.SculptureLarge, 0, halfDeep, 0, halfDeep, Rot4.South, room.StoneOrSteel(SL_LairDefOf.SculptureLarge), style.Paint(0));
        foreach (int side in new[] { -1, 1 })
        {
            room.TryTorch(room.At(side, halfDeep));
            room.TryTorch(room.At(side, -halfDeep));
            room.TryTorch(room.At(side * halfAcross, 0));
        }

        if (halfAcross >= 6 && halfDeep >= 6)
        {
            room.TrySpawnPair(ThingDefOf.Column, 3, 3, Rot4.North, paint: style.WallPaint);
            room.TrySpawnPair(ThingDefOf.Column, 3, -3, Rot4.North, paint: style.WallPaint);
        }

        List<Building_Bookcase> cases = PlaceCases(room);

        room.TrySpawnOver(SL_LairDefOf.Table3x3c, -1, -1, 1, 1, Rot4.North, room.StoneOrSteel(SL_LairDefOf.Table3x3c), style.Paint(2));
        ThingDef chair = ThingDefOf.DiningChair;
        ThingDef chairStuff = room.StoneOrSteel(chair);
        foreach (int side in new[] { -1, 1 })
        {
            room.TrySpawnOver(chair, side, -2, side, -2, Rot4.North, chairStuff, style.Paint(0));
            room.TrySpawnOver(chair, side, 2, side, 2, Rot4.South, chairStuff, style.Paint(0));
        }
        room.TrySpawnOver(chair, 2, 0, 2, 0, Rot4.West, chairStuff, style.Paint(0));
        room.TrySpawnOver(chair, -2, 0, -2, 0, Rot4.East, chairStuff, style.Paint(0));

        Stock(cases);
    }

    // One stretch of wall: the cells beside it keyed by their position along it, and the map
    // direction of the wall they share.
    private sealed class Run
    {
        public Rot4 WallSide;
        public readonly SortedDictionary<int, IntVec3> Cells = new SortedDictionary<int, IntVec3>();
    }

    private static List<Building_Bookcase> PlaceCases(LairRoom room)
    {
        var runs = new Dictionary<int, Run>();
        foreach (KeyValuePair<IntVec3, Rot4> side in room.WallSideCells())
        {
            room.Local(side.Key, out int right, out int forward);
            // Walls running across the room (local north and south) are measured along R,
            // the side walls along F; the other coordinate says which step of the outline.
            bool across = room.Turn(Rot4.North) == side.Value || room.Turn(Rot4.South) == side.Value;
            int along = across ? right : forward;
            int perp = across ? forward : right;
            int key = (side.Value.AsInt * 1000) + perp + 500;
            if (!runs.TryGetValue(key, out Run run))
            {
                run = new Run { WallSide = side.Value };
                runs[key] = run;
            }
            run.Cells[along] = side.Key;
        }

        var cases = new List<Building_Bookcase>();
        ColorDef paint = room.Style.Paint(1);
        foreach (Run run in runs.Values)
        {
            var chunk = new List<IntVec3>();
            int previous = int.MinValue;
            foreach (KeyValuePair<int, IntVec3> cell in run.Cells)
            {
                if (chunk.Count > 0 && cell.Key != previous + 1)
                {
                    FillChunk(room, chunk, run.WallSide, paint, cases);
                    chunk.Clear();
                }
                chunk.Add(cell.Value);
                previous = cell.Key;
            }
            FillChunk(room, chunk, run.WallSide, paint, cases);
        }
        return cases;
    }

    // Cuts a stretch of consecutive wall cells, ordered by position along the wall, into cases.
    // Cells on the negative side of the centre line are cut from their upper end, so both
    // halves of a wall start at the centre.
    private static void FillChunk(LairRoom room, List<IntVec3> chunk, Rot4 wallSide, ColorDef paint, List<Building_Bookcase> cases)
    {
        if (chunk.Count == 0)
        {
            return;
        }
        room.Local(chunk[0], out int right, out int forward);
        bool across = room.Turn(Rot4.North) == wallSide || room.Turn(Rot4.South) == wallSide;
        bool fromEnd = (across ? right : forward) < 0;
        if (fromEnd)
        {
            chunk.Reverse();
        }
        Rot4 face = wallSide.Opposite;
        for (int i = 0; i < chunk.Count; i++)
        {
            Thing placed;
            if (i + 1 < chunk.Count)
            {
                placed = room.TrySpawnOver(SL_LairDefOf.Bookcase, CellRect.FromLimits(chunk[i], chunk[i + 1]), face, room.StoneOrSteel(SL_LairDefOf.Bookcase), paint);
                if (placed != null)
                {
                    i++;
                }
            }
            else
            {
                placed = null;
            }
            if (placed == null)
            {
                placed = room.TrySpawnOver(SL_LairDefOf.BookcaseSmall, CellRect.SingleCell(chunk[i]), face, room.StoneOrSteel(SL_LairDefOf.BookcaseSmall), paint);
            }
            if (placed is Building_Bookcase bookcase)
            {
                cases.Add(bookcase);
            }
        }
    }

    // Books are made the way the game's own bookcase fill does, and added to the case's
    // container; none are left loose on the floor.
    private static void Stock(List<Building_Bookcase> cases)
    {
        if (cases.Count == 0)
        {
            return;
        }
        for (int i = 0; i < BookCount; i++)
        {
            bool fine = i < FineBooks;
            Book book = BookUtility.MakeBook(PickBookDef(), ArtGenerationContext.Outsider, fine ? QualityGenerator.Reward : QualityGenerator.BaseGen);
            if (book == null)
            {
                continue;
            }
            Building_Bookcase bookcase = cases[i % cases.Count];
            if (!bookcase.GetDirectlyHeldThings().TryAdd(book))
            {
                book.Destroy();
            }
        }
    }

    // Mostly novels and textbooks, the odd schematic.
    private static ThingDef PickBookDef()
    {
        float roll = Rand.Value;
        if (roll < 0.45f)
        {
            return ThingDefOf.Novel;
        }
        return roll < 0.9f ? ThingDefOf.TextBook : ThingDefOf.Schematic;
    }
}
