using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace SanguophageLords;

// The lair's hemogen pipe network, laid only when Vanilla Races Expanded - Sanguophage (VRE)
// is active: the draincasket room's vat feeds a network that runs through the lair's
// corridors to a tap beside the caskets of the sanctum and of each deathrest chamber, and to
// a second vat in the larder. Run by GenStep_LairUtilities after the power net, routed by the
// same GridRouter, avoiding the cells the conduits already took. The draincasket room's
// worker places its own vat, tap and caskets; the terminals in the other rooms are placed
// here, because those rooms' workers know nothing of VRE.
//
// Without VRE the program never asks for the draincasket room (its line carries a
// MayRequire), and this does nothing. The rooms are found by their defs and role, never by
// position.
//
// How a VRE network forms (VE Framework PipeSystem source, PipeNetManager.RegisterConnector
// and CompResource.PostSpawnSetup): every building with a CompResource registers itself at
// spawn, joining or merging the nets of any same-net CompResource on a cell sharing an edge
// with it or under it; with none it starts a new net. So spawning pipes cell by cell along a
// 4-connected run is enough, no designator or PlaceWorker involved, and every vat, tap and
// draincasket joins by touching a pipe or another of them edge to edge. A non-pipe network
// building spawned on a pipe destroys that pipe (CompResource.RemovePipes), so the pipes go
// in last and never under one: their footprints are closed to the router. PipeNetManager is
// a MapComponent, so it exists before any genstep runs.
public static class LairHemogenNet
{
    public const string PackageId = "vanillaracesexpanded.sanguophage";

    // The mod is loaded, under its own id or a _steam-suffixed duplicate (ModsConfig.IsActive
    // would miss the latter), and every def the network needs resolved.
    public static bool Active =>
        ModLister.GetActiveModWithIdentifier(PackageId, ignorePostfix: true) != null
        && SL_HemogenNetDefOf.VRE_Draincasket != null
        && SL_HemogenNetDefOf.VRE_HemogenVat != null
        && SL_HemogenNetDefOf.VRE_HemogenTap != null
        && SL_HemogenNetDefOf.VRE_HemogenPipe != null
        && SL_HemogenNetDefOf.VRE_UndergroundHemogenPipe != null;

    public static void Lay(LairSite site, Faction owner)
    {
        if (!Active)
        {
            return;
        }
        LairRoom drainRoom = site.Rooms.FirstOrDefault(r => r.Def == SL_HemogenNetDefOf.SL_LairRoom_DraincasketRoom);
        Thing source = drainRoom?.Spawned.FirstOrDefault(t => t.def == SL_HemogenNetDefOf.VRE_HemogenVat && t.Spawned);
        if (source == null)
        {
            return;
        }

        foreach (LairRoom room in site.Rooms)
        {
            if (room.Def == SL_HemogenNetDefOf.SL_LairRoom_Larder)
            {
                room.TrySpawnAgainstWall(SL_HemogenNetDefOf.VRE_HemogenVat, room.At(0, room.HalfDeep));
            }
            else if (room.Plan.Role == PlanRole.Sanctum || room.Def == SL_HemogenNetDefOf.SL_LairRoom_DeathrestChamber)
            {
                // Beside the casket area: the free wall-side cell nearest a casket, else the
                // far wall.
                Thing casket = room.Spawned.FirstOrDefault(t => t.def == ThingDefOf.DeathrestCasket && t.Spawned);
                room.TrySpawnAgainstWall(SL_HemogenNetDefOf.VRE_HemogenTap, casket != null ? casket.Position : room.At(0, room.HalfDeep));
            }
        }

        LairPlan plan = site.Plan;
        int[] cost = GenStep_LairUtilities.Costs(site);
        // Keep off the power lines where there is a way round: two lines in one cell draw
        // one over the other.
        for (int i = 0; i < cost.Length; i++)
        {
            if (cost[i] > 0 && new IntVec3(i % plan.Width, 0, i / plan.Width).GetTransmitter(site.Map) != null)
            {
                cost[i] += GenStep_LairUtilities.FurnitureCost;
            }
        }
        var members = new List<Thing>();
        foreach (Thing thing in GenStep_LairUtilities.LairBuildings(site).ToList())
        {
            if (thing.def == SL_HemogenNetDefOf.VRE_Draincasket || thing.def == SL_HemogenNetDefOf.VRE_HemogenVat || thing.def == SL_HemogenNetDefOf.VRE_HemogenTap)
            {
                members.Add(thing);
            }
        }
        var targets = new List<RouteTarget>();
        foreach (Thing member in members)
        {
            RouteTarget target = GenStep_LairUtilities.TargetFor(member, true, cost, plan.Width);
            if (member != source)
            {
                targets.Add(target);
            }
        }
        RouteResult route = new GridRouter(plan.Width, plan.Height, cost, GenStep_LairUtilities.TurnCost).Route(GenStep_LairUtilities.Footprint(source), targets);

        foreach (IntVec3 c in route.Laid)
        {
            ThingDef def = GenStep_LairUtilities.Hidden(site, c) ? SL_HemogenNetDefOf.VRE_UndergroundHemogenPipe : SL_HemogenNetDefOf.VRE_HemogenPipe;
            Thing pipe = GenSpawn.Spawn(ThingMaker.MakeThing(def), c, site.Map);
            if (owner != null)
            {
                pipe.SetFaction(owner);
            }
        }
        if (Prefs.DevMode)
        {
            Log.Message("[Sanguophage Lords] Hemogen network: " + route.Reached.Count(r => r) + " of " + targets.Count + " caskets, vats and taps reached from the draincasket room's vat; "
                + route.Laid.Count + " pipe cells laid.");
        }
    }
}
