using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SanguophageLair;

// The mausoleum: the founder's tomb, the end of the lair's axis and its grandest funerary
// room. Round or octagonal, eleven to thirteen cells across, entered from the south end of the
// axis.
//
// Everything follows the room's own outline (LairRoom.Inset), so the round and the octagonal
// room get the same design:
//   inset 0     bare floor against the wall, blood torches between the ring pieces
//   inset 1     a ring of pieces about three cells apart, a sculpture on the axis at the far
//               end and then columns and sculptures by turns, mirrored across the axis
//   inset 2     burgundy carpet, the edge of the dais
//   inset 3-4   scarlet carpet
//   inset 5+    red carpet under the great sarcophagus
// The sarcophagus lies on the axis with its head to the far wall; a pair of small sculptures
// kneel beside it. It is left empty: a sanguophage founder does not stay dead.
//
// Roof. At thirteen across the centre is as far from the wall as thick rock roof reaches, so
// the columns of the ring are what holds it; the ring always has one every other piece.
//
// Guard. One sanguophage stands watch near the door, where the corridor opens into the room.
public class LairRoomWorker_Mausoleum : LairRoomWorker
{
    private const float PieceSpacing = 3f;

    public override void Fill(LairRoom room)
    {
        LairStyle style = room.Style;
        room.FloorRing(style.Carpet(2), 2, 2);
        room.FloorRing(style.Carpet(0), 3, 4);
        room.FloorRing(style.Carpet(1), 5, int.MaxValue);

        room.TrySpawnOver(ThingDefOf.Sarcophagus, 0, 0, 0, 1, Rot4.North, paint: style.Paint(0), quality: QualityCategory.Masterwork);
        room.TrySpawnPair(SL_LairDefOf.SculptureSmall, 2, 0, Rot4.North, paint: style.Paint(1));

        BuildRing(room);

        room.TrySpawnGuard(LairPawnTier.Sanguophage, room.At(0, -room.HalfDeep + 2));
    }

    // The half of the ring on the visitor's right, walked from the far end of the axis to the
    // door, each piece mirrored to the left; torches go on the wall cells between the pieces.
    private static void BuildRing(LairRoom room)
    {
        var ring = new List<IntVec2>();
        var wallCells = new List<IntVec2>();
        foreach (IntVec3 c in room.Cells)
        {
            room.Local(c, out int right, out int forward);
            if (right < 0)
            {
                continue;
            }
            int inset = room.Inset(c);
            if (inset == 1)
            {
                ring.Add(new IntVec2(right, forward));
            }
            else if (inset == 0 && right > 0)
            {
                wallCells.Add(new IntVec2(right, forward));
            }
        }
        // Angle from the far end of the axis (0) round to the door end (pi).
        ring.Sort((a, b) => Math.Atan2(a.x, a.z).CompareTo(Math.Atan2(b.x, b.z)));

        IntVec2 previous = IntVec2.Invalid;
        int index = 0;
        foreach (IntVec2 slot in ring)
        {
            // The mirror of an off-axis piece must not crowd it: keep clear of the axis.
            if ((slot.x != 0 && slot.x < 2) || (previous.IsValid && Distance(slot, previous) < PieceSpacing))
            {
                continue;
            }
            bool column = index % 2 == 1;
            ThingDef def = column ? ThingDefOf.Column : SL_LairDefOf.SculptureLarge;
            ColorDef paint = column ? room.Style.WallPaint : room.Style.Paint(0);
            Thing placed = room.TrySpawn(def, slot.x, slot.z, Rot4.North, paint: paint);
            if (slot.x != 0)
            {
                Thing twin = room.TrySpawn(def, -slot.x, slot.z, Rot4.North, paint: paint);
                placed = placed ?? twin;
            }
            if (placed == null)
            {
                continue;
            }
            if (previous.IsValid)
            {
                PlaceTorchBetween(room, wallCells, previous, slot);
            }
            previous = slot;
            index++;
        }
    }

    private static void PlaceTorchBetween(LairRoom room, List<IntVec2> wallCells, IntVec2 a, IntVec2 b)
    {
        float midRight = (a.x + b.x) / 2f;
        float midForward = (a.z + b.z) / 2f;
        IntVec2 best = IntVec2.Invalid;
        float bestDist = float.MaxValue;
        foreach (IntVec2 cell in wallCells)
        {
            float dist = ((cell.x - midRight) * (cell.x - midRight)) + ((cell.z - midForward) * (cell.z - midForward));
            if (dist < bestDist && room.IsFree(cell.x, cell.z))
            {
                best = cell;
                bestDist = dist;
            }
        }
        if (best.IsValid)
        {
            room.TryTorch(room.At(best.x, best.z));
            room.TryTorch(room.At(-best.x, best.z));
        }
    }

    private static float Distance(IntVec2 a, IntVec2 b)
    {
        return (float)Math.Sqrt(((a.x - b.x) * (a.x - b.x)) + ((a.z - b.z) * (a.z - b.z)));
    }
}
