using RimWorld;
using Verse;

namespace SanguophageLair;

// A corridor between two rooms: one or three cells wide and a few cells long, sized by the
// planner. Kept nearly bare so the rooms at its ends are the events. A three-wide corridor
// gets a single-cell runner of deep red carpet and, if it is long enough to have a free cell
// between the doorways at its ends, one pair of blood torches at its middle. A one-wide
// corridor cannot hold anything without blocking it and is carpeted wall to wall instead.
public class LairRoomWorker_Corridor : LairRoomWorker
{
    public override void Fill(LairRoom room)
    {
        room.Floor(room.Style.Carpet(1), (right, forward) => right == 0);
        if (room.HalfAcross >= 1)
        {
            room.TryTorch(room.At(room.HalfAcross, 0));
            room.TryTorch(room.At(-room.HalfAcross, 0));
        }
    }
}
