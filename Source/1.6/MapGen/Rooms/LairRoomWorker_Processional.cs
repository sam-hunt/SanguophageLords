using System;
using RimWorld;
using Verse;

namespace SanguophageLair;

// The processional: the long hall between the vestibule and the sanctum, and the lair's first
// statement of what it is. A scarlet runner down the middle between two rows of dark columns,
// and in the bays behind the columns, against the walls, blood torches alternating with
// sculpture, every piece mirrored across the hall.
//
// Layout in the room's local frame, for the seven-cell width the def gives it (right -3..3):
//   right 0, +-1   carpet runner, the walking lane, kept clear end to end
//   right +-2      a column every third cell
//   right +-3      the wall bays: one cell past each column, torch and sculpture by turns
// Rooms open off the hall's long sides. Their doorways are reserved by LairRoom, so a column
// or bay piece that would stand in front of a side door is simply not placed, and the rhythm
// skips a beat there rather than blocking the door.
public class LairRoomWorker_Processional : LairRoomWorker
{
    private const int ColumnSpacing = 3;

    public override void Fill(LairRoom room)
    {
        int wall = room.HalfAcross;
        int colonnade = wall - 1;
        room.Floor(room.Style.Carpet(0), (right, forward) => Math.Abs(right) < colonnade);

        int bay = 0;
        for (int forward = -room.HalfDeep + 1; forward < room.HalfDeep; forward += ColumnSpacing, bay++)
        {
            room.TrySpawnPair(ThingDefOf.Column, colonnade, forward, Rot4.North, paint: room.Style.WallPaint);
            if (forward + 1 >= room.HalfDeep)
            {
                break;
            }
            if (bay % 2 == 0)
            {
                room.TryTorch(room.At(wall, forward + 1));
                room.TryTorch(room.At(-wall, forward + 1));
            }
            else
            {
                room.TrySpawnPair(SL_LairDefOf.SculptureSmall, wall, forward + 1, Rot4.South, paint: room.Style.Paint(1));
            }
        }
    }
}
