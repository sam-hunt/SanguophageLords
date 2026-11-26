using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace SanguophageLords;

// Wakes every pawn of a lord as it leaves its sleeping state (LordJob_LairDefend): sleepers
// the vanilla way, deathresters out of deathrest. Vanilla's TransitionAction_WakeAll only does
// the first: RestUtility.WakeUp leaves downed pawns alone, and nothing in vanilla ends an NPC's
// deathrest (Pawn.HearClamor ignores deathresting pawns, and the only Gene_Deathrest.Wake
// callers are its auto-wake, sunlight and the player's gizmo).
//
// A deathrester is woken rested: deathrestTicks is raised to MinDeathrestTicks first, so
// Gene_Deathrest.Wake does not add InterruptedDeathrest (Consciousness x0.8 for days). The
// lair's sanguophages have slept for years; waking one should not weaken it. Bound buildings'
// bonuses still apply only if their presence reached 75 percent of the deathrest, which an
// unpowered set never does.
//
// A sanguophage that went into deathrest because of lethal injuries
// (SanguophageUtility.ShouldBeDeathrestingOrInComaInsteadOfDead) is left asleep: removing the
// hediff would let those injuries kill it.
//
// Decompile-verified, identical in 1.6 and 1.7: RimWorld/Gene_Deathrest.cs (Wake),
// Hediff_Deathrest.cs (removal ends the deathrest job), RimWorld/RestUtility.cs (WakeUp),
// RimWorld/SanguophageUtility.cs.
public class TransitionAction_LairWake : TransitionAction
{
    public override void DoAction(Transition trans)
    {
        var pawns = new List<Pawn>(trans.target.lord.ownedPawns);
        foreach (Pawn pawn in pawns)
        {
            if (pawn.Dead || !pawn.Spawned)
            {
                continue;
            }
            if (pawn.Deathresting)
            {
                WakeFromDeathrest(pawn);
            }
            else
            {
                RestUtility.WakeUp(pawn);
            }
        }
    }

    public static void WakeFromDeathrest(Pawn pawn)
    {
        Gene_Deathrest gene = pawn.genes?.GetFirstGeneOfType<Gene_Deathrest>();
        if (gene == null || SanguophageUtility.ShouldBeDeathrestingOrInComaInsteadOfDead(pawn))
        {
            return;
        }
        if (gene.deathrestTicks < gene.MinDeathrestTicks)
        {
            gene.deathrestTicks = gene.MinDeathrestTicks;
        }
        gene.Wake();
    }
}
