using System;

namespace SanguophageLords;

// How many inhabitants one lair may have, decided from a threat points budget. Room workers
// ask for pawns as their layout suggests; LairPawns asks this budget before honouring each
// request, so the lair's total strength is set here and not by whichever rooms the planner
// happened to pick. Pure arithmetic with no game types, so it is unit-tested
// (Tests/1.6/LairPawnBudgetTests.cs).
//
// The split. The owner (Master) is paid for first: a lair always has one when the points
// cover it. The rest is shared between sanguophages and thralls by SanguophageShare, each
// side buying whole pawns at its tier's cost. Costs are the pawn kinds' combatPower
// (1.6/Defs/PawnKindDefs/LairPawnKinds.xml), the same unit vanilla prices raiders in, so a
// quest's threat points can later be passed straight in as LairSite.DefenderPoints.
//
// Per-tier caps rather than one shared pool, because rooms are furnished in plan order: with
// a single pool, a barracks filled early would spend the points the deathrest chambers
// further in need. Two further limits:
//  - At most MaxAwakeSanguophages of the sanguophages may stand guard awake. The rest wait in
//    deathrest, which is what makes the lair wake in stages (LordJob_LairDefend).
//  - Prisoners cost nothing and are capped by count, since each one is a pawn the player can
//    free into the colony (LairPawns.SpawnPrisoner).
//
// DefaultPoints is balanced for a mid-game colony: with the kinds' costs it buys the master,
// three sanguophages and eight thralls.
public sealed class LairPawnBudget
{
    public const float DefaultPoints = 2000f;

    public const float SanguophageShare = 0.5f;

    public const int MaxAwakeSanguophages = 1;

    public const int MaxPrisoners = 3;

    private readonly int[] caps = new int[3];
    private readonly int[] taken = new int[3];
    private int awakeSanguophages;
    private int prisoners;

    public LairPawnBudget(float points, float thrallCost, float sanguophageCost, float masterCost)
    {
        if (thrallCost <= 0f || sanguophageCost <= 0f || masterCost <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(thrallCost), "Pawn costs must be positive.");
        }
        points = Math.Max(0f, points);
        int masters = points >= masterCost ? 1 : 0;
        float rest = points - (masters * masterCost);
        caps[(int)LairPawnTier.Master] = masters;
        caps[(int)LairPawnTier.Sanguophage] = (int)(rest * SanguophageShare / sanguophageCost);
        caps[(int)LairPawnTier.Thrall] = (int)(rest * (1f - SanguophageShare) / thrallCost);
    }

    public int Cap(LairPawnTier tier)
    {
        return caps[(int)tier];
    }

    public int Taken(LairPawnTier tier)
    {
        return taken[(int)tier];
    }

    public int Prisoners => prisoners;

    // Claims one pawn of the tier if the budget allows it. awake: the pawn will stand guard
    // rather than sleep or deathrest.
    public bool TryTake(LairPawnTier tier, bool awake)
    {
        int i = (int)tier;
        if (taken[i] >= caps[i])
        {
            return false;
        }
        if (awake && tier == LairPawnTier.Sanguophage)
        {
            if (awakeSanguophages >= MaxAwakeSanguophages)
            {
                return false;
            }
            awakeSanguophages++;
        }
        taken[i]++;
        return true;
    }

    public bool TryTakePrisoner()
    {
        if (prisoners >= MaxPrisoners)
        {
            return false;
        }
        prisoners++;
        return true;
    }
}
