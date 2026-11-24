using System.Collections.Generic;
using System.Linq;
using Verse;
using Xunit;

namespace SanguophageLair.Tests;

/// <summary>
/// The pure utility-line router: hand-made grids for its rules, generated lair plans for its
/// behaviour at scale. Grids are drawn as text with z increasing upward (the last line is
/// z = 0): '.' open at cost 1, '#' closed, 'S' a source cell, digits and letters name target
/// cells (open, cost 1).
/// </summary>
public class GridRouterTests
{
    private sealed class Grid
    {
        public int Width;
        public int Height;
        public int[] Cost;
        public List<IntVec3> Sources = new List<IntVec3>();
        public Dictionary<char, List<IntVec3>> Marks = new Dictionary<char, List<IntVec3>>();
    }

    private static Grid Parse(params string[] rows)
    {
        var grid = new Grid { Width = rows[0].Length, Height = rows.Length };
        grid.Cost = new int[grid.Width * grid.Height];
        for (int r = 0; r < rows.Length; r++)
        {
            int z = rows.Length - 1 - r;
            for (int x = 0; x < grid.Width; x++)
            {
                char ch = rows[r][x];
                var c = new IntVec3(x, 0, z);
                grid.Cost[(z * grid.Width) + x] = ch == '#' ? 0 : 1;
                if (ch == 'S')
                {
                    grid.Sources.Add(c);
                }
                else if (ch != '.' && ch != '#')
                {
                    if (!grid.Marks.TryGetValue(ch, out List<IntVec3> list))
                    {
                        list = new List<IntVec3>();
                        grid.Marks[ch] = list;
                    }
                    list.Add(c);
                }
            }
        }
        return grid;
    }

    private static RouteTarget Target(IEnumerable<IntVec3> reach, IEnumerable<IntVec3> joins = null)
    {
        var target = new RouteTarget();
        target.Reach.AddRange(reach);
        if (joins != null)
        {
            target.Joins.AddRange(joins);
        }
        return target;
    }

    private static bool FourConnected(IEnumerable<IntVec3> cells)
    {
        var set = new HashSet<IntVec3>(cells);
        if (set.Count == 0)
        {
            return true;
        }
        var seen = new HashSet<IntVec3> { set.First() };
        var frontier = new Queue<IntVec3>(seen);
        while (frontier.Count > 0)
        {
            IntVec3 c = frontier.Dequeue();
            foreach (IntVec3 d in GenAdj.CardinalDirections)
            {
                if (set.Contains(c + d) && seen.Add(c + d))
                {
                    frontier.Enqueue(c + d);
                }
            }
        }
        return seen.Count == set.Count;
    }

    private static int Bends(List<IntVec3> run)
    {
        int bends = 0;
        for (int i = 2; i < run.Count; i++)
        {
            if (run[i] - run[i - 1] != run[i - 1] - run[i - 2])
            {
                bends++;
            }
        }
        return bends;
    }

    [Fact]
    public void Route_FollowsTheOnlyCorridor()
    {
        Grid g = Parse(
            "#######",
            "#S...1#",
            "#######");
        RouteResult result = new GridRouter(g.Width, g.Height, g.Cost, 2).Route(g.Sources, new[] { Target(g.Marks['1']) });
        Assert.True(result.Reached[0]);
        Assert.Equal(new[] { new IntVec3(2, 0, 1), new IntVec3(3, 0, 1), new IntVec3(4, 0, 1), new IntVec3(5, 0, 1) }, result.Laid);
    }

    [Fact]
    public void Route_SharesTheTrunkBetweenTargets()
    {
        Grid g = Parse(
            "#####1#",
            "#####.#",
            "#S.....",
            "#####.#",
            "#####2#");
        RouteResult result = new GridRouter(g.Width, g.Height, g.Cost, 2).Route(g.Sources, new[] { Target(g.Marks['1']), Target(g.Marks['2']) });
        Assert.All(result.Reached, Assert.True);
        // Trunk of four to the junction, then two up and two down: nothing laid twice.
        Assert.Equal(8, result.Laid.Count);
        Assert.Equal(result.Laid.Count, result.Laid.Distinct().Count());
        Assert.True(FourConnected(result.Laid.Concat(g.Sources)));
    }

    [Fact]
    public void Route_ReportsTargetsItCannotReach()
    {
        Grid g = Parse(
            "#########",
            "#S..1#.2#",
            "#########");
        RouteResult result = new GridRouter(g.Width, g.Height, g.Cost, 2).Route(g.Sources, new[] { Target(g.Marks['1']), Target(g.Marks['2']) });
        Assert.True(result.Reached[0]);
        Assert.False(result.Reached[1]);
        Assert.DoesNotContain(result.Laid, c => c.x > 4);
    }

    [Fact]
    public void Route_NeverCrossesClosedCells()
    {
        Grid g = Parse(
            "1....",
            "####.",
            "S....",
            ".###.",
            "....2");
        RouteResult result = new GridRouter(g.Width, g.Height, g.Cost, 2).Route(g.Sources, new[] { Target(g.Marks['1']), Target(g.Marks['2']) });
        Assert.All(result.Reached, Assert.True);
        Assert.All(result.Laid, c => Assert.True(g.Cost[(c.z * g.Width) + c.x] > 0));
    }

    [Fact]
    public void Route_PrefersStraightRunsOverStaircases()
    {
        Grid g = Parse(
            "......1",
            ".......",
            ".......",
            ".......",
            "S......");
        RouteResult result = new GridRouter(g.Width, g.Height, g.Cost, 2).Route(g.Sources, new[] { Target(g.Marks['1']) });
        Assert.True(result.Reached[0]);
        Assert.Equal(10, result.Laid.Count);
        var run = new List<IntVec3>(g.Sources);
        run.AddRange(result.Laid);
        Assert.Equal(1, Bends(run));
    }

    [Fact]
    public void Route_FollowsCheapCellsRoundAnExpensiveMiddle()
    {
        // The middle row costs 8 a cell, the rows beside it 3: the line keeps to the edge.
        Grid g = Parse(
            ".......",
            "S.....1",
            ".......");
        for (int x = 1; x < 6; x++)
        {
            g.Cost[(1 * g.Width) + x] = 8;
            g.Cost[(0 * g.Width) + x] = 3;
            g.Cost[(2 * g.Width) + x] = 3;
        }
        RouteResult result = new GridRouter(g.Width, g.Height, g.Cost, 2).Route(g.Sources, new[] { Target(g.Marks['1']) });
        Assert.True(result.Reached[0]);
        Assert.DoesNotContain(result.Laid, c => c.z == 1 && c.x > 0 && c.x < 6);
    }

    [Fact]
    public void Route_ServesThroughAJoinedFootprint()
    {
        // Target 1 is a building on the 'a' cells that carries the network, reached from the
        // cell before it; target 2 is a building on the 'b' cell touching it edge to edge, so
        // serving 1 serves 2 with nothing more laid.
        Grid g = Parse(
            "#########",
            "#S...1aa#",
            "#######b#");
        foreach (IntVec3 c in g.Marks['a'].Concat(g.Marks['b']))
        {
            g.Cost[(c.z * g.Width) + c.x] = 0;
        }
        var joined = Target(g.Marks['1'], g.Marks['a']);
        var beyond = Target(new[] { new IntVec3(7, 0, 1) }, g.Marks['b']);
        RouteResult result = new GridRouter(g.Width, g.Height, g.Cost, 2).Route(g.Sources, new[] { joined, beyond });
        Assert.True(result.Reached[0]);
        Assert.True(result.Reached[1]);
        Assert.Equal(4, result.Laid.Count);
        Assert.DoesNotContain(new IntVec3(7, 0, 0), result.Laid);
    }

    public static IEnumerable<object[]> PlanSeeds => Enumerable.Range(1, 12).Select(s => new object[] { s });

    // Routing over a generated lair from one room to the middle of every other room: the
    // network reaches all of them, stays on the plan's circulation, and shares enough trunk
    // to come out far shorter than the sum of the separate shortest runs.
    [Theory]
    [MemberData(nameof(PlanSeeds))]
    public void Route_ConnectsAGeneratedLair(int seed)
    {
        LairPlan plan;
        Rand.PushState(seed);
        try
        {
            plan = LairPlanner.Generate(LairPlannerTests.SampleParams());
        }
        finally
        {
            Rand.PopState();
        }
        Assert.NotNull(plan);
        int[] cost = LairCirculation.Costs(plan);

        List<PlanRoom> rooms = plan.Rooms.Where(r => r.Role == PlanRole.Room).ToList();
        PlanRoom home = rooms[0];
        var sources = new List<IntVec3> { home.Center };
        var targets = new List<RouteTarget>();
        foreach (PlanRoom room in plan.Rooms)
        {
            // The entrance hall and forecourt lie across the cavern, which is closed to lines;
            // corridors may bend, so their bounding box's centre need not be floor.
            if (room != home && room.Role != PlanRole.Forecourt && room.Role != PlanRole.EntranceHall && room.Role != PlanRole.Corridor)
            {
                targets.Add(Target(new[] { room.Center }));
            }
        }
        RouteResult result = new GridRouter(plan.Width, plan.Height, cost, 2).Route(sources, targets);

        Assert.All(result.Reached, Assert.True);
        foreach (IntVec3 c in result.Laid)
        {
            Assert.True(cost[(c.z * plan.Width) + c.x] > 0, $"laid on closed cell {c}");
            Assert.True(LairPlan.IsPassable(plan[c]), $"laid on {plan[c]} at {c}");
        }
        Assert.Equal(result.Laid.Count, result.Laid.Distinct().Count());
        Assert.True(FourConnected(result.Laid.Concat(sources)));

        // Shared trunks keep the tree near a third of the separate runs' total on these plans;
        // half leaves room for unlucky layouts while still catching a router that stops
        // sharing.
        int separate = targets.Sum(t => ShortestRun(plan, cost, home.Center, t.Reach[0]));
        Assert.True(result.Laid.Count * 2 <= separate, $"tree of {result.Laid.Count} cells against {separate} for separate runs");
    }

    private static int ShortestRun(LairPlan plan, int[] cost, IntVec3 from, IntVec3 to)
    {
        var dist = new Dictionary<IntVec3, int> { [from] = 0 };
        var frontier = new Queue<IntVec3>();
        frontier.Enqueue(from);
        while (frontier.Count > 0)
        {
            IntVec3 c = frontier.Dequeue();
            if (c == to)
            {
                return dist[c];
            }
            foreach (IntVec3 d in GenAdj.CardinalDirections)
            {
                IntVec3 n = c + d;
                if (plan.InBounds(n) && cost[(n.z * plan.Width) + n.x] > 0 && !dist.ContainsKey(n))
                {
                    dist[n] = dist[c] + 1;
                    frontier.Enqueue(n);
                }
            }
        }
        return int.MaxValue / 64;
    }
}
