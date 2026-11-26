using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SanguophageLords;

// The materials one lair is built from, resolved once per generated map: its stone (and the
// blocks, floors and chunks that stone gives) plus the paints and carpets its layout def asks
// for. Everything a room worker spawns takes its look from here, so a lair reads as one piece
// of architecture whichever rock it was cut into.
public sealed class LairStyle
{
    public ThingDef Rock { get; private set; }

    public ThingDef Blocks { get; private set; }

    public TerrainDef Tile { get; private set; }

    public TerrainDef Flagstone { get; private set; }

    public TerrainDef Smooth { get; private set; }

    // The floor mining leaves behind: what a room hewn from the rock stands on.
    public TerrainDef Hewn { get; private set; }

    public ColorDef WallPaint { get; private set; }

    public ColorDef DoorPaint { get; private set; }

    private readonly List<ColorDef> furniturePaints = new List<ColorDef>();
    private readonly List<TerrainDef> carpets = new List<TerrainDef>();

    public static LairStyle Resolve(LairLayoutDef layout, ThingDef rock)
    {
        // Stone tile and flagstone are hand-written Core defs named after the five vanilla
        // rocks; a modded rock usually has neither, so each falls back to that rock's own
        // generated smooth floor and only then to granite.
        TerrainDef smooth = LairRock.SmoothFloorFor(rock);
        var style = new LairStyle
        {
            Rock = rock,
            Blocks = LairRock.BlocksFor(rock),
            Smooth = smooth ?? TerrainDefOf.Concrete,
            Tile = Terrain("Tile" + rock.defName) ?? smooth ?? Terrain("TileGranite"),
            Flagstone = Terrain("Flagstone" + rock.defName) ?? smooth ?? Terrain("FlagstoneGranite"),
            Hewn = rock.building?.leaveTerrain ?? rock.building?.naturalTerrain,
            WallPaint = layout.wallPaint,
            DoorPaint = layout.doorPaint,
        };
        style.furniturePaints.AddRange(layout.furniturePaints);
        foreach (string name in layout.carpets)
        {
            TerrainDef carpet = Terrain(name);
            if (carpet != null)
            {
                style.carpets.Add(carpet);
            }
        }
        return style;
    }

    // Null means "leave the map's own terrain".
    public TerrainDef Floor(LairFloor floor)
    {
        switch (floor)
        {
            case LairFloor.Tile:
                return Tile;
            case LairFloor.Flagstone:
                return Flagstone;
            case LairFloor.Smooth:
                return Smooth;
            default:
                return Hewn;
        }
    }

    // Furniture paint by strength: 0 is the layout's strongest accent. Indexes past the end
    // give the last entry; null when the layout paints nothing.
    public ColorDef Paint(int index)
    {
        return furniturePaints.Count == 0 ? null : furniturePaints[Math.Min(index, furniturePaints.Count - 1)];
    }

    // Carpet by brightness, same indexing as Paint. Null when none of the layout's carpet
    // terrains exist; callers then leave the floor bare.
    public TerrainDef Carpet(int index)
    {
        return carpets.Count == 0 ? null : carpets[Math.Min(index, carpets.Count - 1)];
    }

    private static TerrainDef Terrain(string defName)
    {
        return DefDatabase<TerrainDef>.GetNamedSilentFail(defName);
    }
}
