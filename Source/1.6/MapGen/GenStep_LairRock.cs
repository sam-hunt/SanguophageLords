using System.Collections.Generic;
using Verse;
using Verse.Noise;

namespace SanguophageLords;

// GenStep of SL_LairRock: makes the whole pocket map one kind of rock, the one the surface
// stairwell was cut from, so the lair below is the same hill the player walked into.
//
// How (decompile-verified, 1.6): RockNoises.Init runs before any genstep and builds one noise
// per rock type of the map's world tile; GenStep_RocksFromGrid and GenStep_Terrain then pick,
// per cell, the rock whose noise is highest. A pocket map has no world tile, so that list is
// the same arbitrary two or three rocks for every pocket map in the world. Replacing the list
// with a single entry before those steps run (this step's order sits below theirs) leaves
// them only one rock to choose.
public class GenStep_LairRock : GenStep
{
    public override int SeedPart => 1273309485;

    public override void Generate(Map map, GenStepParams parms)
    {
        RockNoises.rockNoises = new List<RockNoises.RockNoise>
        {
            new RockNoises.RockNoise
            {
                rockDef = LairRock.OfEntrance(PocketMapUtility.currentlyGeneratingPortal),
                noise = new Const(1.0),
            },
        };
    }
}
