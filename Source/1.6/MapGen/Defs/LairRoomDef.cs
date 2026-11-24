using System;
using System.Collections.Generic;
using Verse;

namespace SanguophageLair;

// What a room's floor is made of. Resolved against the lair's own stone by LairStyle, so one
// def serves a granite lair and a marble one.
public enum LairFloor
{
    // Cut stone tile: the formal rooms.
    Tile,
    // Flagstone: the working rooms.
    Flagstone,
    // The rock itself, smoothed.
    Smooth,
    // Whatever the map has there (rough stone under the removed rock).
    Natural,
}

// One kind of lair room: how the planner may shape and site it, and which worker furnishes it.
// The geometry fields are copied into a RoomRequest for the planner (see RoomRequest.cs for
// their meaning; the names match). Rooms carry no label or description on purpose: nothing
// shows them to the player, and a def without strings can sit in the main tree behind a
// MayRequire while depending on optional content.
public class LairRoomDef : Def
{
    public PlanRole role = PlanRole.Room;
    public Type workerClass = typeof(LairRoomWorker);
    public List<ShapeOption> shapes = new List<ShapeOption>();
    public PlanWalls walls = PlanWalls.Built;
    public PlanAnchor anchor = PlanAnchor.Any;
    public bool paired;
    public PlanOpening entry = PlanOpening.Door;
    public int maxChildren = 2;
    public bool allowLoops = true;
    public float corridorChance;
    public IntRange corridorLength = new IntRange(3, 6);
    public float depthBias;
    public int minDepth;
    public LairFloor floor = LairFloor.Tile;

    [Unsaved]
    private LairRoomWorker workerInt;

    public LairRoomWorker Worker
    {
        get
        {
            if (workerInt == null)
            {
                workerInt = (LairRoomWorker)Activator.CreateInstance(workerClass);
                workerInt.def = this;
            }
            return workerInt;
        }
    }

    public RoomRequest ToRequest()
    {
        return new RoomRequest
        {
            Key = defName,
            Source = this,
            Role = role,
            Shapes = shapes,
            Walls = walls,
            Anchor = anchor,
            Paired = paired,
            Entry = entry,
            MaxChildren = maxChildren,
            AllowLoops = allowLoops,
            CorridorChance = corridorChance,
            CorridorLength = corridorLength,
            DepthBias = depthBias,
            MinDepth = minDepth,
        };
    }

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (string error in base.ConfigErrors())
        {
            yield return error;
        }
        // The forecourt and corridors are sized by the planner, every other room by its def.
        if (shapes.Count == 0 && role != PlanRole.Forecourt && role != PlanRole.Corridor)
        {
            yield return "has no shapes";
        }
        if (workerClass == null || !typeof(LairRoomWorker).IsAssignableFrom(workerClass))
        {
            yield return "workerClass must derive from LairRoomWorker";
        }
    }
}
