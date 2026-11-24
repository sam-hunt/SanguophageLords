using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace SanguophageLair;

// The lair's power net: conduits from its generators (the power chamber's geothermal plant)
// to every powered building and battery standing in its rooms, grown as one shared tree by
// GridRouter (see GenStep_LairUtilities for the costs). Run by GenStep_LairUtilities. A lair
// whose planner could not fit the power chamber has no generator and is left unpowered.
//
// Engine facts this relies on (decompile-verified, 1.6 and 1.7 alike):
// - A building with a CompPowerTrader that does not transmit is wired at spawn, or when a
//   transmitter appears, to the nearest transmitter whose cell lies within 6 cells of its
//   Position on both axes (PowerConnectionMaker.BestTransmitterForConnector); it needs no
//   conduit underneath. Transmitters (conduits, batteries, generators) join one net through
//   cardinal adjacency only.
// - Spawning only queues those joins (PowerNetManager delayed actions); vanilla's own
//   GenStep_Power flushes them with UpdatePowerNetsAndConnections_First, refreshes each
//   CompPowerPlant's output and switches consumers on where the net has a surplus, which is
//   what is done here so the lair is powered on its first tick.
// - CompPowerPlantSteam produces its full output whether or not a geyser lies under it (the
//   geyser only matters to the build designator), so a generator that lost its geyser would
//   still run.
public static class LairPowerNet
{
    public static void Lay(LairSite site, Faction owner)
    {
        Map map = site.Map;
        var generators = new List<CompPowerPlant>();
        var consumers = new List<CompPowerTrader>();
        var batteries = new List<CompPowerBattery>();
        foreach (Thing thing in GenStep_LairUtilities.LairBuildings(site).ToList())
        {
            CompPowerTrader trader = thing.TryGetComp<CompPowerTrader>();
            if (trader is CompPowerPlant plant)
            {
                generators.Add(plant);
            }
            else if (trader != null && trader.Props.PowerConsumption > 0f)
            {
                consumers.Add(trader);
            }
            CompPowerBattery battery = thing.TryGetComp<CompPowerBattery>();
            if (battery != null)
            {
                batteries.Add(battery);
            }
        }
        if (generators.Count == 0)
        {
            if (Prefs.DevMode && consumers.Count > 0)
            {
                Log.Message("[Sanguophage Lair] Power: no power chamber in this lair; " + consumers.Count + " powered buildings stay unpowered.");
            }
            return;
        }

        LairPlan plan = site.Plan;
        int[] cost = GenStep_LairUtilities.Costs(site);
        var targets = new List<RouteTarget>();
        foreach (CompPowerTrader consumer in consumers)
        {
            targets.Add(GenStep_LairUtilities.TargetFor(consumer.parent, consumer.Props.transmitsPower, cost, plan.Width));
        }
        foreach (CompPowerBattery battery in batteries)
        {
            targets.Add(GenStep_LairUtilities.TargetFor(battery.parent, true, cost, plan.Width));
        }
        var sources = new List<IntVec3>();
        foreach (CompPowerPlant generator in generators)
        {
            sources.AddRange(GenStep_LairUtilities.Footprint(generator.parent));
        }
        RouteResult route = new GridRouter(plan.Width, plan.Height, cost, GenStep_LairUtilities.TurnCost).Route(sources, targets);

        int laid = 0;
        foreach (IntVec3 c in route.Laid)
        {
            if (c.GetTransmitter(map) != null)
            {
                continue;
            }
            ThingDef def = GenStep_LairUtilities.Hidden(site, c) ? ThingDefOf.HiddenConduit : ThingDefOf.PowerConduit;
            Thing conduit = GenSpawn.Spawn(ThingMaker.MakeThing(def), c, map);
            if (owner != null)
            {
                conduit.SetFaction(owner);
            }
            laid++;
        }

        map.powerNetManager.UpdatePowerNetsAndConnections_First();
        foreach (CompPowerPlant generator in generators)
        {
            generator.UpdateDesiredPowerOutput();
        }
        foreach (CompPowerTrader consumer in consumers)
        {
            if (!consumer.PowerOn && consumer.PowerNet != null && consumer.PowerNet.CurrentEnergyGainRate() > 1E-07f)
            {
                consumer.PowerOn = true;
            }
        }
        if (Prefs.DevMode)
        {
            Report(generators, consumers, batteries, laid);
        }
    }

    // Supply against demand, for reading in the harness log: whether one generator carries
    // the lair with every room it was given.
    private static void Report(List<CompPowerPlant> generators, List<CompPowerTrader> consumers, List<CompPowerBattery> batteries, int laid)
    {
        float supply = generators.Sum(g => g.PowerOutput);
        float fullLoad = consumers.Sum(c => c.Props.PowerConsumption);
        float drawing = consumers.Where(c => c.PowerOn).Sum(c => -c.PowerOutput);
        float stored = batteries.Sum(b => b.StoredEnergy);
        int offNet = consumers.Count(c => c.PowerNet == null);
        int nets = generators.Select(g => g.PowerNet).Concat(consumers.Select(c => c.PowerNet)).Where(n => n != null).Distinct().Count();
        string message = "[Sanguophage Lair] Power: " + generators.Count + " generator(s) supplying " + supply.ToString("F0") + " W; "
            + consumers.Count + " powered buildings drawing " + drawing.ToString("F0") + " W now, " + fullLoad.ToString("F0") + " W at full load; "
            + batteries.Count + " batteries holding " + stored.ToString("F0") + " Wd; " + laid + " conduit cells in " + nets + " net(s); "
            + offNet + " powered buildings off the net.";
        if (fullLoad > supply)
        {
            float shortfall = fullLoad - supply;
            message += " Full load exceeds supply by " + shortfall.ToString("F0") + " W; the batteries cover that for "
                + (stored / shortfall * 24f).ToString("F1") + " hours.";
        }
        Log.Message(message);
    }
}
