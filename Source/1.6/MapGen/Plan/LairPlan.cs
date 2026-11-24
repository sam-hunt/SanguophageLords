using System.Collections.Generic;
using System.Text;
using Verse;

namespace SanguophageLair;

public enum PlanCell : byte
{
    // Untouched: whatever the host map already has there (rock, on the underground map).
    None,
    // Interior of a room.
    Floor,
    // A constructed wall.
    Wall,
    // Living rock left standing as a room's wall; the stamper fills it in where a cave had
    // opened the cell, so a hewn room is as sealed as a built one.
    HewnWall,
    // A door in a wall line.
    Door,
    // An open doorway in a wall line: floor, no door.
    Arch,
    // Open natural cave floor carved for the approach.
    Cave,
    ShallowWater,
    DeepWater,
    // Paving carried across the river.
    Bridge,
}

public enum PlanWalls
{
    Built,
    Hewn,
    // No wall ring at all: an open paved area such as the forecourt.
    Open,
}

public enum PlanRole
{
    Room,
    Sanctum,
    Processional,
    Vestibule,
    Corridor,
    EntranceHall,
    Forecourt,
}

public enum PlanOpening
{
    Door,
    Arch,
}

// One way between two rooms: the wall-line cells that were opened and what fills them.
public sealed class PlanConnection
{
    // Room ids; From is the side nearer the gate. -1 is the open approach outside the complex.
    public int From;
    public int To;
    public PlanOpening Opening;
    // Direction of travel from From into To.
    public Rot4 Dir;
    public List<IntVec3> Cells = new List<IntVec3>();
}

public sealed class PlanRoom
{
    public int Id;
    public RoomRequest Request;
    public PlanRole Role;
    public RoomShapeKind Shape;
    public PlanWalls Walls;
    // Interior cells and their bounding box, in plan coordinates.
    public List<IntVec3> Cells = new List<IntVec3>();
    public CellRect Bounds;
    // The side of the room its entrance is on (the way back toward the gate). Contents face
    // it: a room's focal piece stands against the opposite side.
    public Rot4 EntrySide;
    // Doors passed on the way in from the approach; the vestibule is 0.
    public int Depth;
    public int Parent = -1;
    public int Children;
    // Lies on the lair's axis of symmetry.
    public bool OnAxis;
    // Id of the room this one mirrors across the axis, or -1.
    public int MirrorOf = -1;

    public IntVec3 Center => Bounds.CenterCell;
}

// The whole lair as an abstract cell grid plus the room graph over it, independent of any Map.
// LairPlanner produces one in a canonical frame (the approach at low z, the complex growing
// toward high z along the axis x = AxisX); Rotated turns it to face any direction; the stamping
// genstep then reads it cell by cell. Keeping the plan free of Map is what lets the layout be
// unit-tested, and what lets the same complex be stamped into an underground pocket map or set
// into a surface mountain.
public sealed class LairPlan
{
    private readonly PlanCell[] cells;
    private readonly short[] roomAt;

    public int Width { get; }

    public int Height { get; }

    public List<PlanRoom> Rooms { get; } = new List<PlanRoom>();

    public List<PlanConnection> Connections { get; } = new List<PlanConnection>();

    // Direction of travel from the approach into the complex.
    public Rot4 Facing = Rot4.North;

    // Centre cell of the stairwell exit in the entrance hall; Invalid when the plan has no
    // underground approach.
    public IntVec3 StairwellCell = IntVec3.Invalid;

    // Downstream direction of the river; Invalid when there is none.
    public Rot4 RiverFlow = Rot4.Invalid;

    public LairPlan(int width, int height)
    {
        Width = width;
        Height = height;
        cells = new PlanCell[width * height];
        roomAt = new short[width * height];
        for (int i = 0; i < roomAt.Length; i++)
        {
            roomAt[i] = -1;
        }
    }

    public bool InBounds(int x, int z)
    {
        return x >= 0 && z >= 0 && x < Width && z < Height;
    }

    public bool InBounds(IntVec3 c)
    {
        return InBounds(c.x, c.z);
    }

    public PlanCell this[int x, int z]
    {
        get => cells[(z * Width) + x];
        set => cells[(z * Width) + x] = value;
    }

    public PlanCell this[IntVec3 c]
    {
        get => cells[(c.z * Width) + c.x];
        set => cells[(c.z * Width) + c.x] = value;
    }

    // Out-of-bounds cells read as None so neighbour scans need no bounds checks.
    public PlanCell CellOrNone(int x, int z)
    {
        return InBounds(x, z) ? cells[(z * Width) + x] : PlanCell.None;
    }

    // The room whose interior holds the cell, or -1.
    public int RoomIdAt(int x, int z)
    {
        return InBounds(x, z) ? roomAt[(z * Width) + x] : -1;
    }

    public int RoomIdAt(IntVec3 c)
    {
        return RoomIdAt(c.x, c.z);
    }

    public void SetRoomAt(int x, int z, int roomId)
    {
        roomAt[(z * Width) + x] = (short)roomId;
    }

    public static bool IsWall(PlanCell cell)
    {
        return cell == PlanCell.Wall || cell == PlanCell.HewnWall;
    }

    // Open to walk through as far as the plan is concerned (doors included).
    public static bool IsPassable(PlanCell cell)
    {
        return cell != PlanCell.None && !IsWall(cell);
    }

    public IEnumerable<PlanConnection> ConnectionsOf(PlanRoom room)
    {
        for (int i = 0; i < Connections.Count; i++)
        {
            if (Connections[i].From == room.Id || Connections[i].To == room.Id)
            {
                yield return Connections[i];
            }
        }
    }

    // The plan turned clockwise by rot from the canonical North-facing frame. Square or not,
    // the result's bounds start at the origin again.
    public LairPlan Rotated(Rot4 rot)
    {
        if (rot == Rot4.North)
        {
            return this;
        }
        bool swap = rot == Rot4.East || rot == Rot4.West;
        var result = new LairPlan(swap ? Height : Width, swap ? Width : Height)
        {
            Facing = Turn(Facing, rot),
            StairwellCell = StairwellCell.IsValid ? Turn(StairwellCell, rot) : IntVec3.Invalid,
            RiverFlow = RiverFlow.IsValid ? Turn(RiverFlow, rot) : Rot4.Invalid,
        };
        for (int z = 0; z < Height; z++)
        {
            for (int x = 0; x < Width; x++)
            {
                IntVec3 c = Turn(new IntVec3(x, 0, z), rot);
                result[c] = this[x, z];
                result.SetRoomAt(c.x, c.z, roomAt[(z * Width) + x]);
            }
        }
        foreach (PlanRoom room in Rooms)
        {
            var turned = new PlanRoom
            {
                Id = room.Id,
                Request = room.Request,
                Role = room.Role,
                Shape = room.Shape,
                Walls = room.Walls,
                EntrySide = Turn(room.EntrySide, rot),
                Depth = room.Depth,
                Parent = room.Parent,
                Children = room.Children,
                OnAxis = room.OnAxis,
                MirrorOf = room.MirrorOf,
            };
            foreach (IntVec3 c in room.Cells)
            {
                turned.Cells.Add(Turn(c, rot));
            }
            turned.Bounds = CellRect.FromLimits(Turn(room.Bounds.Min, rot), Turn(room.Bounds.Max, rot));
            result.Rooms.Add(turned);
        }
        foreach (PlanConnection connection in Connections)
        {
            var turned = new PlanConnection
            {
                From = connection.From,
                To = connection.To,
                Opening = connection.Opening,
                Dir = Turn(connection.Dir, rot),
            };
            foreach (IntVec3 c in connection.Cells)
            {
                turned.Cells.Add(Turn(c, rot));
            }
            result.Connections.Add(turned);
        }
        return result;
    }

    private static Rot4 Turn(Rot4 dir, Rot4 by)
    {
        return new Rot4((dir.AsInt + by.AsInt) % 4);
    }

    private IntVec3 Turn(IntVec3 c, Rot4 rot)
    {
        if (rot == Rot4.East)
        {
            return new IntVec3(c.z, 0, Width - 1 - c.x);
        }
        if (rot == Rot4.South)
        {
            return new IntVec3(Width - 1 - c.x, 0, Height - 1 - c.z);
        }
        if (rot == Rot4.West)
        {
            return new IntVec3(Height - 1 - c.z, 0, c.x);
        }
        return c;
    }

    // One character per cell, north at the top: the planner's debugging and test view.
    public string ToAscii()
    {
        var sb = new StringBuilder((Width + 1) * Height);
        for (int z = Height - 1; z >= 0; z--)
        {
            for (int x = 0; x < Width; x++)
            {
                sb.Append(Glyph(x, z));
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }

    private char Glyph(int x, int z)
    {
        if (StairwellCell.IsValid && StairwellCell.x == x && StairwellCell.z == z)
        {
            return 'X';
        }
        switch (this[x, z])
        {
            case PlanCell.Floor:
                int id = RoomIdAt(x, z);
                return id < 0 ? '.' : (char)('a' + (id % 26));
            case PlanCell.Wall:
                return '#';
            case PlanCell.HewnWall:
                return '%';
            case PlanCell.Door:
                return '+';
            case PlanCell.Arch:
                return '\'';
            case PlanCell.Cave:
                return ',';
            case PlanCell.ShallowWater:
                return '~';
            case PlanCell.DeepWater:
                return '=';
            case PlanCell.Bridge:
                return 'H';
            default:
                return ' ';
        }
    }
}
