using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace SanguophageLair;

// Lays out a lair as a LairPlan: pure geometry over an abstract grid, drawing only on Verse.Rand
// (seeded by the calling genstep), IntVec3, CellRect and Rot4, so it runs without a Map.
//
// The plan is built in a canonical frame with the approach at low z and the axis of symmetry at
// x = axisX, in this order:
//  1. Approach (underground only): the stairwell hall, then a cavern crossed by a river, then
//     the forecourt under the facade wall. A paved causeway runs up the axis through all three.
//  2. Spine, on the axis: gate arch, vestibule, door, processional, door, sanctum.
//  3. Growth: each requested room in turn is attached to a room already placed, wall to wall
//     or at the end of a corridor. A room hosted by an on-axis room takes a mirror-image
//     partner on the far side where it can, which keeps the heart of the lair formal while the
//     outskirts, grown one room at a time, come out irregular.
//  4. Loops: extra doors between rooms that ended up sharing a wall, so the plan is not a tree.
//  5. Hewn rooms have their outline roughened into the surrounding rock.
//
// Collision model: a room owns its interior cells and shares its wall ring. A new interior may
// only take untouched cells, a new ring may lie on untouched cells or existing walls. The ring
// is the 8-neighbourhood of the interior, so no two interiors ever touch, even diagonally, and
// a stepped outline gets the solid corner-filled wall the reference art has.
public sealed class LairPlanner
{
    // Openings in the same stretch of wall stay this far apart (Chebyshev distance).
    private const int MinOpeningSpacing = 3;

    private const int TriesPerHost = 6;

    private readonly LairPlanParams parms;
    private readonly LairPlan plan;
    private readonly int axisX;
    private readonly Dictionary<int, Placed> placed = new Dictionary<int, Placed>();
    private readonly List<IntVec3> openings = new List<IntVec3>();
    private readonly List<JournalEntry> journal = new List<JournalEntry>();
    private readonly List<int> childLog = new List<int>();

    // Row of the facade wall: the complex keeps every interior cell above it, the approach
    // everything below.
    private int gateZ;
    private int placedFromProgram;

    private struct Placed
    {
        public RoomShape Shape;
        public int X0;
        public int Z0;
    }

    private struct JournalEntry
    {
        public int X;
        public int Z;
        public PlanCell Cell;
        public int Room;
    }

    private struct Mark
    {
        public int Journal;
        public int Rooms;
        public int Connections;
        public int Openings;
        public int ChildLog;
    }

    // Everything that fixes one attachment, in absolute plan coordinates. For an east or west
    // attachment both coordinates run along z, so the mirror image across the axis is the same
    // spec applied in the opposite direction.
    private struct AttachSpec
    {
        public RoomShape Shape;
        // Centre of the opening in the host's wall, along the wall.
        public int T;
        public int CorridorLength;
        public int CorridorWidth;
        // Min corner of the room's bounding box, along the wall.
        public int ChildMin;
    }

    private LairPlanner(LairPlanParams parms)
    {
        this.parms = parms;
        plan = new LairPlan(parms.Width, parms.Height);
        axisX = parms.Width / 2;
    }

    // The best of a few attempts: the first plan that places the whole program, else the one
    // that placed the most of it. Null only when even the spine does not fit the given size.
    public static LairPlan Generate(LairPlanParams parms)
    {
        LairPlan best = null;
        int bestPlaced = -1;
        for (int attempt = 0; attempt < Math.Max(1, parms.Attempts); attempt++)
        {
            var planner = new LairPlanner(parms);
            if (!planner.Build())
            {
                continue;
            }
            if (planner.placedFromProgram > bestPlaced)
            {
                best = planner.plan;
                bestPlaced = planner.placedFromProgram;
            }
            if (bestPlaced >= parms.Program.Count)
            {
                break;
            }
        }
        return best;
    }

    private bool Build()
    {
        RoomShape hallShape = null;
        if (parms.UndergroundApproach)
        {
            hallShape = PickShape(parms.EntranceHall, Rot4.North);
            // Hall, its far wall, south bank, river, north bank, forecourt: see BuildApproach.
            gateZ = parms.Margin + hallShape.Height + 1 + 15 + parms.ForecourtDepth + 1;
        }
        else
        {
            gateZ = parms.Margin + parms.ForecourtDepth;
        }
        BuildForecourtAndFacade();
        if (!BuildSpine())
        {
            return false;
        }
        Grow();
        AddLoops();
        RoughenHewnRooms();
        if (parms.UndergroundApproach)
        {
            BuildApproach(hallShape);
        }
        AssignDepths();
        return true;
    }

    // ----- Spine -----

    private void BuildForecourtAndFacade()
    {
        int half = parms.ForecourtWidth / 2;
        RoomShape shape = RoomShape.Rect((half * 2) + 1, parms.ForecourtDepth);
        Commit(parms.Forecourt, shape, axisX - half, gateZ - parms.ForecourtDepth, PlanRole.Forecourt, PlanWalls.Open, -1, Rot4.South);
        // The facade is two cells thick and a little wider than the paving in front of it.
        // Written before any room so nothing can grow into it; rings may still share it.
        for (int x = axisX - half - 2; x <= axisX + half + 2; x++)
        {
            for (int z = gateZ; z <= gateZ + 1; z++)
            {
                if (plan.InBounds(x, z) && plan[x, z] == PlanCell.None)
                {
                    Set(x, z, PlanCell.Wall, -1);
                }
            }
        }
    }

    private bool BuildSpine()
    {
        // The facade's inner layer stands where the vestibule goes; clear that stretch first.
        RoomShape vestibuleShape = PickShape(parms.Vestibule, Rot4.North);
        int vx0 = axisX - (vestibuleShape.Width / 2);
        for (int x = vx0 - 1; x <= vx0 + vestibuleShape.Width; x++)
        {
            Set(x, gateZ + 1, PlanCell.None, -1);
        }
        if (!Fits(vestibuleShape, vx0, gateZ + 1, complex: true))
        {
            return false;
        }
        PlanRoom vestibule = Commit(parms.Vestibule, vestibuleShape, vx0, gateZ + 1, PlanRole.Vestibule, parms.Vestibule.Walls, -1, Rot4.South);
        vestibule.OnAxis = true;
        var gate = new PlanConnection { From = -1, To = vestibule.Id, Opening = PlanOpening.Arch, Dir = Rot4.North };
        for (int x = axisX - 1; x <= axisX + 1; x++)
        {
            Set(x, gateZ, PlanCell.Arch, -1);
            gate.Cells.Add(new IntVec3(x, 0, gateZ));
            openings.Add(new IntVec3(x, 0, gateZ));
        }
        plan.Connections.Add(gate);

        PlanRoom processional = AttachOnAxis(parms.Processional, vestibule, PlanRole.Processional);
        if (processional == null)
        {
            return false;
        }
        return AttachOnAxis(parms.Sanctum, processional, PlanRole.Sanctum) != null;
    }

    private PlanRoom AttachOnAxis(RoomRequest request, PlanRoom host, PlanRole role)
    {
        RoomShape shape = PickShape(request, Rot4.North);
        var spec = new AttachSpec { Shape = shape, T = axisX, ChildMin = axisX - (shape.Width / 2) };
        return TryAttach(spec, host, Rot4.North, request, role, -1);
    }

    // ----- Growth -----

    private void Grow()
    {
        var pending = new List<RoomRequest>(parms.Program);
        while (pending.Count > 0)
        {
            RoomRequest request = pending[0];
            pending.RemoveAt(0);
            // Strict first: a room that wants a partner only goes where the partner fits too.
            if (TryPlace(request, pending, strictPairs: true) || TryPlace(request, pending, strictPairs: false))
            {
                placedFromProgram++;
            }
        }
    }

    private bool TryPlace(RoomRequest request, List<RoomRequest> pending, bool strictPairs)
    {
        var hosts = new List<PlanRoom>();
        var dirs = new List<Rot4>();
        var weights = new List<float>();
        float total = 0f;
        foreach (PlanRoom host in plan.Rooms)
        {
            for (int i = 0; i < 4; i++)
            {
                var dir = new Rot4(i);
                float weight = HostWeight(request, host, dir);
                if (weight > 0f)
                {
                    hosts.Add(host);
                    dirs.Add(dir);
                    weights.Add(weight);
                    total += weight;
                }
            }
        }
        while (hosts.Count > 0)
        {
            int pick = PickWeighted(weights, total);
            PlanRoom host = hosts[pick];
            Rot4 dir = dirs[pick];
            total -= weights[pick];
            hosts.RemoveAt(pick);
            dirs.RemoveAt(pick);
            weights.RemoveAt(pick);

            bool wantsPair = request.Paired && host.OnAxis && dir.IsHorizontal;
            for (int i = 0; i < TriesPerHost; i++)
            {
                if (!RollSpec(request, host, dir, out AttachSpec spec))
                {
                    continue;
                }
                Mark mark = TakeMark();
                PlanRoom room = TryAttach(spec, host, dir, request, PlanRole.Room, -1);
                if (room == null)
                {
                    Rollback(mark);
                    continue;
                }
                if (wantsPair)
                {
                    RoomRequest partner = FindPartner(request, spec, pending);
                    if (partner != null)
                    {
                        Mark beforePartner = TakeMark();
                        if (TryAttach(spec, host, dir.Opposite, partner, PlanRole.Room, room.Id) != null)
                        {
                            pending.Remove(partner);
                            placedFromProgram++;
                        }
                        else if (strictPairs)
                        {
                            Rollback(mark);
                            continue;
                        }
                        else
                        {
                            Rollback(beforePartner);
                        }
                    }
                }
                return true;
            }
        }
        return false;
    }

    // How much a request wants to hang off this side of this host; 0 rules the pairing out.
    private float HostWeight(RoomRequest request, PlanRoom host, Rot4 dir)
    {
        if (host.Role == PlanRole.Forecourt || host.Role == PlanRole.EntranceHall || host.Role == PlanRole.Vestibule || host.Role == PlanRole.Corridor)
        {
            return 0f;
        }
        if (host.Children >= host.Request.MaxChildren || host.Depth + 1 < request.MinDepth)
        {
            return 0f;
        }
        // The spine's own ends: the sanctum's south face is the processional's door, the
        // processional's ends are the vestibule and the sanctum.
        if (host.Role == PlanRole.Sanctum && dir == Rot4.South)
        {
            return 0f;
        }
        if (host.Role == PlanRole.Processional && !dir.IsHorizontal)
        {
            return 0f;
        }
        float weight;
        switch (request.Anchor)
        {
            case PlanAnchor.AxisNorth:
                if (dir != Rot4.North || !host.OnAxis || host.Role == PlanRole.Processional)
                {
                    return 0f;
                }
                weight = host.Role == PlanRole.Sanctum ? 10f : 1f;
                break;
            case PlanAnchor.SanctumSide:
                if (host.Role != PlanRole.Sanctum || !dir.IsHorizontal)
                {
                    return 0f;
                }
                weight = 1f;
                break;
            case PlanAnchor.ProcessionalSide:
                if (host.Role != PlanRole.Processional)
                {
                    return 0f;
                }
                weight = 1f;
                break;
            default:
                // Leftover faces of the spine stay usable, but ordinary rooms come first.
                weight = host.Role == PlanRole.Room ? 1f : 0.25f;
                break;
        }
        return weight * Mathf.Pow(host.Depth + 1, request.DepthBias);
    }

    private static int PickWeighted(List<float> weights, float total)
    {
        float roll = Rand.Value * total;
        for (int i = 0; i < weights.Count; i++)
        {
            roll -= weights[i];
            if (roll <= 0f)
            {
                return i;
            }
        }
        return weights.Count - 1;
    }

    // The next waiting request that can take the mirror image of a room just placed.
    private static RoomRequest FindPartner(RoomRequest request, AttachSpec spec, List<RoomRequest> pending)
    {
        foreach (RoomRequest candidate in pending)
        {
            if (!candidate.Paired || (candidate.Anchor != request.Anchor && candidate.Anchor != PlanAnchor.Any))
            {
                continue;
            }
            foreach (ShapeOption option in candidate.Shapes)
            {
                // The placed shape is already turned for an east or west entrance.
                int across = spec.Shape.Height;
                int deep = spec.Shape.Width;
                if (option.kind == spec.Shape.Kind && across >= option.across.min && across <= option.across.max
                    && (option.kind == RoomShapeKind.Round || (deep >= option.deep.min && deep <= option.deep.max)))
                {
                    return candidate;
                }
            }
        }
        return null;
    }

    private bool RollSpec(RoomRequest request, PlanRoom host, Rot4 dir, out AttachSpec spec)
    {
        spec = new AttachSpec { Shape = PickShape(request, dir) };
        Placed hp = placed[host.Id];
        hp.Shape.FaceRun(dir, out int runStart, out int runLength);
        runStart += dir.IsHorizontal ? hp.Z0 : hp.X0;

        bool corridor = Rand.Chance(request.CorridorChance);
        if (corridor)
        {
            spec.CorridorWidth = Rand.Chance(parms.WideCorridorChance) ? 3 : 1;
            spec.CorridorLength = request.CorridorLength.RandomInRange;
        }
        int hostOpening = corridor || request.Entry == PlanOpening.Door ? 1 : 3;
        int pad = runLength >= hostOpening + 2 ? 1 : 0;
        int tMin = runStart + pad + (hostOpening / 2);
        int tMax = runStart + runLength - 1 - pad - (hostOpening / 2);
        if (tMin > tMax)
        {
            return false;
        }
        int centre = runStart + (runLength / 2);
        bool mustCentre = hp.Shape.Kind != RoomShapeKind.Rect || (host.OnAxis && !dir.IsHorizontal);
        bool centred = host.Role != PlanRole.Processional && runLength % 2 == 1 && (mustCentre || Rand.Chance(0.6f));
        spec.T = centred ? centre : Rand.RangeInclusive(tMin, tMax);

        spec.Shape.FaceRun(dir.Opposite, out int childRunStart, out int childRunLength);
        int childOpening = corridor ? (request.Entry == PlanOpening.Arch ? spec.CorridorWidth : 1) : hostOpening;
        if (childRunLength < childOpening)
        {
            return false;
        }
        int childPad = childRunLength >= childOpening + 2 ? 1 : 0;
        int minChildMin = spec.T + (childOpening / 2) - childRunStart - childRunLength + 1 + childPad;
        int maxChildMin = spec.T - (childOpening / 2) - childRunStart - childPad;
        bool childCentred = childRunLength % 2 == 1 && (spec.Shape.Kind != RoomShapeKind.Rect || mustCentre || Rand.Chance(0.65f));
        spec.ChildMin = childCentred
            ? spec.T - childRunStart - (childRunLength / 2)
            : Rand.RangeInclusive(minChildMin, maxChildMin);
        return true;
    }

    // Places a room (and its corridor, if any) off one face of a host and opens the way in.
    // Returns null with the plan possibly half-written; the caller rolls back to its mark.
    private PlanRoom TryAttach(AttachSpec spec, PlanRoom host, Rot4 dir, RoomRequest request, PlanRole role, int mirrorOf)
    {
        Placed hp = placed[host.Id];
        int sign = dir == Rot4.North || dir == Rot4.East ? 1 : -1;
        int wall = dir == Rot4.North ? hp.Z0 + hp.Shape.Height
            : dir == Rot4.South ? hp.Z0 - 1
            : dir == Rot4.East ? hp.X0 + hp.Shape.Width
            : hp.X0 - 1;
        PlanRoom parent = host;
        if (spec.CorridorLength > 0)
        {
            RoomShape corridorShape = dir.IsHorizontal
                ? RoomShape.Rect(spec.CorridorLength, spec.CorridorWidth)
                : RoomShape.Rect(spec.CorridorWidth, spec.CorridorLength);
            Origin(corridorShape, dir, wall, spec.T - (spec.CorridorWidth / 2), out int cx0, out int cz0);
            if (!Fits(corridorShape, cx0, cz0, complex: true))
            {
                return null;
            }
            PlanRoom corridor = Commit(parms.Corridor, corridorShape, cx0, cz0, PlanRole.Corridor, parms.Corridor.Walls, host.Id, dir.Opposite);
            corridor.Depth = host.Depth + 1;
            if (!Open(host, corridor, dir, wall, spec.T, 1, PlanOpening.Door))
            {
                return null;
            }
            wall += sign * (spec.CorridorLength + 1);
            parent = corridor;
        }
        Origin(spec.Shape, dir, wall, spec.ChildMin, out int x0, out int z0);
        if (!Fits(spec.Shape, x0, z0, complex: true))
        {
            return null;
        }
        PlanRoom room = Commit(request, spec.Shape, x0, z0, role, request.Walls, parent.Id, dir.Opposite);
        int width = request.Entry == PlanOpening.Door ? 1 : spec.CorridorLength > 0 ? spec.CorridorWidth : 3;
        if (!Open(parent, room, dir, wall, spec.T, width, request.Entry))
        {
            return null;
        }
        // Kept current while the plan grows, since depth steers where later rooms go;
        // AssignDepths settles the final values once loops have added shortcuts.
        room.Depth = parent.Depth + (request.Entry == PlanOpening.Door ? 1 : 0);
        room.MirrorOf = mirrorOf;
        room.OnAxis = !dir.IsHorizontal && host.OnAxis && spec.T == axisX && x0 + (spec.Shape.Width / 2) == axisX && spec.Shape.Width % 2 == 1;
        host.Children++;
        childLog.Add(host.Id);
        return room;
    }

    // Bounding-box origin of a shape whose near face stands just beyond a wall line.
    private static void Origin(RoomShape shape, Rot4 dir, int wall, int alongMin, out int x0, out int z0)
    {
        if (dir == Rot4.North)
        {
            x0 = alongMin;
            z0 = wall + 1;
        }
        else if (dir == Rot4.South)
        {
            x0 = alongMin;
            z0 = wall - shape.Height;
        }
        else if (dir == Rot4.East)
        {
            x0 = wall + 1;
            z0 = alongMin;
        }
        else
        {
            x0 = wall - shape.Width;
            z0 = alongMin;
        }
    }

    // Turns a run of wall cells between two rooms into a door or an arch. The run must be flat:
    // each cell has the first room behind it and the second ahead, and the cells just past its
    // ends stay wall, so a door never ends up with floor beside it.
    private bool Open(PlanRoom from, PlanRoom to, Rot4 dir, int wall, int t, int width, PlanOpening opening)
    {
        IntVec3 ahead = dir.FacingCell;
        var cells = new List<IntVec3>(width);
        for (int i = -(width / 2) - 1; i <= (width / 2) + 1; i++)
        {
            IntVec3 c = dir.IsHorizontal ? new IntVec3(wall, 0, t + i) : new IntVec3(t + i, 0, wall);
            if (!plan.InBounds(c) || !LairPlan.IsWall(plan[c]))
            {
                return false;
            }
            if (Math.Abs(i) > width / 2)
            {
                continue;
            }
            if (plan.RoomIdAt(c - ahead) != from.Id || plan.RoomIdAt(c + ahead) != to.Id || !FarFromOpenings(c))
            {
                return false;
            }
            cells.Add(c);
        }
        var connection = new PlanConnection { From = from.Id, To = to.Id, Opening = opening, Dir = dir };
        foreach (IntVec3 c in cells)
        {
            Set(c.x, c.z, opening == PlanOpening.Door ? PlanCell.Door : PlanCell.Arch, -1);
            connection.Cells.Add(c);
            openings.Add(c);
        }
        plan.Connections.Add(connection);
        return true;
    }

    private bool FarFromOpenings(IntVec3 c)
    {
        for (int i = 0; i < openings.Count; i++)
        {
            if (Math.Abs(openings[i].x - c.x) < MinOpeningSpacing && Math.Abs(openings[i].z - c.z) < MinOpeningSpacing)
            {
                return false;
            }
        }
        return true;
    }

    // ----- Grid primitives -----

    private RoomShape PickShape(RoomRequest request, Rot4 dir)
    {
        ShapeOption option = request.Shapes[0];
        if (request.Shapes.Count > 1)
        {
            float total = 0f;
            foreach (ShapeOption o in request.Shapes)
            {
                total += o.weight;
            }
            float roll = Rand.Value * total;
            foreach (ShapeOption o in request.Shapes)
            {
                option = o;
                roll -= o.weight;
                if (roll <= 0f)
                {
                    break;
                }
            }
        }
        int across = RandomOdd(option.across);
        int deep = option.kind == RoomShapeKind.Round ? across : RandomOdd(option.deep);
        RoomShape shape;
        switch (option.kind)
        {
            case RoomShapeKind.Octagon:
                shape = RoomShape.Octagon(across, deep, Mathf.Max(1, Mathf.RoundToInt(Math.Min(across, deep) * option.chamfer)));
                break;
            case RoomShapeKind.Round:
                shape = RoomShape.Round(across);
                break;
            default:
                shape = RoomShape.Rect(across, deep);
                break;
        }
        return dir.IsHorizontal ? shape.Transposed() : shape;
    }

    // Odd sizes give every room a centre cell and a centred doorway, which the symmetric
    // contents rely on. A range holding no odd number is used as given.
    private static int RandomOdd(IntRange range)
    {
        int low = range.min % 2 == 1 ? range.min : range.min + 1;
        int high = range.max % 2 == 1 ? range.max : range.max - 1;
        return low > high ? range.RandomInRange : low + (2 * Rand.RangeInclusive(0, (high - low) / 2));
    }

    private bool Fits(RoomShape shape, int x0, int z0, bool complex)
    {
        int minZ = complex ? gateZ + 1 : parms.Margin;
        for (int lz = -1; lz <= shape.Height; lz++)
        {
            for (int lx = -1; lx <= shape.Width; lx++)
            {
                int x = x0 + lx;
                int z = z0 + lz;
                if (shape.Contains(lx, lz))
                {
                    if (x < parms.Margin || x >= plan.Width - parms.Margin || z < minZ || z >= plan.Height - parms.Margin
                        || plan[x, z] != PlanCell.None)
                    {
                        return false;
                    }
                }
                else if (IsRing(shape, lx, lz))
                {
                    if (!plan.InBounds(x, z))
                    {
                        return false;
                    }
                    PlanCell cell = plan[x, z];
                    if (cell != PlanCell.None && !LairPlan.IsWall(cell))
                    {
                        return false;
                    }
                }
            }
        }
        return true;
    }

    private static bool IsRing(RoomShape shape, int lx, int lz)
    {
        for (int dz = -1; dz <= 1; dz++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (shape.Contains(lx + dx, lz + dz))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private PlanRoom Commit(RoomRequest request, RoomShape shape, int x0, int z0, PlanRole role, PlanWalls walls, int parent, Rot4 entrySide)
    {
        var room = new PlanRoom
        {
            Id = plan.Rooms.Count,
            Request = request,
            Role = role,
            Shape = shape.Kind,
            Walls = walls,
            Bounds = new CellRect(x0, z0, shape.Width, shape.Height),
            EntrySide = entrySide,
            Parent = parent,
        };
        for (int lz = -1; lz <= shape.Height; lz++)
        {
            for (int lx = -1; lx <= shape.Width; lx++)
            {
                int x = x0 + lx;
                int z = z0 + lz;
                if (shape.Contains(lx, lz))
                {
                    Set(x, z, PlanCell.Floor, room.Id);
                    room.Cells.Add(new IntVec3(x, 0, z));
                }
                else if (walls != PlanWalls.Open && IsRing(shape, lx, lz))
                {
                    // Built stonework wins a shared wall over bare rock.
                    PlanCell cell = plan[x, z];
                    if (cell == PlanCell.None || (cell == PlanCell.HewnWall && walls == PlanWalls.Built))
                    {
                        Set(x, z, walls == PlanWalls.Built ? PlanCell.Wall : PlanCell.HewnWall, -1);
                    }
                }
            }
        }
        plan.Rooms.Add(room);
        placed[room.Id] = new Placed { Shape = shape, X0 = x0, Z0 = z0 };
        return room;
    }

    private void Set(int x, int z, PlanCell cell, int room)
    {
        journal.Add(new JournalEntry { X = x, Z = z, Cell = plan[x, z], Room = plan.RoomIdAt(x, z) });
        plan[x, z] = cell;
        plan.SetRoomAt(x, z, room);
    }

    private Mark TakeMark()
    {
        return new Mark
        {
            Journal = journal.Count,
            Rooms = plan.Rooms.Count,
            Connections = plan.Connections.Count,
            Openings = openings.Count,
            ChildLog = childLog.Count,
        };
    }

    private void Rollback(Mark mark)
    {
        for (int i = journal.Count - 1; i >= mark.Journal; i--)
        {
            JournalEntry entry = journal[i];
            plan[entry.X, entry.Z] = entry.Cell;
            plan.SetRoomAt(entry.X, entry.Z, entry.Room);
        }
        journal.RemoveRange(mark.Journal, journal.Count - mark.Journal);
        for (int i = childLog.Count - 1; i >= mark.ChildLog; i--)
        {
            plan.Rooms[childLog[i]].Children--;
        }
        childLog.RemoveRange(mark.ChildLog, childLog.Count - mark.ChildLog);
        for (int i = plan.Rooms.Count - 1; i >= mark.Rooms; i--)
        {
            placed.Remove(i);
        }
        plan.Rooms.RemoveRange(mark.Rooms, plan.Rooms.Count - mark.Rooms);
        plan.Connections.RemoveRange(mark.Connections, plan.Connections.Count - mark.Connections);
        openings.RemoveRange(mark.Openings, openings.Count - mark.Openings);
    }

    // ----- Loops -----

    // Rooms placed against each other share a wall with no way through. Opening some of those
    // walls turns the tree the growth pass built into a graph with more than one route.
    private void AddLoops()
    {
        var shared = new Dictionary<long, List<IntVec3>>();
        var dirOf = new Dictionary<long, Rot4>();
        for (int z = 1; z < plan.Height - 1; z++)
        {
            for (int x = 1; x < plan.Width - 1; x++)
            {
                if (!LairPlan.IsWall(plan[x, z]))
                {
                    continue;
                }
                Rot4 dir;
                int a;
                int b;
                if (LairPlan.IsWall(plan[x - 1, z]) && LairPlan.IsWall(plan[x + 1, z]))
                {
                    dir = Rot4.North;
                    a = plan.RoomIdAt(x, z - 1);
                    b = plan.RoomIdAt(x, z + 1);
                }
                else if (LairPlan.IsWall(plan[x, z - 1]) && LairPlan.IsWall(plan[x, z + 1]))
                {
                    dir = Rot4.East;
                    a = plan.RoomIdAt(x - 1, z);
                    b = plan.RoomIdAt(x + 1, z);
                }
                else
                {
                    continue;
                }
                if (a < 0 || b < 0 || a == b || !CanLoop(plan.Rooms[a]) || !CanLoop(plan.Rooms[b]))
                {
                    continue;
                }
                long key = ((long)a << 32) | (uint)b;
                if (!shared.TryGetValue(key, out List<IntVec3> cells))
                {
                    cells = new List<IntVec3>();
                    shared[key] = cells;
                    dirOf[key] = dir;
                }
                cells.Add(new IntVec3(x, 0, z));
            }
        }
        foreach (KeyValuePair<long, List<IntVec3>> pair in shared)
        {
            int a = (int)(pair.Key >> 32);
            int b = (int)(pair.Key & 0xffffffff);
            if (Connected(a, b) || !Rand.Chance(parms.LoopChance))
            {
                continue;
            }
            IntVec3 cell = pair.Value[pair.Value.Count / 2];
            if (!FarFromOpenings(cell))
            {
                continue;
            }
            Set(cell.x, cell.z, PlanCell.Door, -1);
            openings.Add(cell);
            var connection = new PlanConnection { From = a, To = b, Opening = PlanOpening.Door, Dir = dirOf[pair.Key] };
            connection.Cells.Add(cell);
            plan.Connections.Add(connection);
        }
    }

    private static bool CanLoop(PlanRoom room)
    {
        // The spine keeps its formal doors; a corridor already is a way through.
        return room.Role == PlanRole.Room && room.Request.AllowLoops;
    }

    private bool Connected(int a, int b)
    {
        foreach (PlanConnection connection in plan.Connections)
        {
            if ((connection.From == a && connection.To == b) || (connection.From == b && connection.To == a))
            {
                return true;
            }
        }
        return false;
    }

    // ----- Hewn rooms -----

    // A room cut from living rock should not have ruler-straight walls. Each cell of a hewn
    // room's wall ring that has nothing but rock behind it may become floor, pushing the wall
    // one cell further out. Cells near a doorway are left alone so openings stay framed. The
    // room's Bounds keep describing the regular core its contents are laid out in.
    private void RoughenHewnRooms()
    {
        var bulges = new List<IntVec3>();
        foreach (PlanRoom room in plan.Rooms)
        {
            if (room.Walls != PlanWalls.Hewn || room.Role == PlanRole.Corridor)
            {
                continue;
            }
            bulges.Clear();
            foreach (IntVec3 c in room.Cells)
            {
                for (int i = 0; i < 4; i++)
                {
                    IntVec3 w = c + GenAdj.CardinalDirections[i];
                    if (plan[w] == PlanCell.HewnWall && !bulges.Contains(w) && Rand.Chance(parms.HewnBulgeChance) && CanBulge(w, room.Id))
                    {
                        bulges.Add(w);
                    }
                }
            }
            foreach (IntVec3 w in bulges)
            {
                // Re-checked: an earlier bulge of this pass may have used up the rock behind.
                if (!CanBulge(w, room.Id))
                {
                    continue;
                }
                Set(w.x, w.z, PlanCell.Floor, room.Id);
                room.Cells.Add(w);
                for (int i = 0; i < 8; i++)
                {
                    IntVec3 n = w + GenAdj.AdjacentCells[i];
                    if (plan[n] == PlanCell.None)
                    {
                        Set(n.x, n.z, PlanCell.HewnWall, -1);
                    }
                }
            }
        }
    }

    private bool CanBulge(IntVec3 w, int roomId)
    {
        if (w.x <= parms.Margin || w.z <= gateZ + 1 || w.x >= plan.Width - 1 - parms.Margin || w.z >= plan.Height - 1 - parms.Margin)
        {
            return false;
        }
        for (int i = 0; i < 8; i++)
        {
            IntVec3 n = w + GenAdj.AdjacentCells[i];
            PlanCell cell = plan[n];
            bool ownFloor = cell == PlanCell.Floor && plan.RoomIdAt(n) == roomId;
            if (!ownFloor && cell != PlanCell.None && cell != PlanCell.HewnWall)
            {
                return false;
            }
        }
        foreach (IntVec3 opening in openings)
        {
            if (Math.Abs(opening.x - w.x) <= 2 && Math.Abs(opening.z - w.z) <= 2)
            {
                return false;
            }
        }
        // The wall the bulge pushes outward must not become another room's inner face.
        for (int dz = -2; dz <= 2; dz++)
        {
            for (int dx = -2; dx <= 2; dx++)
            {
                int other = plan.RoomIdAt(w.x + dx, w.z + dz);
                if (other >= 0 && other != roomId)
                {
                    return false;
                }
            }
        }
        return true;
    }

    // ----- Approach -----

    // South to north below the facade: the stairwell hall against the plan's edge, a natural
    // cavern with the river running across it, and the forecourt (already placed). The causeway
    // carries the hall's paving up the axis, over the river, to the forecourt.
    private void BuildApproach(RoomShape hallShape)
    {
        int hallX0 = axisX - (hallShape.Width / 2);
        int hallZ0 = parms.Margin;
        PlanRoom hall = Commit(parms.EntranceHall, hallShape, hallX0, hallZ0, PlanRole.EntranceHall, parms.EntranceHall.Walls, -1, Rot4.North);
        hall.OnAxis = true;
        plan.StairwellCell = hall.Center;
        int hallWall = hallZ0 + hallShape.Height;
        var arch = new PlanConnection { From = hall.Id, To = -1, Opening = PlanOpening.Arch, Dir = Rot4.North };
        for (int x = axisX - 1; x <= axisX + 1; x++)
        {
            Set(x, hallWall, PlanCell.Arch, -1);
            arch.Cells.Add(new IntVec3(x, 0, hallWall));
        }
        plan.Connections.Add(arch);

        int forecourtZ = gateZ - parms.ForecourtDepth;
        int riverZ = hallWall + ((forecourtZ - hallWall) / 2);
        for (int z = hallWall + 1; z < forecourtZ; z++)
        {
            for (int x = axisX - 1; x <= axisX + 1; x++)
            {
                Set(x, z, PlanCell.Bridge, -1);
            }
        }

        // The river crosses the whole plan, wandering a little except where the causeway
        // bridges it. Chest-deep in the middle, shallow at the banks.
        float phaseA = Rand.Range(0f, 6.28f);
        float phaseB = Rand.Range(0f, 6.28f);
        float waveA = Rand.Range(14f, 22f);
        float waveB = Rand.Range(6f, 9f);
        var riverAt = new int[plan.Width];
        for (int x = 0; x < plan.Width; x++)
        {
            float fade = Mathf.SmoothStep(0f, 1f, (Math.Abs(x - axisX) - 4) / 10f);
            float wander = (1.6f * Mathf.Sin((x / waveA) + phaseA)) + (0.9f * Mathf.Sin((x / waveB) + phaseB));
            riverAt[x] = riverZ + Mathf.RoundToInt(wander * fade);
            for (int dz = -2; dz <= 2; dz++)
            {
                int z = riverAt[x] + dz;
                if (plan[x, z] == PlanCell.None)
                {
                    Set(x, z, Math.Abs(dz) <= 1 ? PlanCell.DeepWater : PlanCell.ShallowWater, -1);
                }
            }
        }
        plan.RiverFlow = Rand.Bool ? Rot4.East : Rot4.West;

        // The cavern is as deep as the whole gap in front of the facade and pinches down to
        // the river's banks toward the plan's edges, where the water runs on through a tunnel.
        int coreHalf = (parms.ForecourtWidth / 2) + Rand.RangeInclusive(3, 6);
        float taper = Rand.Range(14f, 20f);
        int jitterSouth = 0;
        int jitterNorth = 0;
        for (int x = 0; x < plan.Width; x++)
        {
            if (x % 2 == 0)
            {
                jitterSouth = Mathf.Clamp(jitterSouth + Rand.RangeInclusive(-1, 1), -1, 1);
                jitterNorth = Mathf.Clamp(jitterNorth + Rand.RangeInclusive(-1, 1), -1, 1);
            }
            float open = 1f - Mathf.SmoothStep(0f, 1f, (Math.Abs(x - axisX) - coreHalf) / taper);
            int south = Mathf.RoundToInt(Mathf.Lerp(riverAt[x] - 3, hallWall + 1, open)) + jitterSouth;
            int north = Mathf.RoundToInt(Mathf.Lerp(riverAt[x] + 3, gateZ - 1, open)) + jitterNorth;
            south = Math.Max(south, hallWall + 1);
            north = Math.Min(north, gateZ - 1);
            for (int z = south; z <= north; z++)
            {
                if (plan[x, z] == PlanCell.None)
                {
                    Set(x, z, PlanCell.Cave, -1);
                }
            }
        }
        SupportCavernRoof(hallWall + 1, gateZ - 1);
    }

    // The cavern is far wider than thick rock roof can span, so it is given what a real one
    // has: pillars of rock left standing wherever the roof would otherwise be more than
    // RoofSpan from a wall. Then the whole open approach gets a skin of living rock, which
    // both guarantees those walls exist (a natural cave tunnel may have run where the plan
    // only says "untouched") and keeps the cave network outside from opening into it.
    private void SupportCavernRoof(int southZ, int northZ)
    {
        const int RoofSpan = 6;
        var open = new List<IntVec3>();
        for (int z = southZ; z <= northZ; z++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                PlanCell cell = plan[x, z];
                if (cell == PlanCell.Cave || cell == PlanCell.ShallowWater || cell == PlanCell.DeepWater || cell == PlanCell.Bridge || cell == PlanCell.Floor)
                {
                    open.Add(new IntVec3(x, 0, z));
                }
            }
        }
        foreach (IntVec3 c in open)
        {
            for (int i = 0; i < 8; i++)
            {
                IntVec3 n = c + GenAdj.AdjacentCells[i];
                if (plan.InBounds(n) && plan[n] == PlanCell.None)
                {
                    Set(n.x, n.z, PlanCell.HewnWall, -1);
                }
            }
        }

        var supported = new HashSet<IntVec3>();
        void Cover(IntVec3 holder)
        {
            for (int dz = -RoofSpan; dz <= RoofSpan; dz++)
            {
                for (int dx = -RoofSpan; dx <= RoofSpan; dx++)
                {
                    if ((dx * dx) + (dz * dz) <= RoofSpan * RoofSpan)
                    {
                        supported.Add(new IntVec3(holder.x + dx, 0, holder.z + dz));
                    }
                }
            }
        }
        for (int z = southZ - 1; z <= northZ + 1; z++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                if (LairPlan.IsWall(plan[x, z]))
                {
                    Cover(new IntVec3(x, 0, z));
                }
            }
        }
        open.Shuffle();
        foreach (IntVec3 c in open)
        {
            if (supported.Contains(c))
            {
                continue;
            }
            // The nearest piece of bare cave floor that is clear of the causeway takes the
            // pillar: a ragged clump of up to four cells.
            IntVec3 site = IntVec3.Invalid;
            int best = int.MaxValue;
            for (int dz = -4; dz <= 4; dz++)
            {
                for (int dx = -4; dx <= 4; dx++)
                {
                    var candidate = new IntVec3(c.x + dx, 0, c.z + dz);
                    int dist = (dx * dx) + (dz * dz);
                    if (dist < best && CanBePillar(candidate))
                    {
                        site = candidate;
                        best = dist;
                    }
                }
            }
            if (!site.IsValid)
            {
                continue;
            }
            for (int i = 0; i < 4; i++)
            {
                var part = new IntVec3(site.x + (i % 2), 0, site.z + (i / 2));
                if (CanBePillar(part) && (i == 0 || Rand.Chance(0.6f)))
                {
                    Set(part.x, part.z, PlanCell.HewnWall, -1);
                    Cover(part);
                }
            }
        }
    }

    private bool CanBePillar(IntVec3 c)
    {
        return plan.InBounds(c) && plan[c] == PlanCell.Cave && Math.Abs(c.x - axisX) >= 4;
    }

    // ----- Depths -----

    // Doors between the open approach and each room, arches counting for nothing. This is the
    // lair's sense of "how far in": rooms are themed and defended by it.
    private void AssignDepths()
    {
        foreach (PlanRoom room in plan.Rooms)
        {
            room.Depth = int.MaxValue;
        }
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (PlanConnection connection in plan.Connections)
            {
                int step = connection.Opening == PlanOpening.Door ? 1 : 0;
                changed |= Relax(connection.From, connection.To, step);
                changed |= Relax(connection.To, connection.From, step);
            }
        }
        foreach (PlanRoom room in plan.Rooms)
        {
            if (room.Depth == int.MaxValue)
            {
                room.Depth = 0;
            }
        }
    }

    private bool Relax(int from, int to, int step)
    {
        if (to < 0)
        {
            return false;
        }
        int fromDepth = from < 0 ? 0 : plan.Rooms[from].Depth;
        if (fromDepth == int.MaxValue || fromDepth + step >= plan.Rooms[to].Depth)
        {
            return false;
        }
        plan.Rooms[to].Depth = fromDepth + step;
        return true;
    }
}
