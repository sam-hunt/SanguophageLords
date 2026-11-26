using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Verse;
using Xunit;

namespace SanguophageLords.Tests;

/// <summary>
/// Structural invariants of the pure lair planner over many seeds. Set SL_PLAN_DUMP to a
/// folder to also write each seed's plan there as ASCII for eyeballing.
/// </summary>
public class LairPlannerTests
{
    private const int Seeds = 40;

    public static LairPlanParams SampleParams()
    {
        RoomRequest Request(string key, PlanRole role, PlanWalls walls, params ShapeOption[] shapes)
        {
            return new RoomRequest { Key = key, Role = role, Walls = walls, Shapes = shapes.ToList() };
        }

        ShapeOption Shape(RoomShapeKind kind, int acrossMin, int acrossMax, int deepMin, int deepMax)
        {
            return new ShapeOption { kind = kind, across = new IntRange(acrossMin, acrossMax), deep = new IntRange(deepMin, deepMax) };
        }

        var parms = new LairPlanParams
        {
            EntranceHall = Request("hall", PlanRole.EntranceHall, PlanWalls.Built, Shape(RoomShapeKind.Octagon, 15, 17, 15, 17)),
            Forecourt = Request("forecourt", PlanRole.Forecourt, PlanWalls.Open),
            Vestibule = Request("vestibule", PlanRole.Vestibule, PlanWalls.Built, Shape(RoomShapeKind.Rect, 7, 7, 4, 5)),
            Processional = Request("processional", PlanRole.Processional, PlanWalls.Built, Shape(RoomShapeKind.Rect, 7, 7, 15, 21)),
            Sanctum = Request("sanctum", PlanRole.Sanctum, PlanWalls.Built, Shape(RoomShapeKind.Octagon, 21, 25, 21, 25)),
            Corridor = Request("corridor", PlanRole.Corridor, PlanWalls.Built),
        };
        parms.Processional.MaxChildren = 4;
        parms.Sanctum.MaxChildren = 3;

        RoomRequest Room(string key, PlanAnchor anchor, bool paired, PlanWalls walls, params ShapeOption[] shapes)
        {
            RoomRequest request = Request(key, PlanRole.Room, walls, shapes);
            request.Anchor = anchor;
            request.Paired = paired;
            request.CorridorChance = 0.4f;
            return request;
        }

        parms.Program.Add(Room("mausoleum", PlanAnchor.AxisNorth, false, PlanWalls.Built, Shape(RoomShapeKind.Round, 11, 13, 11, 13)));
        parms.Program.Add(Room("deathrest", PlanAnchor.SanctumSide, true, PlanWalls.Built, Shape(RoomShapeKind.Round, 9, 11, 9, 11)));
        parms.Program.Add(Room("deathrest", PlanAnchor.SanctumSide, true, PlanWalls.Built, Shape(RoomShapeKind.Round, 9, 11, 9, 11)));
        parms.Program.Add(Room("tomb", PlanAnchor.ProcessionalSide, true, PlanWalls.Built, Shape(RoomShapeKind.Rect, 7, 9, 7, 11)));
        parms.Program.Add(Room("tomb", PlanAnchor.ProcessionalSide, true, PlanWalls.Built, Shape(RoomShapeKind.Rect, 7, 9, 7, 11)));
        parms.Program.Add(Room("library", PlanAnchor.Any, false, PlanWalls.Built, Shape(RoomShapeKind.Octagon, 11, 13, 11, 13)));
        parms.Program.Add(Room("prison", PlanAnchor.Any, false, PlanWalls.Hewn, Shape(RoomShapeKind.Rect, 9, 13, 9, 11)));
        parms.Program.Add(Room("barracks", PlanAnchor.Any, false, PlanWalls.Hewn, Shape(RoomShapeKind.Rect, 9, 11, 7, 11)));
        parms.Program.Add(Room("larder", PlanAnchor.Any, false, PlanWalls.Hewn, Shape(RoomShapeKind.Rect, 5, 7, 5, 7)));
        parms.Program.Add(Room("dining", PlanAnchor.Any, false, PlanWalls.Built, Shape(RoomShapeKind.Rect, 9, 11, 11, 13)));
        parms.Program.Add(Room("vault", PlanAnchor.Any, false, PlanWalls.Built, Shape(RoomShapeKind.Rect, 5, 7, 5, 7)));
        parms.Program[parms.Program.Count - 1].MaxChildren = 0;
        parms.Program[parms.Program.Count - 1].AllowLoops = false;
        parms.Program[parms.Program.Count - 1].DepthBias = 2f;
        parms.Program[parms.Program.Count - 1].MinDepth = 4;
        for (int i = 0; i < 4; i++)
        {
            parms.Program.Add(Room("crypt", PlanAnchor.Any, false, i % 2 == 0 ? PlanWalls.Built : PlanWalls.Hewn,
                Shape(RoomShapeKind.Rect, 5, 9, 5, 9), Shape(RoomShapeKind.Round, 7, 9, 7, 9)));
        }
        return parms;
    }

    private static LairPlan PlanFor(int seed, LairPlanParams parms = null)
    {
        Rand.PushState(seed);
        try
        {
            return LairPlanner.Generate(parms ?? SampleParams());
        }
        finally
        {
            Rand.PopState();
        }
    }

    public static IEnumerable<object[]> AllSeeds => Enumerable.Range(1, Seeds).Select(s => new object[] { s });

    [Theory]
    [MemberData(nameof(AllSeeds))]
    public void Plan_HoldsItsInvariants(int seed)
    {
        LairPlanParams parms = SampleParams();
        LairPlan plan = PlanFor(seed, parms);
        Assert.NotNull(plan);
        string dump = Environment.GetEnvironmentVariable("SL_PLAN_DUMP");
        if (!string.IsNullOrEmpty(dump))
        {
            Directory.CreateDirectory(dump);
            File.WriteAllText(Path.Combine(dump, $"plan-{seed:D3}.txt"), Describe(plan) + plan.ToAscii());
        }

        // The spine is always there.
        foreach (PlanRole role in new[] { PlanRole.EntranceHall, PlanRole.Forecourt, PlanRole.Vestibule, PlanRole.Processional, PlanRole.Sanctum })
        {
            Assert.Single(plan.Rooms, r => r.Role == role);
        }
        Assert.True(plan.StairwellCell.IsValid);

        // Most of the program fits at this size.
        int programRooms = plan.Rooms.Count(r => r.Role == PlanRole.Room);
        Assert.True(programRooms >= parms.Program.Count - 2, $"only {programRooms} of {parms.Program.Count} rooms placed");

        // Room interiors never touch, even diagonally, and each cell belongs to its room.
        foreach (PlanRoom room in plan.Rooms)
        {
            foreach (IntVec3 c in room.Cells)
            {
                Assert.Equal(PlanCell.Floor, plan[c]);
                Assert.Equal(room.Id, plan.RoomIdAt(c));
                foreach (IntVec3 offset in GenAdj.AdjacentCells)
                {
                    int other = plan.RoomIdAt(c + offset);
                    Assert.True(other < 0 || other == room.Id, $"rooms {room.Id} and {other} touch at {c}");
                }
            }
        }

        // Every door and arch is framed: wall on both sides along the wall line, open cells
        // ahead and behind.
        foreach (PlanConnection connection in plan.Connections)
        {
            IntVec3 ahead = connection.Dir.FacingCell;
            IntVec3 side = connection.Dir.RighthandCell;
            foreach (IntVec3 c in connection.Cells)
            {
                Assert.True(plan[c] == PlanCell.Door || plan[c] == PlanCell.Arch);
                Assert.True(LairPlan.IsPassable(plan[c + ahead]), $"blocked ahead of {c}");
                Assert.True(LairPlan.IsPassable(plan[c - ahead]), $"blocked behind {c}");
                foreach (IntVec3 flank in new[] { c + side, c - side })
                {
                    Assert.True(LairPlan.IsWall(plan[flank]) || connection.Cells.Contains(flank), $"opening at {c} is not framed");
                }
            }
        }

        // Every room can be walked to from the stairwell.
        var reached = new HashSet<IntVec3> { plan.StairwellCell };
        var frontier = new Queue<IntVec3>();
        frontier.Enqueue(plan.StairwellCell);
        while (frontier.Count > 0)
        {
            IntVec3 c = frontier.Dequeue();
            foreach (IntVec3 offset in GenAdj.CardinalDirections)
            {
                IntVec3 n = c + offset;
                if (plan.InBounds(n) && LairPlan.IsPassable(plan[n]) && reached.Add(n))
                {
                    frontier.Enqueue(n);
                }
            }
        }
        foreach (PlanRoom room in plan.Rooms)
        {
            Assert.True(reached.Contains(room.Cells[0]), $"room {room.Id} ({room.Request.Key}) is cut off");
        }

        // Nothing walkable leaks out of the plan except the river, which runs edge to edge.
        for (int x = 0; x < plan.Width; x++)
        {
            for (int z = 0; z < plan.Height; z++)
            {
                bool edge = x == 0 || z == 0 || x == plan.Width - 1 || z == plan.Height - 1;
                PlanCell cell = plan[x, z];
                Assert.True(!edge || cell == PlanCell.None || cell == PlanCell.HewnWall || cell == PlanCell.ShallowWater || cell == PlanCell.DeepWater || cell == PlanCell.Cave, $"{cell} on the plan edge at ({x}, {z})");
            }
        }

        // The vault asked to be deep inside, and is.
        Assert.All(plan.Rooms.Where(r => r.Request.Key == "vault"), r => Assert.True(r.Depth >= 3, $"vault at depth {r.Depth}"));

        // No open cell of the approach is further from rock or wall than thick roof spans.
        PlanRoom hall = plan.Rooms.Single(r => r.Role == PlanRole.EntranceHall);
        PlanRoom vestibule = plan.Rooms.Single(r => r.Role == PlanRole.Vestibule);
        for (int z = hall.Bounds.maxZ + 2; z < vestibule.Bounds.minZ - 1; z++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                if (!LairPlan.IsPassable(plan[x, z]))
                {
                    continue;
                }
                bool held = false;
                for (int dz = -6; dz <= 6 && !held; dz++)
                {
                    for (int dx = -6; dx <= 6 && !held; dx++)
                    {
                        held = (dx * dx) + (dz * dz) <= 47 && LairPlan.IsWall(plan.CellOrNone(x + dx, z + dz));
                    }
                }
                Assert.True(held, $"cavern roof unsupported at ({x}, {z})");
            }
        }

        // Mirrored partners really are mirror images.
        int axis = plan.Width / 2;
        foreach (PlanRoom room in plan.Rooms.Where(r => r.MirrorOf >= 0))
        {
            PlanRoom twin = plan.Rooms[room.MirrorOf];
            Assert.Equal(twin.Bounds.minZ, room.Bounds.minZ);
            Assert.Equal(twin.Bounds.Height, room.Bounds.Height);
            Assert.Equal(axis - twin.Bounds.minX, room.Bounds.maxX - axis);
        }
    }

    [Fact]
    public void SameSeed_GivesSamePlan()
    {
        Assert.Equal(PlanFor(7).ToAscii(), PlanFor(7).ToAscii());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Rotation_KeepsRoomsAndCellsTogether(int quarterTurns)
    {
        LairPlan plan = PlanFor(11);
        LairPlan turned = plan.Rotated(new Rot4(quarterTurns));
        Assert.Equal(plan.Rooms.Count, turned.Rooms.Count);
        Assert.Equal(new Rot4(quarterTurns), turned.Facing);
        foreach (PlanRoom room in turned.Rooms)
        {
            Assert.All(room.Cells, c => Assert.Equal(room.Id, turned.RoomIdAt(c)));
            Assert.All(room.Cells, c => Assert.True(room.Role == PlanRole.Room && room.Walls == PlanWalls.Hewn || room.Bounds.Contains(c)));
        }
        foreach (PlanConnection connection in turned.Connections)
        {
            Assert.All(connection.Cells, c => Assert.True(turned[c] == PlanCell.Door || turned[c] == PlanCell.Arch));
        }
        Assert.Equal(turned.Rooms.Single(r => r.Role == PlanRole.EntranceHall).Id, turned.RoomIdAt(turned.StairwellCell));
    }

    private static string Describe(LairPlan plan)
    {
        var lines = plan.Rooms.Select(r =>
            $"{(char)('a' + (r.Id % 26))} {r.Id,2} {r.Request.Key,-12} {r.Shape,-7} {r.Bounds.Width}x{r.Bounds.Height} depth {r.Depth} parent {r.Parent}{(r.MirrorOf >= 0 ? " mirror of " + r.MirrorOf : "")}{(r.OnAxis ? " on-axis" : "")}");
        return string.Join("\n", lines) + "\n\n";
    }
}
