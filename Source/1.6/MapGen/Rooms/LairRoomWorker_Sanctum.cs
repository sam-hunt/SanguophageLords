using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SanguophageLords;

// The sanctum: the great octagonal chamber at the end of the processional, where the lair's
// master deathrests. It is the room the Biotech reference art is built around, and is laid
// out the same way, in the room's local frame with the casket at the centre:
//
//   - The casket lies on the axis, head toward the far wall.
//   - Six deathrest buildings radiate from it: an accelerator before and behind it on the
//     axis, and two pumps either side. All are bound to the master, whose deathrest lines then
//     draw to each of them.
//   - Four columns stand around that group. They are the roof: the chamber is 21 to 25 cells
//     across, so its centre is far beyond the 6.9 cells thick rock roof spans from the walls,
//     and these four bring every cell back within reach of a holder.
//   - A two-cell ring of scarlet carpet circles the whole group, six cells out from the
//     centre whatever the chamber's size, an octagon parallel to the walls.
//   - Each side that has a doorway gets a pair of large sculptures flanking it; a side with
//     none gets a single one where the door would be. Blood torches line the walls.
//
// Power: the buildings here draw 2200 W between them (800 W per accelerator, 100 W for each
// pump and the casket); the utilities step connects them to the lair's power net.
public class LairRoomWorker_Sanctum : LairRoomWorker
{
    // Distance from the centre to the inner edge of the carpet ring.
    private const int CarpetRadius = 6;

    public override void Fill(LairRoom room)
    {
        int half = room.HalfAcross;
        room.FloorBand(room.Style.Carpet(0), CarpetRadius, CarpetRadius + 2);

        Thing casket = room.TrySpawnOver(ThingDefOf.DeathrestCasket, 0, -1, 0, 0, Rot4.North);
        var bound = new List<Thing>();
        void Bind(ThingDef def, int right1, int forward1, int right2, int forward2, Rot4 localRot)
        {
            Thing thing = room.TrySpawnOver(def, right1, forward1, right2, forward2, localRot);
            if (thing != null)
            {
                bound.Add(thing);
            }
        }
        Bind(SL_LairDefOf.DeathrestAccelerator, 0, 3, 0, 4, Rot4.North);
        Bind(SL_LairDefOf.DeathrestAccelerator, 0, -5, 0, -4, Rot4.North);
        Bind(SL_LairDefOf.Hemopump, -4, 1, -3, 1, Rot4.East);
        Bind(SL_LairDefOf.HemogenAmplifier, 3, 1, 4, 1, Rot4.East);
        Bind(SL_LairDefOf.GlucosoidPump, -4, -2, -3, -2, Rot4.East);
        Bind(SL_LairDefOf.PsychofluidPump, 3, -2, 4, -2, Rot4.East);

        room.TrySpawnPair(ThingDefOf.Column, 4, 4, Rot4.North, paint: room.Style.WallPaint);
        room.TrySpawnPair(ThingDefOf.Column, 4, -4, Rot4.North, paint: room.Style.WallPaint);

        // The four faces of the octagon a door can be in, as (right, forward) of the floor
        // cell just inside the middle of each, with the step along that face.
        FlankFace(room, 0, -half, 1, 0);
        FlankFace(room, 0, half, 1, 0);
        FlankFace(room, -half, 0, 0, 1);
        FlankFace(room, half, 0, 0, 1);

        room.TorchesAlongWalls(6);

        if (casket != null)
        {
            room.TrySpawnDeathrester(LairPawnTier.Master, casket, bound);
        }
    }

    private static void FlankFace(LairRoom room, int right, int forward, int stepRight, int stepForward)
    {
        ThingDef sculpture = SL_LairDefOf.SculptureLarge;
        if (room.IsDoorway(room.At(right, forward)))
        {
            room.TrySpawn(sculpture, right + (2 * stepRight), forward + (2 * stepForward), Rot4.South, paint: room.Style.Paint(0));
            room.TrySpawn(sculpture, right - (2 * stepRight), forward - (2 * stepForward), Rot4.South, paint: room.Style.Paint(0));
        }
        else
        {
            room.TrySpawn(sculpture, right, forward, Rot4.South, paint: room.Style.Paint(0));
        }
    }
}
