using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace SanguophageLords;

// Turns a LairPlan into the map: one pass over the plan's cells, each cell kind mapped to what
// stands there. The plan must already be in map coordinates (same size as the map).
//
// Engine facts this relies on (decompile-verified, 1.6):
// - An underground map is roofed RoofRockThick on every cell before any genstep runs, so
//   nothing here touches roofs; carving a cell is destroying its rock edifice.
// - Building.DeSpawn only reports a lost roof holder while the game is Playing, so carving
//   during generation cannot trigger a collapse. In play, thick roof further than
//   RoofCollapseUtility.RoofMaxSupportDistance from a holder falls when a holder near it goes,
//   which is why the planner leaves rock pillars in the cavern and room workers put columns
//   in wide rooms.
// - Moving-water terrain animates from map.waterInfo.riverFlowMap alone (two floats per cell
//   at index (x * Size.z + z) * 2, read lazily when the map first renders); no river mutator
//   or RiverMaker is needed for a hand-laid river.
// - TerrainGrid.SetTerrain clears the cell's paint, so floor colour is always set after it.
public static class LairStamper
{
    // Flow vectors are per-cell steps along the river; this is the length vanilla's river
    // worker ends up with and reads as a steady current.
    private const float FlowSpeed = 1.6f;

    public static void Stamp(LairSite site)
    {
        Map map = site.Map;
        LairPlan plan = site.Plan;
        LairStyle style = site.Style;
        TerrainDef shallow = TerrainDefOf.WaterMovingShallow;
        TerrainDef deep = TerrainDefOf.WaterMovingChestDeep;
        for (int z = 0; z < plan.Height; z++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                var c = new IntVec3(x, 0, z);
                switch (plan[x, z])
                {
                    case PlanCell.Floor:
                        Clear(map, c, everything: true);
                        PlanRoom room = plan.Rooms[plan.RoomIdAt(x, z)];
                        SetFloor(map, c, style.Floor(((LairRoomDef)room.Request.Source).floor));
                        break;
                    case PlanCell.Arch:
                    case PlanCell.Bridge:
                        Clear(map, c, everything: true);
                        SetFloor(map, c, style.Tile);
                        break;
                    case PlanCell.Wall:
                        Clear(map, c, everything: true);
                        SpawnStonework(map, c, ThingDefOf.Wall, style.Blocks, style.WallPaint);
                        break;
                    case PlanCell.Door:
                        Clear(map, c, everything: true);
                        SetFloor(map, c, style.Tile);
                        SpawnStonework(map, c, ThingDefOf.Door, style.Blocks, style.DoorPaint);
                        break;
                    case PlanCell.HewnWall:
                        // Living rock: only cells a cave had already opened need filling.
                        if (c.GetEdifice(map) == null)
                        {
                            Clear(map, c, everything: true);
                            GenSpawn.Spawn(ThingMaker.MakeThing(style.Rock), c, map);
                        }
                        break;
                    case PlanCell.Cave:
                        // Keep loose chunks and the natural floor; only the rock goes.
                        Clear(map, c, everything: false);
                        break;
                    case PlanCell.ShallowWater:
                        Clear(map, c, everything: true);
                        map.terrainGrid.SetTerrain(c, shallow);
                        break;
                    case PlanCell.DeepWater:
                        Clear(map, c, everything: true);
                        map.terrainGrid.SetTerrain(c, deep);
                        break;
                }
            }
        }
        if (plan.RiverFlow.IsValid)
        {
            WriteRiverFlow(map, plan);
        }
    }

    private static void Clear(Map map, IntVec3 c, bool everything)
    {
        List<Thing> things = c.GetThingList(map);
        for (int i = things.Count - 1; i >= 0; i--)
        {
            Thing thing = things[i];
            if (thing.def.destroyable && (everything || thing.def.category == ThingCategory.Building))
            {
                thing.Destroy();
            }
        }
    }

    private static void SetFloor(Map map, IntVec3 c, TerrainDef terrain)
    {
        if (terrain != null)
        {
            map.terrainGrid.SetTerrain(c, terrain);
        }
    }

    private static void SpawnStonework(Map map, IntVec3 c, ThingDef def, ThingDef blocks, ColorDef paint)
    {
        Thing thing = GenSpawn.Spawn(ThingMaker.MakeThing(def, blocks), c, map);
        if (paint != null && thing is Building building)
        {
            building.ChangePaint(paint);
        }
    }

    // The river runs along one map axis and wanders across the other. Each water cell's flow
    // follows the local run of the channel: the step from this line's mid-channel to the one a
    // few lines downstream.
    private static void WriteRiverFlow(Map map, LairPlan plan)
    {
        bool alongX = plan.RiverFlow.IsHorizontal;
        int length = alongX ? plan.Width : plan.Height;
        int breadth = alongX ? plan.Height : plan.Width;
        int sign = plan.RiverFlow == Rot4.East || plan.RiverFlow == Rot4.North ? 1 : -1;

        // Mid-channel position of the water on each line across the river; -1 where a line
        // has no water at all.
        var mid = new float[length];
        for (int i = 0; i < length; i++)
        {
            int sum = 0;
            int count = 0;
            for (int j = 0; j < breadth; j++)
            {
                if (IsWater(alongX ? plan[i, j] : plan[j, i]))
                {
                    sum += j;
                    count++;
                }
            }
            mid[i] = count == 0 ? -1f : (float)sum / count;
        }

        var flow = new List<float>(new float[map.Size.x * map.Size.z * 2]);
        for (int i = 0; i < length; i++)
        {
            int ahead = Mathf.Clamp(i + (sign * 3), 0, length - 1);
            float drift = mid[i] < 0f || mid[ahead] < 0f ? 0f : mid[ahead] - mid[i];
            Vector2 step = (alongX ? new Vector2(sign * 3f, drift) : new Vector2(drift, sign * 3f)).normalized * FlowSpeed;
            for (int j = 0; j < breadth; j++)
            {
                int x = alongX ? i : j;
                int z = alongX ? j : i;
                if (IsWater(plan[x, z]))
                {
                    int index = ((x * map.Size.z) + z) * 2;
                    flow[index] = step.x;
                    flow[index + 1] = step.y;
                }
            }
        }
        map.waterInfo.riverFlowMap = flow;
    }

    private static bool IsWater(PlanCell cell)
    {
        return cell == PlanCell.ShallowWater || cell == PlanCell.DeepWater;
    }
}
