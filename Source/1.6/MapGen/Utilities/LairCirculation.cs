namespace SanguophageLords;

// The cost grid the lair's utility lines are routed over (GridRouter), read from the plan
// alone: lines may only follow the lair's own circulation, never cut through rock or wall.
//
// What is open to a line, and what each cell costs to enter:
//   door or arch                      2   every run between rooms must pass one
//   corridor floor beside a wall      2   the trunk routes: the corridors carry the lines
//   corridor floor, middle            3
//   bridge                            3
//   room floor beside a wall          3   a line skirting a room's edge, as a builder runs it
//   room floor, middle                8   crossing the middle of a hall is the last resort
//   anything else                     0   rock, wall, cave floor, water: closed
// A middle-of-room cell costs more than two cells round by the wall, so a line takes the
// edge unless the detour is long. The map-side callers add what the plan cannot know
// (carpet, furniture, the other network's lines) on top.
public static class LairCirculation
{
    public const int Doorway = 2;
    public const int CorridorEdge = 2;
    public const int CorridorMiddle = 3;
    public const int Bridge = 3;
    public const int RoomEdge = 3;
    public const int RoomMiddle = 8;

    public static int[] Costs(LairPlan plan)
    {
        var cost = new int[plan.Width * plan.Height];
        for (int z = 0; z < plan.Height; z++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                cost[(z * plan.Width) + x] = CostAt(plan, x, z);
            }
        }
        return cost;
    }

    private static int CostAt(LairPlan plan, int x, int z)
    {
        switch (plan[x, z])
        {
            case PlanCell.Door:
            case PlanCell.Arch:
                return Doorway;
            case PlanCell.Bridge:
                return Bridge;
            case PlanCell.Floor:
                int id = plan.RoomIdAt(x, z);
                bool corridor = id >= 0 && plan.Rooms[id].Role == PlanRole.Corridor;
                bool edge = BesideWall(plan, x, z);
                if (corridor)
                {
                    return edge ? CorridorEdge : CorridorMiddle;
                }
                return edge ? RoomEdge : RoomMiddle;
            default:
                return 0;
        }
    }

    private static bool BesideWall(LairPlan plan, int x, int z)
    {
        return LairPlan.IsWall(plan.CellOrNone(x + 1, z))
            || LairPlan.IsWall(plan.CellOrNone(x - 1, z))
            || LairPlan.IsWall(plan.CellOrNone(x, z + 1))
            || LairPlan.IsWall(plan.CellOrNone(x, z - 1));
    }
}
