using Verse.AI;
using Verse.AI.Group;

namespace SanguophageLords;

// The sleeping state of a room's lord (LordJob_LairDefend): every pawn gets the SL_LairSleep
// duty, which keeps it asleep in its own bed (JobGiver_LairSleep). Vanilla's LordToil_Sleep
// with the SleepForever duty in a bed-keeping form.
//
// Deathresters in the same lord get the duty too and never act on it: they are downed, and
// their deathrest job never looks for another (JobDriver_Deathrest builds its lay-down toil
// with lookForOtherJobs false), so they stay in their caskets until TransitionAction_LairWake.
public class LordToil_LairSleep : LordToil
{
    public override void UpdateAllDuties()
    {
        for (int i = 0; i < lord.ownedPawns.Count; i++)
        {
            lord.ownedPawns[i].mindState.duty = new PawnDuty(SL_LairPawnDefOf.SL_LairSleep);
        }
    }
}
