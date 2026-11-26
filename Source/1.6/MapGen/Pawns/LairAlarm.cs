using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace SanguophageLords;

// The lair's general alarm: an invisible thing (ThingDef SL_LairAlarm,
// 1.6/Defs/ThingDefs/LairAlarm.xml) that watches every defender and, when the lair should
// rouse as a whole, sends the alarm signal every LordJob_LairDefend lord of the map listens
// for, then destroys itself. One per lair, spawned by GenStep_LairDefenders.
//
// When it fires is LairDefenseRules' business (master wakes or is hurt, another sanguophage
// wakes plus a delay, enough defenders lost). "Hurt" reads Pawn_MindState.lastHarmTick, which
// only external violence sets (Pawn_MindState.Notify_DamageTaken); a master lost between two
// polls counts as hurt too. It polls every rare tick (250 ticks) instead of hooking pawn
// events, because vanilla has no event for "woke up" or "was captured" that a thing can hear
// without Harmony, and the alarm does not need to be instant.
//
// A defender counts as
//  - dormant while it deathrests or its lord is still in LordToil_LairSleep. The lord's state,
//    not Pawn.Awake, decides: a freshly spawned sleeper is briefly awake before its first job
//    starts, and a sleeper woken by noise goes back to sleep unless its lord woke.
//  - lost once it is dead, gone from the map, no longer of the lair's faction or a prisoner
//    (captured, recruited), or downed. A deathresting pawn is downed by its deathrest, so it
//    counts as lost only if it has been seen awake since (a sanguophage goes down into deathless
//    deathrest instead of dying) or if lethal injuries are what keep it there
//    (SanguophageUtility.ShouldBeDeathrestingOrInComaInsteadOfDead).
//
// Persistence: the defenders are saved as references, pruned of destroyed pawns before
// saving, the way Lord.ExposeData prunes its own. It is a Thing like vanilla's
// SignalAction_*, so it saves and reloads with the map and re-registers for signals on spawn
// (Thing.SpawnSetup, def.receivesSignals). If another source sends the alarm first, it hears
// it and removes itself.
public class LairAlarm : Thing
{
    public sealed class Entry : IExposable
    {
        public Pawn pawn;
        public LairPawnTier tier;
        public bool dormant;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref tier, "tier", LairPawnTier.Thrall);
            Scribe_Values.Look(ref dormant, "dormant", defaultValue: false);
        }
    }

    private Faction lairFaction;
    private string alarmSignal;
    private int lossesToRaise;
    private int losses;
    private int raiseAtTick = -1;
    private int configuredTick;
    private List<Entry> entries = new List<Entry>();

    public void Configure(Faction faction, string signal, IEnumerable<LairDefender> defenders)
    {
        lairFaction = faction;
        alarmSignal = signal;
        foreach (LairDefender defender in defenders)
        {
            entries.Add(new Entry { pawn = defender.Pawn, tier = defender.Tier, dormant = defender.Role != LairPawnRole.Guard });
        }
        lossesToRaise = LairDefenseRules.LossesToRaiseAlarm(entries.Count);
        configuredTick = Find.TickManager.TicksGame;
    }

    public override void TickRare()
    {
        base.TickRare();
        int now = Find.TickManager.TicksGame;
        bool masterHurt = false;
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            Entry entry = entries[i];
            if (IsLost(entry))
            {
                masterHurt |= entry.tier == LairPawnTier.Master;
                entries.RemoveAt(i);
                losses++;
                continue;
            }
            if (entry.tier == LairPawnTier.Master && entry.pawn.mindState.lastHarmTick >= configuredTick)
            {
                Raise();
                return;
            }
            if (entry.dormant && !IsDormant(entry.pawn))
            {
                entry.dormant = false;
                if (entry.tier == LairPawnTier.Master)
                {
                    Raise();
                    return;
                }
                if (entry.tier == LairPawnTier.Sanguophage && raiseAtTick < 0)
                {
                    raiseAtTick = now + LairDefenseRules.AlarmDelayTicks;
                }
            }
        }
        if (masterHurt || losses >= lossesToRaise || (raiseAtTick >= 0 && now >= raiseAtTick))
        {
            Raise();
        }
    }

    public override void Notify_SignalReceived(Signal signal)
    {
        base.Notify_SignalReceived(signal);
        if (signal.tag == alarmSignal && !Destroyed)
        {
            Destroy();
        }
    }

    private void Raise()
    {
        if (Destroyed)
        {
            return;
        }
        if (entries.Exists(e => e.dormant))
        {
            Messages.Message("MessageSleepingPawnsWokenUp".Translate(lairFaction.def.pawnsPlural).CapitalizeFirst(), MessageTypeDefOf.ThreatBig);
        }
        Find.SignalManager.SendSignal(new Signal(alarmSignal));
        if (!Destroyed)
        {
            Destroy();
        }
    }

    private static bool IsDormant(Pawn pawn)
    {
        return pawn.Deathresting || pawn.GetLord()?.CurLordToil is LordToil_LairSleep;
    }

    private bool IsLost(Entry entry)
    {
        Pawn pawn = entry.pawn;
        if (pawn == null || pawn.Destroyed || pawn.Dead || pawn.MapHeld != Map)
        {
            return true;
        }
        if (pawn.Faction != lairFaction || pawn.IsPrisoner)
        {
            return true;
        }
        if (pawn.Deathresting)
        {
            return !entry.dormant || SanguophageUtility.ShouldBeDeathrestingOrInComaInsteadOfDead(pawn);
        }
        return pawn.Downed;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            losses += entries.RemoveAll(e => e.pawn.DestroyedOrNull());
        }
        Scribe_References.Look(ref lairFaction, "lairFaction");
        Scribe_Values.Look(ref alarmSignal, "alarmSignal");
        Scribe_Values.Look(ref lossesToRaise, "lossesToRaise", 0);
        Scribe_Values.Look(ref losses, "losses", 0);
        Scribe_Values.Look(ref raiseAtTick, "raiseAtTick", -1);
        Scribe_Values.Look(ref configuredTick, "configuredTick", 0);
        Scribe_Collections.Look(ref entries, "entries", LookMode.Deep);
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            entries ??= new List<Entry>();
            losses += entries.RemoveAll(e => e.pawn == null);
        }
    }
}
