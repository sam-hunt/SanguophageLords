using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace SanguophageLords;

// GenStep of SL_LairDefenders: turns the defenders the room workers spawned (LairSite.Defenders,
// filled by LairPawns) into lords, triggers and the alarm. Runs after SL_LairContents, so every
// room has had its say and the contents step's safety nets have run, and before Fog.
//
// Per room with defenders:
//  - its guards join one guard lord posted at the room's centre;
//  - its sleepers and deathresters join one sleeping lord, and a RectTrigger over the room's
//    bounds sends that lord's room signal when a colonist steps in.
// Then one LairAlarm for the whole lair. LordJob_LairDefend and LairAlarm describe what each
// does; LairDefenseRules holds the numbers.
//
// Signals are named per map ("SL_Lair.<map id>.Room<plan room id>" and "...Alarm"): the
// SignalManager is global, so two lairs open at once must not hear each other.
//
// RectTrigger (Verse/RectTrigger.cs, identical in 1.6 and 1.7) checks its rect every 60 ticks
// for a humanlike pawn of the player's faction, sends its signal and destroys itself.
// destroyIfUnfogged stays false, so seeing into a room from its doorway does not count as
// entering it. A round or octagonal room's bounds take in its corners too, which lie behind
// its own walls; a corner could reach a neighbouring corridor, waking the room a step early.
//
// Defenders whose bed or casket was removed by LairRoom.EnsureWalkable after they were placed
// still join their lords: a sleeper then sleeps where it lies (JobGiver_LairSleep), and a
// deathrester stays in deathrest on the floor.
public class GenStep_LairDefenders : GenStep
{
    public override int SeedPart => 1544381209;

    public override void Generate(Map map, GenStepParams parms)
    {
        LairSite site = LairSite.Current;
        if (site == null || site.Defenders.Count == 0)
        {
            return;
        }
        Faction faction = site.Faction;
        string prefix = "SL_Lair." + map.uniqueID;
        string alarmSignal = prefix + ".Alarm";
        List<LairDefender> defenders = site.Defenders.Where(d => d.Pawn.Spawned && d.Pawn.Map == map && !d.Pawn.Dead).ToList();

        foreach (IGrouping<LairRoom, LairDefender> group in defenders.GroupBy(d => d.Room))
        {
            LairRoom room = group.Key;
            List<Pawn> guards = group.Where(d => d.Role == LairPawnRole.Guard).Select(d => d.Pawn).ToList();
            List<Pawn> sleepers = group.Where(d => d.Role != LairPawnRole.Guard).Select(d => d.Pawn).ToList();
            if (guards.Count > 0)
            {
                float defendRadius = LairDefenseRules.GuardDefendRadius(room.HalfAcross, room.HalfDeep);
                float wanderRadius = LairDefenseRules.GuardWanderRadius(room.HalfAcross, room.HalfDeep);
                LordMaker.MakeNewLord(faction, LordJob_LairDefend.Guards(faction, room.Center, defendRadius, wanderRadius, alarmSignal), map, guards);
            }
            if (sleepers.Count > 0)
            {
                string roomSignal = prefix + ".Room" + room.Plan.Id;
                CellRect area = room.Plan.Bounds;
                LordMaker.MakeNewLord(faction, LordJob_LairDefend.Sleepers(faction, area, roomSignal, alarmSignal), map, sleepers);
                var trigger = (RectTrigger)ThingMaker.MakeThing(ThingDefOf.RectTrigger);
                trigger.signalTag = roomSignal;
                trigger.Rect = area;
                GenSpawn.Spawn(trigger, area.CenterCell, map);
            }
        }

        var alarm = (LairAlarm)ThingMaker.MakeThing(SL_LairPawnDefOf.SL_LairAlarm);
        alarm.Configure(faction, alarmSignal, defenders);
        GenSpawn.Spawn(alarm, map.Center, map);

        if (Prefs.DevMode)
        {
            LairPawnBudget budget = site.PawnBudget;
            Log.Message("[Sanguophage Lords] Defenders: " + defenders.Count(d => d.Role == LairPawnRole.Guard) + " guards, "
                + defenders.Count(d => d.Role == LairPawnRole.Sleeper) + " sleepers, "
                + defenders.Count(d => d.Role == LairPawnRole.Deathrester) + " deathresters; budget used "
                + budget.Taken(LairPawnTier.Master) + "/" + budget.Cap(LairPawnTier.Master) + " master, "
                + budget.Taken(LairPawnTier.Sanguophage) + "/" + budget.Cap(LairPawnTier.Sanguophage) + " sanguophages, "
                + budget.Taken(LairPawnTier.Thrall) + "/" + budget.Cap(LairPawnTier.Thrall) + " thralls, "
                + budget.Prisoners + "/" + LairPawnBudget.MaxPrisoners + " prisoners.");
        }
    }
}
