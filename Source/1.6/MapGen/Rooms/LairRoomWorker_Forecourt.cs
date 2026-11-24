using System;
using RimWorld;
using Verse;

namespace SanguophageLair;

// The forecourt: the strip of paving under the lair's facade, across the river from the
// stairwell hall. With the gate arch in the facade behind it, this is the lair's face, the
// "ominous entranceway" a visitor sees from the far bank.
//
// The room has no walls of its own; in its local frame the facade is the far side (positive
// forward) and the cavern the near side. The middle three cells are the causeway's line to
// the gate and stay clear.
//   - A grand sculpture either side of the gate, hard against the facade.
//   - Blood torches beside the gate and toward the ends of the facade.
//   - A row of columns along the open edge facing the river, a colonnade the causeway passes
//     through. They also carry the cavern roof out from the facade.
public class LairRoomWorker_Forecourt : LairRoomWorker
{
    public override void Fill(LairRoom room)
    {
        int back = room.HalfDeep;
        ColorDef accent = room.Style.Paint(0);
        // 2x2 pieces: three and four cells out from the axis, in the two rows by the facade.
        room.TrySpawnOver(SL_LairDefOf.SculptureGrand, 3, back - 1, 4, back, Rot4.South, paint: accent);
        room.TrySpawnOver(SL_LairDefOf.SculptureGrand, -4, back - 1, -3, back, Rot4.South, paint: accent);

        room.TryTorch(room.At(2, back));
        room.TryTorch(room.At(-2, back));
        int end = room.HalfAcross - 2;
        room.TryTorch(room.At(end, back));
        room.TryTorch(room.At(-end, back));

        for (int right = 2; right <= room.HalfAcross; right += 3)
        {
            room.TrySpawnPair(ThingDefOf.Column, right, -back, Rot4.North, paint: room.Style.WallPaint);
        }
    }
}
