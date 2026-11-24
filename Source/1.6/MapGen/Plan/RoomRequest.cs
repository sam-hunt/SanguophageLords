using System.Collections.Generic;
using Verse;

namespace SanguophageLair;

// Where in the complex a room asks to stand.
public enum PlanAnchor
{
    // Off any room that still takes children.
    Any,
    // On the axis, beyond the sanctum.
    AxisNorth,
    // Off the sanctum's east or west face.
    SanctumSide,
    // Off the processional's long sides.
    ProcessionalSide,
}

// One shape a room may take. Sizes are relative to the way the room is entered: Across is the
// interior width of the wall the entrance is in, Deep the distance from that wall to the far
// one. Round rooms use Across as the diameter.
public sealed class ShapeOption
{
    public RoomShapeKind kind = RoomShapeKind.Rect;
    public IntRange across = new IntRange(7, 9);
    public IntRange deep = new IntRange(7, 9);
    // Octagon corner cut as a fraction of the shorter side.
    public float chamfer = 0.28f;
    public float weight = 1f;
}

// What the planner needs to know about one room it is asked to place. Plain data with no def
// or Map reference of its own, so plans can be built in unit tests; the generating genstep
// fills these in from LairRoomDefs and reads Source back to find the def that fills the room.
public sealed class RoomRequest
{
    public string Key;
    public object Source;
    public PlanRole Role = PlanRole.Room;
    public List<ShapeOption> Shapes = new List<ShapeOption>();
    public PlanWalls Walls = PlanWalls.Built;
    public PlanAnchor Anchor = PlanAnchor.Any;
    // Wants a mirror-image partner on the other side of the axis when its host lies on it.
    public bool Paired;
    // How the room's own entrance is closed.
    public PlanOpening Entry = PlanOpening.Door;
    // Further rooms that may hang off this one.
    public int MaxChildren = 2;
    // May receive a second door into a neighbour it happens to share a wall with.
    public bool AllowLoops = true;
    // Chance of standing off its host at the end of a corridor rather than wall to wall.
    public float CorridorChance;
    public IntRange CorridorLength = new IntRange(3, 6);
    // Positive values pull the room toward hosts deep in the complex, negative toward the gate.
    public float DepthBias;
    // Fewest doors between the approach and this room.
    public int MinDepth;
}

public sealed class LairPlanParams
{
    public int Width = 150;
    public int Height = 150;
    // Cells kept clear between any room interior and the plan's edge.
    public int Margin = 7;
    // Builds the stairwell hall, the river cavern and the causeway in front of the gate. Off
    // for a complex whose approach the host map supplies (the surface variant).
    public bool UndergroundApproach = true;
    // Whole-plan retries when a plan could not place every requested room.
    public int Attempts = 4;
    // Chance that a corridor is three cells wide rather than one.
    public float WideCorridorChance = 0.7f;
    // Chance, per pair of rooms that share a wall without a way through, of an extra door.
    public float LoopChance = 0.35f;
    // Chance per exposed wall cell that a hewn room's outline bulges into the rock there.
    public float HewnBulgeChance = 0.3f;
    public int ForecourtDepth = 4;
    public int ForecourtWidth = 21;

    public RoomRequest EntranceHall;
    public RoomRequest Forecourt;
    public RoomRequest Vestibule;
    public RoomRequest Processional;
    public RoomRequest Sanctum;
    public RoomRequest Corridor;
    // Rooms to add around the spine, most important first.
    public List<RoomRequest> Program = new List<RoomRequest>();
}
