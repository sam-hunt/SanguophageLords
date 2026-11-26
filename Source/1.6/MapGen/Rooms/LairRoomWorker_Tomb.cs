using System;
using RimWorld;
using Verse;

namespace SanguophageLords;

// The tomb: a formal burial chamber for the lair's honoured dead. Two of them open off the
// processional's long sides, so one faces the other across the main hall.
//
// Layout in the room's local frame (right -wall..wall, forward -deep..deep), for the seven to
// nine cells across the def gives it:
//   right 0, +-1   carpet runner from the door to the far wall, the walking aisle
//   right +-2      burgundy carpet edging, only in the nine-wide rooms (seven-wide ones have
//                  no spare cell for it between the aisle and the sarcophagi)
//   right +-wall   sarcophagi lying across the side rows (two cells long, so they take
//                  right wall-1..wall), heads to the wall, one every second cell, blood torches
//                  standing in every other gap against the wall
//   far wall       a grand stele filling the end of the aisle (right -1..1, the last three
//                  rows), a torch in each far corner
// Sarcophagi are red-painted stone. A few hold a desiccated corpse (LairBurial.TryInter); most
// are empty.
//
// Facing and footprints. A sarcophagus is 1x2 and rotatable; LairRoom.TrySpawnOver is given
// the pair of cells it should cover and finds the centre itself. The head end is assumed to be
// the end the rotation points to (not verifiable here: the art ships in the game's asset
// bundles).
public class LairRoomWorker_Tomb : LairRoomWorker
{
    private const float BodyChance = 0.3f;
    private const int MaxBodies = 3;

    public override void Fill(LairRoom room)
    {
        LairStyle style = room.Style;
        int wall = room.HalfAcross;
        int deep = room.HalfDeep;

        if (wall >= 4)
        {
            room.Floor(style.Carpet(2), (right, forward) => Math.Abs(right) == 2);
        }
        room.Floor(style.Carpet(0), (right, forward) => Math.Abs(right) <= 1);

        int bodies = 0;
        int gap = 0;
        for (int forward = -deep + 2; forward <= deep - 2; forward += 2, gap++)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                Rot4 head = side > 0 ? Rot4.East : Rot4.West;
                Thing sarcophagus = room.TrySpawnOver(ThingDefOf.Sarcophagus, side * wall, forward, side * (wall - 1), forward, head, paint: style.Paint(0));
                if (sarcophagus != null && bodies < MaxBodies && Rand.Chance(BodyChance) && LairBurial.TryInter(sarcophagus))
                {
                    bodies++;
                }
            }
            if (gap % 2 == 0 && forward + 1 < deep)
            {
                room.TryTorch(room.At(wall, forward + 1));
                room.TryTorch(room.At(-wall, forward + 1));
            }
        }
        room.TryTorch(room.At(wall, deep));
        room.TryTorch(room.At(-wall, deep));

        // The focal piece. A room too short for the stele (or one with a door in its far wall)
        // gets a sculpture at the aisle's end instead.
        Thing stele = room.TrySpawnOver(ThingDefOf.SteleGrand, -1, deep - 2, 1, deep, Rot4.North, paint: style.Paint(1));
        if (stele == null)
        {
            room.TrySpawn(SL_LairDefOf.SculptureLarge, 0, deep, Rot4.North, paint: style.Paint(1));
        }
    }
}
