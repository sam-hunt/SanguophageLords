using System.Collections.Generic;
using Verse;

namespace SanguophageLair;

// One thing a utility network must serve.
public sealed class RouteTarget
{
    // Cells a line may end on to serve the target: next to a building for a pipe, on or
    // beside it for a power consumer.
    public List<IntVec3> Reach = new List<IntVec3>();

    // Cells that become part of the network once the target is served: the footprint of a
    // building that itself carries the network on (a battery, a hemogen vat), so that later
    // lines may branch off it and anything touching it is served through it.
    public List<IntVec3> Joins = new List<IntVec3>();
}

public sealed class RouteResult
{
    // Cells to lay a line on, in the order they were added: each run starts beside the
    // network grown so far.
    public List<IntVec3> Laid = new List<IntVec3>();

    // Per target, whether the network serves it.
    public bool[] Reached;
}

// Grows one network of lines over a cost grid: the pure part of the lair's conduit and pipe
// laying, free of Map so it can be unit-tested on hand-made grids and generated plans.
//
// The grid holds an entry cost per cell, z * width + x as in LairPlan; zero or less is a cell
// no line may cross. Starting from the source cells, the network takes, again and again, the
// cheapest run from any cell it already holds to any target it does not yet serve, lays it
// and adds what it served (the shortest-path heuristic for a Steiner tree). Shared trunks
// fall out of this: a later target meets the network wherever it passes nearest, not back at
// the source. Every run is a 4-connected chain of cells, which both vanilla power conduits
// and the VE Framework's pipes need to link.
//
// A run pays the entry cost of each cell plus turnCost at each change of direction, so lines
// run straight unless a bend saves more than it costs: straight lines along walls read as
// built, a staircase across a floor does not. Ties are broken by cell index, so the result is
// deterministic and needs no random state.
public sealed class GridRouter
{
    private const int NoDir = 4;
    private const int States = 5;

    private static readonly int[] StepX = { 0, 1, 0, -1 };
    private static readonly int[] StepZ = { 1, 0, -1, 0 };

    private readonly int width;
    private readonly int height;
    private readonly int[] cost;
    private readonly int turnCost;

    public GridRouter(int width, int height, int[] cost, int turnCost)
    {
        this.width = width;
        this.height = height;
        this.cost = cost;
        this.turnCost = turnCost;
    }

    public bool Walkable(IntVec3 c)
    {
        return InBounds(c) && cost[Index(c)] > 0;
    }

    public RouteResult Route(IEnumerable<IntVec3> sources, IList<RouteTarget> targets)
    {
        var result = new RouteResult { Reached = new bool[targets.Count] };
        var inTree = new bool[width * height];
        var tree = new List<int>();

        // Which targets each cell serves.
        var servedAt = new Dictionary<int, List<int>>();
        for (int t = 0; t < targets.Count; t++)
        {
            foreach (IntVec3 c in targets[t].Reach)
            {
                if (!InBounds(c))
                {
                    continue;
                }
                int i = Index(c);
                if (!servedAt.TryGetValue(i, out List<int> list))
                {
                    list = new List<int>();
                    servedAt[i] = list;
                }
                list.Add(t);
            }
        }

        var added = new List<int>();
        foreach (IntVec3 c in sources)
        {
            if (InBounds(c) && !inTree[Index(c)])
            {
                inTree[Index(c)] = true;
                tree.Add(Index(c));
                added.Add(Index(c));
            }
        }
        int remaining = targets.Count;
        remaining -= Settle(added, targets, servedAt, inTree, tree, result.Reached);

        var dist = new int[width * height * States];
        var parent = new int[width * height * States];
        while (remaining > 0)
        {
            List<int> run = CheapestRun(tree, inTree, servedAt, result.Reached, dist, parent);
            if (run == null)
            {
                break;
            }
            foreach (int i in run)
            {
                inTree[i] = true;
                tree.Add(i);
                result.Laid.Add(Cell(i));
            }
            remaining -= Settle(run, targets, servedAt, inTree, tree, result.Reached);
        }
        return result;
    }

    // Marks every target the new cells serve as reached and joins its footprint to the
    // network, following on through footprints that serve further targets. Returns how many
    // targets were newly reached.
    private int Settle(List<int> newCells, IList<RouteTarget> targets, Dictionary<int, List<int>> servedAt, bool[] inTree, List<int> tree, bool[] reached)
    {
        int count = 0;
        var pending = new Queue<int>(newCells);
        while (pending.Count > 0)
        {
            int i = pending.Dequeue();
            if (!servedAt.TryGetValue(i, out List<int> served))
            {
                continue;
            }
            foreach (int t in served)
            {
                if (reached[t])
                {
                    continue;
                }
                reached[t] = true;
                count++;
                foreach (IntVec3 c in targets[t].Joins)
                {
                    if (InBounds(c) && !inTree[Index(c)])
                    {
                        inTree[Index(c)] = true;
                        tree.Add(Index(c));
                        pending.Enqueue(Index(c));
                    }
                }
            }
        }
        return count;
    }

    // Dijkstra from every network cell at once, over (cell, direction of arrival) states so
    // bends can be charged, stopping at the first cell that serves an unreached target.
    // Returns that run's new cells from the network outward, or null when no target is
    // reachable.
    private List<int> CheapestRun(List<int> tree, bool[] inTree, Dictionary<int, List<int>> servedAt, bool[] reached, int[] dist, int[] parent)
    {
        for (int s = 0; s < dist.Length; s++)
        {
            dist[s] = int.MaxValue;
            parent[s] = -1;
        }
        var heap = new MinHeap();
        foreach (int i in tree)
        {
            int s = (i * States) + NoDir;
            dist[s] = 0;
            heap.Push(0, s);
        }
        while (heap.Count > 0)
        {
            heap.Pop(out int d, out int state);
            if (d > dist[state])
            {
                continue;
            }
            int cell = state / States;
            int dir = state % States;
            if (!inTree[cell] && ServesUnreached(cell, servedAt, reached))
            {
                var run = new List<int>();
                for (int s = state; s >= 0 && !inTree[s / States]; s = parent[s])
                {
                    run.Add(s / States);
                }
                run.Reverse();
                return run;
            }
            int x = cell % width;
            int z = cell / width;
            for (int k = 0; k < 4; k++)
            {
                int nx = x + StepX[k];
                int nz = z + StepZ[k];
                if (nx < 0 || nz < 0 || nx >= width || nz >= height)
                {
                    continue;
                }
                int next = (nz * width) + nx;
                if (inTree[next] || cost[next] <= 0)
                {
                    continue;
                }
                int nd = d + cost[next] + (dir != NoDir && dir != k ? turnCost : 0);
                int ns = (next * States) + k;
                if (nd < dist[ns])
                {
                    dist[ns] = nd;
                    parent[ns] = state;
                    heap.Push(nd, ns);
                }
            }
        }
        return null;
    }

    private static bool ServesUnreached(int cell, Dictionary<int, List<int>> servedAt, bool[] reached)
    {
        if (!servedAt.TryGetValue(cell, out List<int> served))
        {
            return false;
        }
        foreach (int t in served)
        {
            if (!reached[t])
            {
                return true;
            }
        }
        return false;
    }

    private bool InBounds(IntVec3 c)
    {
        return c.x >= 0 && c.z >= 0 && c.x < width && c.z < height;
    }

    private int Index(IntVec3 c)
    {
        return (c.z * width) + c.x;
    }

    private IntVec3 Cell(int i)
    {
        return new IntVec3(i % width, 0, i / width);
    }

    // Binary min-heap of (priority, item); ties go to the lower item so runs are
    // reproducible. net472 has no PriorityQueue.
    private sealed class MinHeap
    {
        private readonly List<long> keys = new List<long>();

        public int Count => keys.Count;

        public void Push(int priority, int item)
        {
            keys.Add(((long)priority << 32) | (uint)item);
            int i = keys.Count - 1;
            while (i > 0)
            {
                int up = (i - 1) / 2;
                if (keys[up] <= keys[i])
                {
                    break;
                }
                (keys[up], keys[i]) = (keys[i], keys[up]);
                i = up;
            }
        }

        public void Pop(out int priority, out int item)
        {
            long top = keys[0];
            priority = (int)(top >> 32);
            item = (int)(top & 0xFFFFFFFF);
            int last = keys.Count - 1;
            keys[0] = keys[last];
            keys.RemoveAt(last);
            int i = 0;
            while (true)
            {
                int left = (2 * i) + 1;
                if (left >= keys.Count)
                {
                    break;
                }
                int least = left + 1 < keys.Count && keys[left + 1] < keys[left] ? left + 1 : left;
                if (keys[i] <= keys[least])
                {
                    break;
                }
                (keys[least], keys[i]) = (keys[i], keys[least]);
                i = least;
            }
        }
    }
}
