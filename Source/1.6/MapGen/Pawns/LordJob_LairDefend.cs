using RimWorld;
using Verse;
using Verse.AI.Group;

namespace SanguophageLair;

// The lord of one room's defenders. GenStep_LairDefenders makes up to two per room: one for
// its guards, one for its sleepers and deathresters. Both end in the same assault on the
// intruders; they differ in how they start and what sets them off.
//
//   Guard:  LordToil_DefendPoint at the room's centre, defend radius reaching just past its
//           doors (LairDefenseRules). Guards engage whatever comes into range on their own,
//           through the Defend duty. On the alarm signal they leave their post and assault.
//   Sleep:  LordToil_LairSleep, everyone asleep in bed or in deathrest. Wakes the room
//           (TransitionAction_LairWake) and assaults when
//            - an intruder steps into the room: the room signal, sent by a RectTrigger over
//              its bounds;
//            - one of its pawns is harmed (Trigger_PawnHarmed, player instigators, as vanilla's
//              LordJob_SleepThenAssaultColony);
//            - a lair building in the room is damaged: a bed, casket or deathrest building.
//              Building.PreApplyDamage tells every lord of the building's faction, so the
//              trigger checks the building stands in this room's area;
//            - a sleeper hears clamor not made by the lair itself: an intruder walking within
//              earshot (Pawn_PathFollower raises Movement clamor every 12 cells, radius 7, through
//              open doors only), gunfire, a fellow being hurt. Deathresting pawns hear nothing
//              (Pawn.HearClamor), so a room of deathresters wakes only to the other triggers;
//            - the alarm signal, which wakes it without the "have woken up" message (LairAlarm
//              sends one for the whole lair).
//
// The assault is vanilla's LordJob_AssaultColony graph with kidnapping, stealing and the
// give-up timeout switched off: on a sealed pocket map there is no edge to carry a captive or
// loot to, and the lair's own loot is not something its owners steal. Vanilla's sleeping-lord
// job hardwires the full assault, which is why this job exists rather than a subclass of it.
//
// No flee toil: the faction def sets autoFlee false and AddFleeToil is false here, so the lair
// fights to the last. Persistence: the lord saves this job's fields; the graph, its lambdas
// and the assault subgraph are rebuilt by CreateGraph on load (Lord.ExposeData), and the
// current toil is restored by index.
//
// Engine facts relied on, decompile-verified and identical in 1.6 and 1.7
// (Verse.AI.Group/Lord.cs, Transition.cs, StateGraph.cs, Trigger_*.cs, LordToil_DefendPoint.cs,
// RimWorld/LordJob_AssaultColony.cs, LordJob_SleepThenAssaultColony.cs, Verse/Building.cs,
// Verse/Pawn.cs, Verse/GenClamor.cs):
//  - Lords are signal receivers (LordManager registers them), so Trigger_Signal hears
//    RectTrigger and LairAlarm signals directly.
//  - Transition.Execute runs pre-actions, moves to the target toil (assigning its duties), then
//    post-actions: pawns are woken already holding their assault duty.
public class LordJob_LairDefend : LordJob
{
    public enum Mode
    {
        Guard,
        Sleep,
    }

    private Faction faction;
    private Mode mode;
    private IntVec3 post;
    private float defendRadius;
    private float wanderRadius;
    private CellRect area;
    private string roomSignal;
    private string alarmSignal;

    public override bool AddFleeToil => false;

    public override bool GuiltyOnDowned => true;

    public LordJob_LairDefend()
    {
    }

    public static LordJob_LairDefend Guards(Faction faction, IntVec3 post, float defendRadius, float wanderRadius, string alarmSignal)
    {
        return new LordJob_LairDefend
        {
            faction = faction,
            mode = Mode.Guard,
            post = post,
            defendRadius = defendRadius,
            wanderRadius = wanderRadius,
            alarmSignal = alarmSignal,
        };
    }

    public static LordJob_LairDefend Sleepers(Faction faction, CellRect area, string roomSignal, string alarmSignal)
    {
        return new LordJob_LairDefend
        {
            faction = faction,
            mode = Mode.Sleep,
            area = area,
            roomSignal = roomSignal,
            alarmSignal = alarmSignal,
        };
    }

    public override StateGraph CreateGraph()
    {
        var graph = new StateGraph();
        LordToil start = mode == Mode.Guard
            ? new LordToil_DefendPoint(post, defendRadius, wanderRadius)
            : new LordToil_LairSleep();
        graph.AddToil(start);
        var assaultJob = new LordJob_AssaultColony(faction, canKidnap: false, canTimeoutOrFlee: false, sappers: false, useAvoidGridSmart: false, canSteal: false);
        LordToil assault = graph.AttachSubgraph(assaultJob.CreateGraph()).StartingToil;

        if (mode == Mode.Sleep)
        {
            var woken = new Transition(start, assault);
            woken.AddTrigger(new Trigger_Signal(roomSignal));
            woken.AddTrigger(new Trigger_PawnHarmed(1f, requireInstigatorWithFaction: false, Faction.OfPlayer));
            woken.AddTrigger(new Trigger_Custom(LairBuildingDamaged));
            woken.AddTrigger(new Trigger_Custom(HeardIntruder));
            string message = "MessageSleepingPawnsWokenUp".Translate(faction.def.pawnsPlural).CapitalizeFirst();
            woken.AddPreAction(new TransitionAction_Message(message, MessageTypeDefOf.ThreatBig, repeatAvoiderTag: null, repeatAvoiderSeconds: 1f, canSendMessage: AnyAsleep));
            woken.AddPostAction(new TransitionAction_LairWake());
            graph.AddTransition(woken);
        }

        var alarm = new Transition(start, assault);
        alarm.AddTrigger(new Trigger_Signal(alarmSignal));
        if (mode == Mode.Sleep)
        {
            alarm.AddPostAction(new TransitionAction_LairWake());
        }
        graph.AddTransition(alarm);
        return graph;
    }

    private bool LairBuildingDamaged(TriggerSignal signal)
    {
        return signal.type == TriggerSignalType.BuildingDamaged
            && signal.thing != null
            && area.Contains(signal.thing.Position)
            && signal.dinfo.Def != null
            && signal.dinfo.Def.ExternalViolenceFor(signal.thing);
    }

    private bool HeardIntruder(TriggerSignal signal)
    {
        if (signal.type != TriggerSignalType.Clamor)
        {
            return false;
        }
        // Movement clamor comes from every walking humanlike, the lair's own included.
        return signal.clamorType != ClamorDefOf.Movement || (signal.thing != null && signal.thing.Faction != faction);
    }

    private bool AnyAsleep()
    {
        foreach (Pawn pawn in lord.ownedPawns)
        {
            if (pawn.Spawned && !pawn.Dead && !pawn.Awake())
            {
                return true;
            }
        }
        return false;
    }

    public override void ExposeData()
    {
        Scribe_References.Look(ref faction, "faction");
        Scribe_Values.Look(ref mode, "mode", Mode.Guard);
        Scribe_Values.Look(ref post, "post");
        Scribe_Values.Look(ref defendRadius, "defendRadius", 0f);
        Scribe_Values.Look(ref wanderRadius, "wanderRadius", 0f);
        Scribe_Values.Look(ref area, "area");
        Scribe_Values.Look(ref roomSignal, "roomSignal");
        Scribe_Values.Look(ref alarmSignal, "alarmSignal");
    }
}
