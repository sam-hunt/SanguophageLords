using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SanguophageLair;

// A lesser deathrest chamber: a small round room beside the sanctum where one of the lair's
// other sanguophages deathrests. The sanctum in miniature: the casket at the centre with its
// head to the far wall, three pumps around it (one beyond the head, one either side), a round
// band of scarlet carpet just inside the wall, and blood torches against the far wall.
//
// No accelerator here: at 800 W each they are the master's privilege, and leaving them out
// keeps a pair of these chambers to 800 W together on the lair's power net.
public class LairRoomWorker_DeathrestChamber : LairRoomWorker
{
    public override void Fill(LairRoom room)
    {
        room.FloorBand(room.Style.Carpet(0), room.HalfAcross - 1.6f, room.HalfAcross - 0.6f);

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
        Bind(SL_LairDefOf.Hemopump, 0, 2, 0, 3, Rot4.North);
        Bind(SL_LairDefOf.HemogenAmplifier, 2, 0, 3, 0, Rot4.East);
        Bind(SL_LairDefOf.GlucosoidPump, -3, 0, -2, 0, Rot4.East);

        room.TorchesAlongWalls(5);

        if (casket != null)
        {
            room.TrySpawnDeathrester(LairPawnTier.Sanguophage, casket, bound);
        }
    }
}
