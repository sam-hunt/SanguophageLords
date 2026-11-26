using RimWorld;
using Verse;
using Verse.AI;

namespace SanguophageLords;

// The sleeping lair pawn's job: lie in its own bed, asleep until something wakes it. Given by
// the SL_LairSleep duty (1.6/Defs/DutyDefs/SL_LairSleep.xml) that LordToil_LairSleep assigns,
// and used by LairPawns.SpawnSleeper for the pawn's first job.
//
// Why not vanilla's SleepForever duty: its JobGiver_ForceSleepNow lays the pawn down on its
// current cell, never in a bed (RimWorld/JobGiver_ForceSleepNow.cs). A pawn tucked into bed
// would, at the lay-down toil's next job check (every 211 ticks, Toils_LayDown), swap to that
// job and lie on top of the bed instead of in it. This gives the same forced sleep in the bed.
//
// Engine facts relied on, identical in 1.6 and 1.7 (RimWorld/Toils_LayDown.cs,
// JobDriver_LayDown.cs, Verse.AI/Pawn_JobTracker.cs):
//  - Job.forceSleep keeps the pawn asleep however rested it is; only damage, clamor or
//    RestUtility.WakeUp end the job.
//  - The periodic job check re-runs this giver; the result targets the same bed, so
//    JobDriver_LayDown.IsContinuation holds and the running job is kept.
// Without a usable bed (destroyed, or claimed away) the pawn sleeps where it stands.
public class JobGiver_LairSleep : ThinkNode_JobGiver
{
    protected override Job TryGiveJob(Pawn pawn)
    {
        return SleepJob(pawn, pawn.ownership?.OwnedBed);
    }

    public static Job SleepJob(Pawn pawn, Building_Bed bed)
    {
        Job job = bed != null && RestUtility.CanUseBedNow(bed, pawn, checkSocialProperness: false)
            ? JobMaker.MakeJob(JobDefOf.LayDown, bed)
            : JobMaker.MakeJob(JobDefOf.LayDown, pawn.Position);
        job.forceSleep = true;
        return job;
    }
}
