using System;
using RimWorld;
using Verse;

namespace SanguophageLair;

// The entrance hall: the paved octagon at the foot of the ancient stairwell, where visitors
// arrive. It is meant to be read in the first seconds underground: old, deliberate, and lit
// by something that is still being tended.
//
// The 7x7 stairwell exit already stands at the centre when this runs (the exit step precedes
// the contents step), so everything here is placed around it. In the local frame the hall's
// one opening, the arch toward the river, is on the near side (negative forward):
//   - A column at each corner of the stairwell. They are also what holds the roof over it:
//     the hall is 15 to 17 across and the exit itself holds nothing up.
//   - A runner of deep red carpet from the stairs to the arch, with a small sculpture either
//     side of the arch.
//   - Blood torches along the walls, the first light the player sees.
public class LairRoomWorker_EntranceHall : LairRoomWorker
{
    // Half the stairwell exit's 7x7 footprint.
    private const int StairsHalf = 3;

    public override void Fill(LairRoom room)
    {
        int corner = StairsHalf + 1;
        room.TrySpawnPair(ThingDefOf.Column, corner, corner, Rot4.North, paint: room.Style.WallPaint);
        room.TrySpawnPair(ThingDefOf.Column, corner, -corner, Rot4.North, paint: room.Style.WallPaint);

        room.Floor(room.Style.Carpet(1), (right, forward) => Math.Abs(right) <= 1 && forward < -StairsHalf);
        room.TrySpawnPair(SL_LairDefOf.SculptureSmall, 3, -room.HalfDeep, Rot4.South, paint: room.Style.Paint(1));

        room.TorchesAlongWalls(7);
    }
}
