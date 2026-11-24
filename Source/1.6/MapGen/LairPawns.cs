using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace SanguophageLair;

// Who a lair pawn is, from the room workers' point of view. Which PawnKindDef each tier is
// belongs to LairPawns, so the lair's roster can change without touching a single room.
public enum LairPawnTier
{
    // A baseliner servant of the lair.
    Thrall,
    // One of the lair's sanguophages.
    Sanguophage,
    // The sanguophage the lair belongs to.
    Master,
}

// Creates the lair's inhabitants for the room workers, through the LairRoom.TrySpawn* methods,
// which are the only callers. Each method returns null, spawning nothing, when the request is
// refused: the pawn budget (LairSite.PawnBudget) is spent for that tier, the bed is unusable,
// or the room has no free cell. Workers already cope with null.
//
// What each method leaves behind, for GenStep_LairDefenders to turn into lords:
//  - SpawnGuard: an awake pawn on a free cell of the room, facing the entrance.
//  - SpawnSleeper: a pawn owning the bed and already asleep in it (JobGiver_LairSleep's job).
//  - SpawnDeathrester: a sanguophage deathresting in the casket, bound to it and to the given
//    buildings. Recipe below.
//  - SpawnPrisoner: a captive, not a defender, so not registered.
// Beds, caskets and bound buildings that have no faction are given the lair's, which deathrest
// binding needs (Gene_Deathrest.CanBindToBindable checks it on later deathrests) and which
// makes damage to them reach the room's lord (Building.PreApplyDamage notifies only lords of
// the building's own faction). Refuelable bound buildings are topped up.
//
// One choke point, GenerateDefender, makes every defender, and raises PawnGenerated after
// generating it and before spawning it. That event is the hook for other assemblies: Hemophage
// Warcasket subscribes to dress lair pawns in its armour. Our pawn kinds (one per tier,
// 1.6/Defs/PawnKindDefs/LairPawnKinds.xml) are the matching XML hook, with stable defNames.
// Prisoners are made by the same generator call but do not raise the event.
//
// Deathrest recipe, decompile-verified, identical in 1.6 and 1.7 (RimWorld/Gene_Deathrest.cs,
// JobDriver_Deathrest.cs, Toils_LayDown.cs, Pawn_Ownership.cs, Verse/Pawn_HealthTracker.cs,
// Pawn.cs, Verse.AI/Pawn_JobTracker.cs):
//  1. Claim the casket (Pawn_Ownership.ClaimBedIfNonMedical treats a DeathrestCasket as the
//     assigned casket) and spawn the pawn on its sleeping slot. Ownership matters:
//     RestUtility.CanUseBedNow, checked every tick by the lay-down toil, fails a pawn lying in
//     a bed it does not own.
//  2. Bind the buildings explicitly with Gene_Deathrest.BindTo, before deathrest starts.
//     BindTo does no room or line-of-sight check, which matters because rooms do not exist
//     until Map.FinalizeInit; and binding first leaves nothing for the deathrest start's own
//     TryLinkToNearbyDeathrestBuildings to grab from a neighbour's set. Capacity is raised with
//     OffsetCapacity to the number of buildings that count toward it.
//  3. Start JobDefOf.Deathrest on the casket the way Pawn_JobTracker.Notify_TuckedIntoBed
//     does. Its lay-down toil sets the in-bed posture and then adds HediffDefOf.Deathrest, so
//     the pawn is downed (Consciousness capped at 0.1) while already in bed, and MakeDowned's
//     DropAndForbidEverything(keepInventoryAndEquipmentIfInBed) keeps its weapon and gear.
//     Adding the hediff first would drop them on the floor.
//  4. Gene_Deathrest.autoWake stays false, so the pawn never wakes by itself; only
//     TransitionAction_LairWake (or sunlight, which the roofed lair keeps out) ends it.
//     deathrestTicks starts partway through, so the inspector shows a plausible progress.
public static class LairPawns
{
    private const float PrisonerBloodLossMin = 0.25f;
    private const float PrisonerBloodLossMax = 0.45f;
    private const float DeathrestProgressMin = 0.3f;
    private const float DeathrestProgressMax = 0.9f;
    private const int StandCellSearchRadius = 3;

    // Raised for every lair defender after generation and before spawning: the pawn, its tier,
    // and the lair map being generated. Handlers may change the pawn's gear, hediffs and the
    // like; they must not spawn or destroy it. A throwing handler is logged and skipped.
    public static event Action<Pawn, LairPawnTier, Map> PawnGenerated;

    public static PawnKindDef KindFor(LairPawnTier tier)
    {
        switch (tier)
        {
            case LairPawnTier.Master:
                return SL_LairPawnDefOf.SL_LairMaster;
            case LairPawnTier.Sanguophage:
                return SL_LairPawnDefOf.SL_LairSanguophage;
            default:
                return SL_LairPawnDefOf.SL_LairThrall;
        }
    }

    // The budget for a lair bought with the given threat points, priced at the kinds'
    // combatPower.
    public static LairPawnBudget MakeBudget(float points)
    {
        return new LairPawnBudget(points, KindFor(LairPawnTier.Thrall).combatPower, KindFor(LairPawnTier.Sanguophage).combatPower, KindFor(LairPawnTier.Master).combatPower);
    }

    // An awake defender standing guard near a cell of the room.
    public static Pawn SpawnGuard(LairRoom room, LairPawnTier tier, IntVec3 near)
    {
        if (room == null || !TryFindStandCell(room, near, out IntVec3 cell) || !room.Site.PawnBudget.TryTake(tier, awake: true))
        {
            return null;
        }
        Pawn pawn = GenerateDefender(room, tier);
        // Rested, so a guard does not leave its post for a nap soon after the player arrives
        // (the Defend duty lets pawns satisfy their needs).
        if (pawn.needs?.rest != null)
        {
            pawn.needs.rest.CurLevel = 1f;
        }
        GenSpawn.Spawn(pawn, cell, room.Map, room.Turn(Rot4.South));
        Register(room, pawn, tier, LairPawnRole.Guard);
        return pawn;
    }

    // A defender asleep in an ordinary bed of the room, who wakes when the lair is alerted.
    public static Pawn SpawnSleeper(LairRoom room, LairPawnTier tier, Thing bed)
    {
        if (room == null || !(bed is Building_Bed building) || !IsFreeBed(room, building) || building.def == ThingDefOf.DeathrestCasket || !room.Site.PawnBudget.TryTake(tier, awake: false))
        {
            return null;
        }
        Pawn pawn = GenerateDefender(room, tier);
        PrepareBuilding(building, room.Site.Faction);
        SpawnInBed(pawn, building, room.Map);
        StartJobInBed(pawn, JobGiver_LairSleep.SleepJob(pawn, building));
        Register(room, pawn, tier, LairPawnRole.Sleeper);
        return pawn;
    }

    // A sanguophage deathresting in a casket, bound to the given deathrest buildings.
    public static Pawn SpawnDeathrester(LairRoom room, LairPawnTier tier, Thing casket, IEnumerable<Thing> boundBuildings)
    {
        if (room == null || !LairDefenseRules.CanDeathrest(tier) || !(casket is Building_Bed bed) || !IsFreeBed(room, bed) || !room.Site.PawnBudget.TryTake(tier, awake: false))
        {
            return null;
        }
        Pawn pawn = GenerateDefender(room, tier);
        Gene_Deathrest gene = pawn.genes?.GetFirstGeneOfType<Gene_Deathrest>();
        if (gene == null)
        {
            // Only another mod changing the xenotype can get here. The pawn keeps watch
            // beside the casket instead.
            Log.Warning("[Sanguophage Lair] " + pawn.LabelShort + " was generated without the deathrest gene; posting it as a guard.");
            if (!TryFindStandCell(room, bed.Position, out IntVec3 cell))
            {
                Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.Discard);
                return null;
            }
            GenSpawn.Spawn(pawn, cell, room.Map, room.Turn(Rot4.South));
            Register(room, pawn, tier, LairPawnRole.Guard);
            return pawn;
        }

        Faction faction = room.Site.Faction;
        PrepareBuilding(bed, faction);
        var comps = new List<CompDeathrestBindable>();
        AddBindable(bed, gene, comps);
        if (boundBuildings != null)
        {
            foreach (Thing building in boundBuildings)
            {
                if (building != null && building != bed && building.Spawned && building.Map == room.Map)
                {
                    PrepareBuilding(building, faction);
                    AddBindable(building, gene, comps);
                }
            }
        }
        SpawnInBed(pawn, bed, room.Map);
        foreach (CompDeathrestBindable comp in comps)
        {
            if (!gene.BindingWillExceedStackLimit(comp))
            {
                gene.BindTo(comp);
            }
        }
        if (gene.CurrentCapacity > gene.DeathrestCapacity)
        {
            gene.OffsetCapacity(gene.CurrentCapacity - gene.DeathrestCapacity, sendNotification: false);
        }
        StartJobInBed(pawn, JobMaker.MakeJob(JobDefOf.Deathrest, bed));
        if (!pawn.Deathresting)
        {
            pawn.health.AddHediff(HediffDefOf.Deathrest);
        }
        gene.autoWake = false;
        gene.deathrestTicks = (int)(gene.MinDeathrestTicks * Rand.Range(DeathrestProgressMin, DeathrestProgressMax));
        Register(room, pawn, tier, LairPawnRole.Deathrester);
        return pawn;
    }

    // A captive kept for blood: a prisoner of the lair, in the given bed when there is one,
    // else standing near the cell.
    //
    // The vanilla prisoner-rescue shape (RimWorld/PrisonerWillingToJoinQuestUtility.cs and
    // GenStep_PrisonerWillingToJoin.cs, identical in 1.6 and 1.7): a Slave-kind pawn of the
    // captor's faction, made its prisoner before spawning (SetGuestStatus drops gear only from
    // a spawned pawn), with Pawn_MindState.WillJoinColonyIfRescued set. That flag makes the
    // pawn non-hostile to the player (GenHostility.HostileTo) and offers the "Free prisoner"
    // order (FloatMenuOptionProvider_OfferHelp), on which it joins the colony. Being a
    // prisoner of the lair's faction also keeps the lair's pawns from attacking it.
    public static Pawn SpawnPrisoner(LairRoom room, Thing bed, IntVec3 near)
    {
        if (room == null)
        {
            return null;
        }
        Building_Bed building = bed as Building_Bed;
        if (building != null && (!IsFreeBed(room, building) || building.def == ThingDefOf.DeathrestCasket || !building.def.building.bed_humanlike))
        {
            building = null;
        }
        IntVec3 cell = IntVec3.Invalid;
        if ((building == null && !TryFindStandCell(room, near, out cell)) || !room.Site.PawnBudget.TryTakePrisoner())
        {
            return null;
        }
        Faction faction = room.Site.Faction;
        var request = new PawnGenerationRequest(PawnKindDefOf.Slave, faction, PawnGenerationContext.NonPlayer, room.Map.Tile, forceGenerateNewPawn: true, canGeneratePawnRelations: true, colonistRelationChanceFactor: 75f, forceAddFreeWarmLayerIfNeeded: true, forcedXenotype: XenotypeDefOf.Baseliner);
        Pawn pawn = PawnGenerator.GeneratePawn(request);
        pawn.guest.SetGuestStatus(faction, GuestStatus.Prisoner);
        pawn.mindState.WillJoinColonyIfRescued = true;
        pawn.health.AddHediff(HediffDefOf.BloodfeederMark, ExecutionUtility.ExecuteCutPart(pawn));
        Hediff bloodLoss = HediffMaker.MakeHediff(HediffDefOf.BloodLoss, pawn);
        bloodLoss.Severity = Rand.Range(PrisonerBloodLossMin, PrisonerBloodLossMax);
        pawn.health.AddHediff(bloodLoss);

        if (building == null)
        {
            GenSpawn.Spawn(pawn, cell, room.Map, Rot4.Random);
            return pawn;
        }
        PrepareBuilding(building, faction);
        building.ForPrisoners = true;
        SpawnInBed(pawn, building, room.Map);
        StartJobInBed(pawn, JobMaker.MakeJob(JobDefOf.LayDown, building));
        return pawn;
    }

    // ----- Helpers -----

    // The one place lair defenders are generated.
    private static Pawn GenerateDefender(LairRoom room, LairPawnTier tier)
    {
        XenotypeDef xenotype = tier == LairPawnTier.Thrall ? XenotypeDefOf.Baseliner : XenotypeDefOf.Sanguophage;
        var request = new PawnGenerationRequest(KindFor(tier), room.Site.Faction, PawnGenerationContext.NonPlayer, room.Map.Tile, forceGenerateNewPawn: true, mustBeCapableOfViolence: true, forcedXenotype: xenotype);
        Pawn pawn = PawnGenerator.GeneratePawn(request);
        RaisePawnGenerated(pawn, tier, room.Map);
        return pawn;
    }

    private static void RaisePawnGenerated(Pawn pawn, LairPawnTier tier, Map map)
    {
        Action<Pawn, LairPawnTier, Map> handlers = PawnGenerated;
        if (handlers == null)
        {
            return;
        }
        foreach (Delegate handler in handlers.GetInvocationList())
        {
            try
            {
                ((Action<Pawn, LairPawnTier, Map>)handler)(pawn, tier, map);
            }
            catch (Exception e)
            {
                Log.Error("[Sanguophage Lair] A PawnGenerated handler (" + handler.Method.DeclaringType + "." + handler.Method.Name + ") failed for " + pawn.LabelShort + ": " + e);
            }
        }
    }

    private static void Register(LairRoom room, Pawn pawn, LairPawnTier tier, LairPawnRole role)
    {
        room.Site.Defenders.Add(new LairDefender(pawn, tier, role, room));
    }

    private static bool IsFreeBed(LairRoom room, Building_Bed bed)
    {
        return bed.Spawned && bed.Map == room.Map && !bed.Medical && !bed.ForPrisoners && bed.AnyUnownedSleepingSlot;
    }

    private static void PrepareBuilding(Thing building, Faction faction)
    {
        if (building.Faction == null && building.def.CanHaveFaction)
        {
            building.SetFaction(faction);
        }
        CompRefuelable refuelable = building.TryGetComp<CompRefuelable>();
        if (refuelable != null && refuelable.Fuel < refuelable.Props.fuelCapacity)
        {
            refuelable.Refuel(refuelable.Props.fuelCapacity - refuelable.Fuel);
        }
    }

    private static void AddBindable(Thing building, Gene_Deathrest gene, List<CompDeathrestBindable> comps)
    {
        CompDeathrestBindable comp = building.TryGetComp<CompDeathrestBindable>();
        if (comp != null && comp.BoundPawn == null && !gene.BoundComps.Contains(comp))
        {
            comps.Add(comp);
        }
    }

    // Claims the bed and puts the pawn on its sleeping slot (claim first: the slot is the
    // owner's).
    private static void SpawnInBed(Pawn pawn, Building_Bed bed, Map map)
    {
        pawn.ownership.ClaimBedIfNonMedical(bed);
        GenSpawn.Spawn(pawn, RestUtility.GetBedSleepingSlotPosFor(pawn, bed), map, bed.Rotation);
    }

    // Starts a job on a pawn already on its bed's sleeping slot, as
    // Pawn_JobTracker.Notify_TuckedIntoBed does, so it is lying in bed from the first frame.
    private static void StartJobInBed(Pawn pawn, Job job)
    {
        pawn.jobs.StartJob(job, JobCondition.InterruptForced, tag: JobTag.TuckedIntoBed, continueSleeping: true);
    }

    // A free standable cell of the room for a pawn: the given one, else one nearby, else the
    // room's free cell nearest to it.
    private static bool TryFindStandCell(LairRoom room, IntVec3 near, out IntVec3 cell)
    {
        Map map = room.Map;
        bool Free(IntVec3 c) => room.Contains(c) && !room.IsDoorway(c) && c.Standable(map) && c.GetFirstPawn(map) == null;
        if (Free(near))
        {
            cell = near;
            return true;
        }
        if (near.InBounds(map) && CellFinder.TryFindRandomCellNear(near, map, StandCellSearchRadius, Free, out cell))
        {
            return true;
        }
        cell = IntVec3.Invalid;
        int best = int.MaxValue;
        foreach (IntVec3 c in room.Cells)
        {
            int dist = (c - near).LengthHorizontalSquared;
            if (dist < best && Free(c))
            {
                cell = c;
                best = dist;
            }
        }
        return cell.IsValid;
    }
}
