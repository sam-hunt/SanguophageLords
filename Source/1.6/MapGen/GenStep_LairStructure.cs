using RimWorld;
using Verse;

namespace SanguophageLords;

// GenStep of SL_LairStructure: plans a lair for this map and stamps its structure (rooms,
// walls, doors, floors, the approach cavern and its river) into the rock. Furnishing is the
// next steps' work; this one publishes the LairSite they read.
//
// Order: after Plants and RockChunks, so neither scatters into the rooms, and before Fog, so
// the fog flood from the stairwell stops at the lair's doors.
//
// The planner works in a frame with the approach to the south. A square map lets the finished
// plan be turned to any of the four directions, so lairs do not all face the same way; an
// oblong one keeps the plan as built or turns it about.
public class GenStep_LairStructure : GenStep
{
    public LairLayoutDef layout;

    public override int SeedPart => 648113977;

    public override void Generate(Map map, GenStepParams parms)
    {
        MapPortal entrance = PocketMapUtility.currentlyGeneratingPortal;
        LairPlan plan = LairPlanner.Generate(layout.ToPlanParams(map.Size.x, map.Size.z, undergroundApproach: true));
        if (plan == null)
        {
            // The dev-menu pocket map is 100 cells square, too small for a lair; the exit
            // step then falls back to a bare cave, as before this step existed.
            Log.Warning("[Sanguophage Lords] A " + map.Size.x + "x" + map.Size.z + " map is too small for the lair layout " + layout.defName + "; generating a bare cave instead.");
            return;
        }
        bool square = map.Size.x == map.Size.z;
        plan = plan.Rotated(square ? Rot4.Random : (Rand.Bool ? Rot4.North : Rot4.South));
        var site = new LairSite(map, plan, layout, LairStyle.Resolve(layout, LairRock.OfEntrance(entrance)));
        LairStamper.Stamp(site);
        site.Publish();
    }
}
