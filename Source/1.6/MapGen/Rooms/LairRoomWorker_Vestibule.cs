using System;
using RimWorld;
using Verse;

namespace SanguophageLords;

// The vestibule: the short chamber just inside the gate arch. The arch has no door, so this
// room is in plain view from the forecourt and the far bank, and it is dressed as a threat:
// the processional's scarlet runner starting at the threshold, a large sculpture either side
// of it, and a blood torch in each near corner so the open gate glows red from across the
// river. The door in its far wall is the first the player has to open.
public class LairRoomWorker_Vestibule : LairRoomWorker
{
    public override void Fill(LairRoom room)
    {
        room.Floor(room.Style.Carpet(0), (right, forward) => Math.Abs(right) <= 1);
        room.TrySpawnPair(SL_LairDefOf.SculptureLarge, room.HalfAcross - 1, 0, Rot4.South, paint: room.Style.Paint(0));
        room.TryTorch(room.At(room.HalfAcross, -room.HalfDeep));
        room.TryTorch(room.At(-room.HalfAcross, -room.HalfDeep));
        room.TryTorch(room.At(room.HalfAcross, room.HalfDeep));
        room.TryTorch(room.At(-room.HalfAcross, room.HalfDeep));
    }
}
