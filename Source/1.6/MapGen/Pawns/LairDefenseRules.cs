using System;

namespace SanguophageLords;

// The numbers behind how the lair defends itself, kept free of game types so they are
// unit-tested (Tests/1.6/LairPawnBudgetTests.cs). Consumers: LairAlarm (when the whole lair is
// roused), GenStep_LairDefenders (guard posts), LairPawns (which tier may do what).
//
// The general alarm. Rooms wake one by one as intruders reach them (LordJob_LairDefend). The
// alarm then rouses every room still asleep, and sends the guards from their posts after the
// intruders, when:
//  - the master wakes or is hurt: the lair's owner rouses its household at once;
//  - any other sanguophage wakes: the alarm follows AlarmDelayTicks later (an in-game hour,
//    under a minute at normal speed), the window a quick strike team has to deal with it;
//  - AlarmLossFraction of the defenders are lost (dead, downed, captured), and never fewer
//    than MinAlarmLosses, so a two-pawn lair is not roused by its first casualty.
// "Lost" counts a sanguophage in deathless deathrest, which is how a sanguophage goes down:
// lethal damage puts it into deathrest instead of killing it.
public static class LairDefenseRules
{
    public const int AlarmDelayTicks = 2500;

    public const float AlarmLossFraction = 0.34f;

    public const int MinAlarmLosses = 2;

    // Guards stand within this many cells of their post when idle.
    public const int MaxGuardWanderRadius = 4;

    // How far past their room's walls guards will go to engage, so they hold the doorways.
    public const int GuardReachBeyondRoom = 3;

    public static int LossesToRaiseAlarm(int defenders)
    {
        return Math.Max(MinAlarmLosses, (int)Math.Ceiling(defenders * AlarmLossFraction));
    }

    // Thralls are baseliners, without the deathrest gene.
    public static bool CanDeathrest(LairPawnTier tier)
    {
        return tier != LairPawnTier.Thrall;
    }

    // halfAcross, halfDeep: the room's half sizes (LairRoom.HalfAcross/HalfDeep).
    public static float GuardDefendRadius(int halfAcross, int halfDeep)
    {
        return Math.Max(halfAcross, halfDeep) + GuardReachBeyondRoom;
    }

    public static float GuardWanderRadius(int halfAcross, int halfDeep)
    {
        return Math.Max(1, Math.Min(MaxGuardWanderRadius, Math.Min(halfAcross, halfDeep) - 1));
    }
}
