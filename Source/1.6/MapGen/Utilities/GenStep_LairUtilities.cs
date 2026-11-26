using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SanguophageLords;

// GenStep of SL_LairUtilities: lays the lair's utility lines once every room is furnished.
// First the power net (LairPowerNet: conduits from the power chamber's generator to every
// powered building), then, when Vanilla Races Expanded - Sanguophage is active, the hemogen
// pipe network (LairHemogenNet). Both route with GridRouter over the lair's circulation
// (LairCirculation) plus the map costs below, so the two networks share the corridors and
// keep to the edges of rooms in the same way. Does nothing when the structure step could not
// place a lair.
//
// Order: after SL_LairContents, which spawns everything the lines connect, and before the
// defenders and Fog. Each network runs inside its own try/catch: a lair without power or
// without pipes is still a lair.
//
// Engine facts the map costs rely on (decompile-verified, 1.6 and 1.7 alike):
// - A power conduit or pipe is a non-edifice building on the Conduits altitude, so
//   GenSpawn.SpawningWipes lets it lie under doors, walls and furniture without either
//   removing the other; the reverse is not true for power: a power-transmitting building
//   spawned over a conduit wipes it, which is why lines are laid only after every room is
//   furnished.
// - Terrain affordance is checked only by the build designators, so mapgen may lay a line on
//   any floor; the plan's circulation is all solid floor or bridge paving anyway.
public class GenStep_LairUtilities : GenStep
{
    // What a line pays on top of the plan's cost (LairCirculation) where the map has
    // something the line should rather avoid: carpet (visible lines on a carpet read as
    // clutter) and furniture (a visible line vanishing under a sculpture reads as an
    // accident). Large enough that a line goes round a single piece by the wall instead.
    public const int CarpetCost = 6;
    public const int FurnitureCost = 6;

    // Charged per bend; see GridRouter.
    public const int TurnCost = 2;

    public override int SeedPart => 1407713393;

    public override void Generate(Map map, GenStepParams parms)
    {
        LairSite site = LairSite.Current;
        if (site == null)
        {
            return;
        }
        Faction owner = Owner(site);
        try
        {
            LairPowerNet.Lay(site, owner);
        }
        catch (Exception e)
        {
            Log.Error("[Sanguophage Lords] Laying the lair's power net failed: " + e);
        }
        try
        {
            LairHemogenNet.Lay(site, owner);
        }
        catch (Exception e)
        {
            Log.Error("[Sanguophage Lords] Laying the lair's hemogen pipes failed: " + e);
        }
    }

    // The faction the lines belong to. LairSite names no owner, so they are laid unowned,
    // which the engine handles like any ruin's wiring.
    private static Faction Owner(LairSite site)
    {
        return null;
    }

    // The plan's circulation costs plus what the map adds on top: carpet, and furniture other
    // than doors and existing lines.
    public static int[] Costs(LairSite site)
    {
        Map map = site.Map;
        LairPlan plan = site.Plan;
        int[] cost = LairCirculation.Costs(plan);
        for (int z = 0; z < plan.Height; z++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                int i = (z * plan.Width) + x;
                if (cost[i] <= 0)
                {
                    continue;
                }
                var c = new IntVec3(x, 0, z);
                TerrainDef terrain = c.GetTerrain(map);
                if (terrain != null && terrain.IsCarpet)
                {
                    cost[i] += CarpetCost;
                }
                if (HasFurniture(c, map))
                {
                    cost[i] += FurnitureCost;
                }
            }
        }
        return cost;
    }

    private static bool HasFurniture(IntVec3 c, Map map)
    {
        List<Thing> things = c.GetThingList(map);
        for (int i = 0; i < things.Count; i++)
        {
            ThingDef def = things[i].def;
            if (def.category != ThingCategory.Building || def.IsDoor || def.altitudeLayer == AltitudeLayer.Conduits)
            {
                continue;
            }
            if (def.passability != Traversability.Standable || def.IsEdifice())
            {
                return true;
            }
        }
        return false;
    }

    // A building the network must reach. One that carries the network itself (a battery, a
    // hemogen vat) is reached from a cell sharing an edge with it and then joins the network
    // with its whole footprint, which no line may cross before then. One that only draws from
    // it (a power consumer, wired to the nearest conduit within six cells) is reached by a
    // line on or around it, corners included.
    public static RouteTarget TargetFor(Thing thing, bool carriesNetwork, int[] cost, int width)
    {
        var target = new RouteTarget();
        CellRect footprint = thing.OccupiedRect();
        if (carriesNetwork)
        {
            foreach (IntVec3 c in footprint)
            {
                target.Joins.Add(c);
                cost[(c.z * width) + c.x] = 0;
            }
            target.Reach.AddRange(GenAdj.CellsAdjacentCardinal(thing));
        }
        else
        {
            foreach (IntVec3 c in footprint)
            {
                target.Reach.Add(c);
            }
            target.Reach.AddRange(GenAdj.CellsAdjacent8Way(thing));
        }
        return target;
    }

    // Lines are visible unless the layout hides them all; under carpet they are always hidden.
    public static bool Hidden(LairSite site, IntVec3 c)
    {
        if (site.Layout.hiddenConduits)
        {
            return true;
        }
        TerrainDef terrain = c.GetTerrain(site.Map);
        return terrain != null && terrain.IsCarpet;
    }

    public static IEnumerable<IntVec3> Footprint(Thing thing)
    {
        foreach (IntVec3 c in thing.OccupiedRect())
        {
            yield return c;
        }
    }

    // Buildings standing in the lair's rooms (the stamped walls and doors included).
    public static IEnumerable<Thing> LairBuildings(LairSite site)
    {
        List<Thing> buildings = site.Map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial);
        for (int i = 0; i < buildings.Count; i++)
        {
            if (site.Plan.RoomIdAt(buildings[i].Position) >= 0)
            {
                yield return buildings[i];
            }
        }
    }
}
