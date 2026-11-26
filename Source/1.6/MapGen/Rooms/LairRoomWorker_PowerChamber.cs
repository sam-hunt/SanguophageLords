using RimWorld;
using Verse;

namespace SanguophageLords;

// The power chamber: where the lair draws on the heat of the deep rock. A geothermal
// generator over a steam geyser, a bank of batteries against the side walls as a buffer,
// and nothing else but a pair of blood torches by the door: a hot, bare, rock-hewn service
// room. GenStep_LairUtilities later runs conduits from the generator to the batteries and to
// every powered building in the lair.
//
// Layout in the room's local frame (the def gives 11~13 across, 9~11 deep):
//   generator   6x6, centred across, its back edge one cell short of the far wall so a lane
//               runs all the way round it; tried one row nearer the door at a time if the
//               ideal rows will not take it
//   batteries   1x2, standing along both side walls from the generator's front row forward,
//               up to two a side, in mirrored pairs, full at generation
//   torches     a mirrored pair in the corners either side of the door
// An even-sized footprint cannot sit on an odd room's centre line, so the generator stands
// half a cell to one side and its lanes differ by one cell; that is the only asymmetry.
//
// Engine facts this relies on (decompile-verified, 1.6 and 1.7 alike):
// - GeothermalGenerator is 6x6 and SteamGeyser 2x2, both non-rotating; the build designator
//   requires the generator's Position to be the geyser's (PlaceWorker_OnSteamGeyser), which
//   with GenAdj.OccupiedRect puts the geyser in the middle of the generator. A geyser is not
//   an edifice and cannot be destroyed, so spawning it under the finished generator wipes
//   neither; LairRoom.TrySpawn refuses any overlap, which is why the geyser alone is spawned
//   directly.
// - CompPowerPlant switches itself on at spawn and CompPowerPlantSteam's output does not
//   depend on the geyser, so the generator gives its full 3600 W from the first tick, fuel-
//   free; it only stops for a breakdown (mean time 228 days per building). While harvested
//   the geyser stops venting and the generator's own sprayer pushes the heat instead
//   (IntermittentSteamSprayer), which keeps the chamber hot.
public class LairRoomWorker_PowerChamber : LairRoomWorker
{
    private const int GeneratorSize = 6;
    private const int BatteriesPerSide = 2;

    public override void Fill(LairRoom room)
    {
        Thing generator = PlaceGenerator(room);
        if (generator != null)
        {
            GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.SteamGeyser), generator.Position, room.Map);
        }

        int wall = room.HalfAcross;
        int front = room.HalfDeep - GeneratorSize;
        for (int i = 0; i < BatteriesPerSide; i++)
        {
            int forward = front + (2 * i);
            Charge(room.TrySpawnOver(ThingDefOf.Battery, wall, forward, wall, forward + 1, Rot4.North));
            Charge(room.TrySpawnOver(ThingDefOf.Battery, -wall, forward, -wall, forward + 1, Rot4.North));
        }

        for (int forward = -room.HalfDeep; forward < front; forward++)
        {
            if (room.IsFree(wall, forward) && room.IsFree(-wall, forward))
            {
                room.TryTorch(room.At(wall, forward));
                room.TryTorch(room.At(-wall, forward));
                break;
            }
        }
    }

    private static Thing PlaceGenerator(LairRoom room)
    {
        for (int back = room.HalfDeep - 1; back - GeneratorSize + 1 >= -room.HalfDeep; back--)
        {
            int frontRow = back - GeneratorSize + 1;
            Thing generator = room.TrySpawnOver(ThingDefOf.GeothermalGenerator, -2, frontRow, 3, back, Rot4.North)
                ?? room.TrySpawnOver(ThingDefOf.GeothermalGenerator, -3, frontRow, 2, back, Rot4.North);
            if (generator != null)
            {
                return generator;
            }
        }
        return null;
    }

    private static void Charge(Thing battery)
    {
        battery?.TryGetComp<CompPowerBattery>()?.SetStoredEnergyPct(1f);
    }
}
