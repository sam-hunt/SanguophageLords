using Xunit;

namespace SanguophageLords.Tests;

/// <summary>
/// The pure numbers behind the lair's inhabitants: the pawn budget (LairPawnBudget) and the
/// defence rules (LairDefenseRules). Costs mirror the kinds' combatPower in
/// 1.6/Defs/PawnKindDefs/LairPawnKinds.xml.
/// </summary>
public class LairPawnBudgetTests
{
    private const float Thrall = 100f;
    private const float Sanguophage = 250f;
    private const float Master = 400f;

    private static LairPawnBudget Budget(float points)
    {
        return new LairPawnBudget(points, Thrall, Sanguophage, Master);
    }

    [Fact]
    public void DefaultPointsBuyTheDocumentedLair()
    {
        LairPawnBudget budget = Budget(LairPawnBudget.DefaultPoints);
        Assert.Equal(1, budget.Cap(LairPawnTier.Master));
        Assert.Equal(3, budget.Cap(LairPawnTier.Sanguophage));
        Assert.Equal(8, budget.Cap(LairPawnTier.Thrall));
    }

    [Fact]
    public void NoMasterWhenThePointsCannotPayForOne()
    {
        LairPawnBudget budget = Budget(Master - 1f);
        Assert.Equal(0, budget.Cap(LairPawnTier.Master));
        Assert.False(budget.TryTake(LairPawnTier.Master, awake: false));
        Assert.True(budget.Cap(LairPawnTier.Thrall) > 0);
    }

    [Fact]
    public void ZeroAndNegativePointsBuyNothing()
    {
        foreach (float points in new[] { 0f, -500f })
        {
            LairPawnBudget budget = Budget(points);
            Assert.Equal(0, budget.Cap(LairPawnTier.Master));
            Assert.Equal(0, budget.Cap(LairPawnTier.Sanguophage));
            Assert.Equal(0, budget.Cap(LairPawnTier.Thrall));
        }
    }

    [Theory]
    [InlineData(1000f)]
    [InlineData(2000f)]
    [InlineData(3500f)]
    [InlineData(8000f)]
    public void SpendingNeverExceedsThePoints(float points)
    {
        LairPawnBudget budget = Budget(points);
        float spent = (budget.Cap(LairPawnTier.Master) * Master) + (budget.Cap(LairPawnTier.Sanguophage) * Sanguophage) + (budget.Cap(LairPawnTier.Thrall) * Thrall);
        Assert.True(spent <= points, $"spent {spent} of {points}");
    }

    /// <summary>
    /// From the point the master is affordable on. Below it the points all go to the other
    /// tiers, so crossing the master's price trades a thrall or two for the master.
    /// </summary>
    [Fact]
    public void MorePointsNeverBuyFewerPawns()
    {
        LairPawnBudget previous = Budget(Master);
        for (float points = Master + 50f; points <= 6000f; points += 50f)
        {
            LairPawnBudget budget = Budget(points);
            foreach (LairPawnTier tier in new[] { LairPawnTier.Thrall, LairPawnTier.Sanguophage, LairPawnTier.Master })
            {
                Assert.True(budget.Cap(tier) >= previous.Cap(tier), $"{tier} fell at {points} points");
            }
            previous = budget;
        }
    }

    [Fact]
    public void RequestsAreHonouredUpToEachTiersCap()
    {
        LairPawnBudget budget = Budget(LairPawnBudget.DefaultPoints);
        int thralls = 0;
        while (budget.TryTake(LairPawnTier.Thrall, awake: false))
        {
            thralls++;
        }
        Assert.Equal(budget.Cap(LairPawnTier.Thrall), thralls);
        Assert.Equal(thralls, budget.Taken(LairPawnTier.Thrall));
        // Thralls spent first must not starve the sanguophages: caps are per tier.
        Assert.True(budget.TryTake(LairPawnTier.Sanguophage, awake: false));
        Assert.True(budget.TryTake(LairPawnTier.Master, awake: true));
        Assert.False(budget.TryTake(LairPawnTier.Master, awake: false));
    }

    [Fact]
    public void OnlyOneSanguophageStandsGuardAwake()
    {
        LairPawnBudget budget = Budget(LairPawnBudget.DefaultPoints);
        Assert.True(budget.TryTake(LairPawnTier.Sanguophage, awake: true));
        Assert.False(budget.TryTake(LairPawnTier.Sanguophage, awake: true));
        // A refused awake request spends nothing; the remaining sanguophages may still deathrest.
        Assert.Equal(1, budget.Taken(LairPawnTier.Sanguophage));
        Assert.True(budget.TryTake(LairPawnTier.Sanguophage, awake: false));
        Assert.True(budget.TryTake(LairPawnTier.Sanguophage, awake: false));
        Assert.False(budget.TryTake(LairPawnTier.Sanguophage, awake: false));
    }

    [Fact]
    public void AwakeLimitDoesNotApplyToThrallsOrTheMaster()
    {
        LairPawnBudget budget = Budget(LairPawnBudget.DefaultPoints);
        Assert.True(budget.TryTake(LairPawnTier.Thrall, awake: true));
        Assert.True(budget.TryTake(LairPawnTier.Thrall, awake: true));
        Assert.True(budget.TryTake(LairPawnTier.Sanguophage, awake: true));
        Assert.True(budget.TryTake(LairPawnTier.Master, awake: true));
    }

    [Fact]
    public void PrisonersAreCappedByCountAlone()
    {
        LairPawnBudget budget = Budget(0f);
        for (int i = 0; i < LairPawnBudget.MaxPrisoners; i++)
        {
            Assert.True(budget.TryTakePrisoner());
        }
        Assert.False(budget.TryTakePrisoner());
        Assert.Equal(LairPawnBudget.MaxPrisoners, budget.Prisoners);
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 2)]
    [InlineData(5, 2)]
    [InlineData(6, 3)]
    [InlineData(12, 5)]
    [InlineData(30, 11)]
    public void AlarmNeedsAThirdOfTheDefendersLostAndNeverFewerThanTwo(int defenders, int expected)
    {
        Assert.Equal(expected, LairDefenseRules.LossesToRaiseAlarm(defenders));
    }

    [Fact]
    public void OnlySanguophageTiersDeathrest()
    {
        Assert.False(LairDefenseRules.CanDeathrest(LairPawnTier.Thrall));
        Assert.True(LairDefenseRules.CanDeathrest(LairPawnTier.Sanguophage));
        Assert.True(LairDefenseRules.CanDeathrest(LairPawnTier.Master));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 6)]
    [InlineData(3, 3)]
    [InlineData(6, 9)]
    [InlineData(12, 12)]
    public void GuardsHoldTheirRoomAndJustBeyondItsDoors(int halfAcross, int halfDeep)
    {
        float defend = LairDefenseRules.GuardDefendRadius(halfAcross, halfDeep);
        float wander = LairDefenseRules.GuardWanderRadius(halfAcross, halfDeep);
        Assert.True(defend > halfAcross && defend > halfDeep);
        Assert.InRange(wander, 1f, LairDefenseRules.MaxGuardWanderRadius);
        Assert.True(wander < defend);
    }
}
