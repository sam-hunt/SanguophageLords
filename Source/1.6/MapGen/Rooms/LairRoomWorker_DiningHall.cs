using RimWorld;
using Verse;

namespace SanguophageLords;

// The dining hall: a long table down the room's axis on a field of red carpet, dining chairs
// facing it from both sides, and a gold chair for the master at the head, against the far
// wall under two large sculptures. Sanguophages dine on hemogen, so packs stand on the
// table and on the sideboards along the walls.
//
// Local frame, for HalfAcross = a (4 or 5) and HalfDeep = d (5 to 7):
//   the table is three cells wide (right -1..1) and runs from forward -d+3, leaving two rows
//   of floor inside the door, to forward d-1, its foot open and the master's chair at (0, d)
//   chairs      right +-2, every other table row
//   lanes       right +-3 (and +-4 in the widest room), kept clear
//   walls       right +-a: a torch, a two-cell shelf and a sculpture in a four-cell rhythm
// Tables come in fixed sizes. The table's length (7, 9 or 11) is cut into 3x3 tables and
// rows of three 1x2 tables, taking 3 rows at a time unless that would leave one row over.
// Items sit on a table cell like on a shelf, the engine placing no restriction on a spawn.
public class LairRoomWorker_DiningHall : LairRoomWorker
{
    private const int WallRhythm = 4;

    public override void Fill(LairRoom room)
    {
        int wall = room.HalfAcross;
        int far = room.HalfDeep;
        LairStyle style = room.Style;

        room.FloorRing(style.Carpet(2), 1, 1);
        room.FloorRing(style.Carpet(0), 2, int.MaxValue);

        int start = -far + 3;
        int end = far - 1;
        for (int forward = start; forward <= end;)
        {
            int remaining = end - forward + 1;
            int rows = remaining >= 3 && remaining - 3 != 1 ? 3 : 2;
            if (rows == 3)
            {
                room.TrySpawnOver(SL_LairDefOf.Table3x3c, -1, forward, 1, forward + 2, Rot4.North, paint: style.Paint(2));
            }
            else
            {
                for (int right = -1; right <= 1; right++)
                {
                    room.TrySpawnOver(ThingDefOf.Table1x2c, right, forward, right, forward + 1, Rot4.North, paint: style.Paint(2));
                }
            }
            forward += rows;
        }

        for (int forward = start; forward <= end; forward += 2)
        {
            room.TrySpawnPair(ThingDefOf.DiningChair, 2, forward, Rot4.West, ThingDefOf.Steel, style.Paint(1));
        }
        room.TrySpawn(ThingDefOf.DiningChair, 0, far, Rot4.South, ThingDefOf.Gold);

        for (int forward = start + 1; forward < end; forward += 3)
        {
            IntVec3 cell = room.At(0, forward);
            if (HouseholdLayout.HasBuilding(room, cell))
            {
                room.TrySpawnItem(ThingDefOf.HemogenPack, cell, 3, onShelf: true);
            }
        }

        for (int forward = -far + 2; forward <= far; forward += WallRhythm)
        {
            room.TryTorch(room.At(wall, forward));
            room.TryTorch(room.At(-wall, forward));
            Thing shelf = room.TrySpawnOverPair(ThingDefOf.Shelf, wall, forward + 1, wall, forward + 2, Rot4.West, out Thing mirrored, paint: style.Paint(0));
            HouseholdLayout.StockShelf(room, shelf, ThingDefOf.HemogenPack, 5);
            HouseholdLayout.StockShelf(room, mirrored, ThingDefOf.HemogenPack, 5);
            room.TrySpawnPair(SL_LairDefOf.SculptureSmall, wall, forward + 3, Rot4.South, paint: style.Paint(1));
        }

        room.TrySpawnPair(SL_LairDefOf.SculptureLarge, 1, far, Rot4.South, paint: style.Paint(0));
        room.TryTorch(room.At(2, far));
        room.TryTorch(room.At(-2, far));
    }
}
