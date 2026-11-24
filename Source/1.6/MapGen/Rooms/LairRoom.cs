using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SanguophageLair;

// A room worker's handle on one stamped room: where it is, which way it faces, and the tools
// to furnish it. Workers never touch the Map directly for placement; going through here is
// what keeps doorways clear, footprints inside the room and every lair in one style.
//
// The local frame. A room is described as its visitor sees it on walking in: "forward" runs
// from the entrance toward the far wall, "right" is the visitor's right hand, and the origin
// is the room's centre cell (rooms have odd sizes, so there is one). At(right, forward) turns
// local coordinates into a map cell and Turn(rot) a local facing into a map one, with local
// North meaning forward. A worker written in this frame furnishes the room correctly whichever
// way the planner turned it. A room that is the mirror image of a partner across the lair's
// axis has its right hand flipped, so the pair comes out as true mirror images.
public sealed class LairRoom
{
    private readonly HashSet<IntVec3> doorways = new HashSet<IntVec3>();
    private readonly List<Thing> spawned = new List<Thing>();
    private readonly IntVec3 forward;
    private readonly IntVec3 right;
    private readonly Rot4 rightDir;
    private Dictionary<IntVec3, int> insets;

    public LairSite Site { get; }

    public PlanRoom Plan { get; }

    public LairRoomDef Def => (LairRoomDef)Plan.Request.Source;

    public Map Map => Site.Map;

    public LairStyle Style => Site.Style;

    public IntVec3 Center => Plan.Center;

    // The map direction a visitor faces on walking in.
    public Rot4 Forward { get; }

    // Cells from the centre to the side walls and to the entrance/far walls of the room's
    // regular core. At(HalfAcross, 0) is the last floor cell before the right-hand wall.
    public int HalfAcross { get; }

    public int HalfDeep { get; }

    public IReadOnlyList<IntVec3> Cells => Plan.Cells;

    // Everything this room has spawned so far, oldest first.
    public IReadOnlyList<Thing> Spawned => spawned;

    public LairRoom(LairSite site, PlanRoom plan)
    {
        Site = site;
        Plan = plan;
        Forward = plan.EntrySide.Opposite;
        rightDir = plan.MirrorOf >= 0 ? Forward.Rotated(RotationDirection.Counterclockwise) : Forward.Rotated(RotationDirection.Clockwise);
        forward = Forward.FacingCell;
        right = rightDir.FacingCell;
        HalfAcross = (Forward.IsHorizontal ? plan.Bounds.Height : plan.Bounds.Width) / 2;
        HalfDeep = (Forward.IsHorizontal ? plan.Bounds.Width : plan.Bounds.Height) / 2;

        // The two cells inside each opening stay clear, so a door never opens onto furniture.
        foreach (PlanConnection connection in site.Plan.ConnectionsOf(plan))
        {
            IntVec3 inward = connection.To == plan.Id ? connection.Dir.FacingCell : connection.Dir.Opposite.FacingCell;
            foreach (IntVec3 c in connection.Cells)
            {
                doorways.Add(c + inward);
                doorways.Add(c + inward + inward);
            }
        }
    }

    // ----- Frame -----

    public IntVec3 At(int rightward, int forwardward)
    {
        return Center + (right * rightward) + (forward * forwardward);
    }

    // A local facing as a map one: local North looks forward, local East to the right.
    public Rot4 Turn(Rot4 local)
    {
        if (local == Rot4.North)
        {
            return Forward;
        }
        if (local == Rot4.South)
        {
            return Forward.Opposite;
        }
        return local == Rot4.East ? rightDir : rightDir.Opposite;
    }

    public void Local(IntVec3 c, out int rightward, out int forwardward)
    {
        IntVec3 d = c - Center;
        rightward = (d.x * right.x) + (d.z * right.z);
        forwardward = (d.x * forward.x) + (d.z * forward.z);
    }

    // ----- Queries -----

    public bool Contains(IntVec3 c)
    {
        return Site.Plan.RoomIdAt(c) == Plan.Id;
    }

    // One of the cells kept clear inside a door or arch.
    public bool IsDoorway(IntVec3 c)
    {
        return doorways.Contains(c);
    }

    // Floor of this room with nothing standing on it and not reserved for a doorway.
    public bool IsFree(IntVec3 c)
    {
        if (!Contains(c) || doorways.Contains(c))
        {
            return false;
        }
        List<Thing> things = c.GetThingList(Map);
        for (int i = 0; i < things.Count; i++)
        {
            ThingCategory category = things[i].def.category;
            if (category == ThingCategory.Building || category == ThingCategory.Item || category == ThingCategory.Pawn)
            {
                return false;
            }
        }
        return true;
    }

    public bool IsFree(int rightward, int forwardward)
    {
        return IsFree(At(rightward, forwardward));
    }

    // Whether the cell is wall (built or hewn) in the plan. Doors and arches are not.
    public bool IsWall(IntVec3 c)
    {
        return Site.Plan.InBounds(c) && LairPlan.IsWall(Site.Plan[c]);
    }

    // How many cells in from the room's outline a cell is: 0 for floor touching a wall (corner
    // to corner counts), 1 for the next ring in, and so on. Rings of equal inset follow the
    // room's own shape, stepped corners included, which is what concentric carpets and
    // "against the wall" placement want. -1 outside the room.
    public int Inset(IntVec3 c)
    {
        if (insets == null)
        {
            insets = new Dictionary<IntVec3, int>(Plan.Cells.Count);
            var frontier = new Queue<IntVec3>();
            foreach (IntVec3 cell in Plan.Cells)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (!Contains(cell + GenAdj.AdjacentCells[i]))
                    {
                        insets[cell] = 0;
                        frontier.Enqueue(cell);
                        break;
                    }
                }
            }
            while (frontier.Count > 0)
            {
                IntVec3 cell = frontier.Dequeue();
                for (int i = 0; i < 8; i++)
                {
                    IntVec3 n = cell + GenAdj.AdjacentCells[i];
                    if (Contains(n) && !insets.ContainsKey(n))
                    {
                        insets[n] = insets[cell] + 1;
                        frontier.Enqueue(n);
                    }
                }
            }
        }
        return insets.TryGetValue(c, out int inset) ? inset : -1;
    }

    // Free floor cells with a wall directly beside them, each with the map direction of that
    // wall. A cell in a stepped corner comes back once per adjoining wall.
    public IEnumerable<KeyValuePair<IntVec3, Rot4>> WallSideCells()
    {
        foreach (IntVec3 c in Plan.Cells)
        {
            if (!IsFree(c))
            {
                continue;
            }
            for (int i = 0; i < 4; i++)
            {
                var dir = new Rot4(i);
                if (IsWall(c + dir.FacingCell))
                {
                    yield return new KeyValuePair<IntVec3, Rot4>(c, dir);
                }
            }
        }
    }

    // ----- Spawning -----

    // Spawns a building or item at a map cell with a map rotation, in the lair's style, if its
    // whole footprint is free floor of this room. Returns null and spawns nothing otherwise,
    // so a worker can attempt its ideal layout and let tight rooms drop pieces.
    //  stuff: null takes the lair's stone blocks where the def accepts stone, else the def's
    //         default stuff.
    //  paint: applied to paintable buildings only.
    public Thing TrySpawn(ThingDef def, IntVec3 c, Rot4 rot, ThingDef stuff = null, ColorDef paint = null, QualityCategory? quality = null)
    {
        if (def == null)
        {
            return null;
        }
        if (!def.rotatable)
        {
            rot = Rot4.North;
        }
        foreach (IntVec3 cell in GenAdj.OccupiedRect(c, rot, def.size))
        {
            if (!IsFree(cell))
            {
                return null;
            }
        }
        Thing thing = Make(def, stuff, paint, quality);
        GenSpawn.Spawn(thing, c, Map, rot);
        spawned.Add(thing);
        return thing;
    }

    // TrySpawn in the local frame.
    public Thing TrySpawn(ThingDef def, int rightward, int forwardward, Rot4 localRot, ThingDef stuff = null, ColorDef paint = null, QualityCategory? quality = null)
    {
        return TrySpawn(def, At(rightward, forwardward), Turn(localRot), stuff, paint, quality);
    }

    // Spawns a piece so that its footprint is exactly the rectangle between two local cells,
    // facing localRot. This is the way to place anything more than one cell across: where a
    // thing's centre cell falls depends on its size and rotation, its footprint does not.
    // Null (and nothing spawned) when the def cannot cover that rectangle in that rotation.
    public Thing TrySpawnOver(ThingDef def, int right1, int forward1, int right2, int forward2, Rot4 localRot, ThingDef stuff = null, ColorDef paint = null, QualityCategory? quality = null)
    {
        if (def == null)
        {
            return null;
        }
        CellRect footprint = CellRect.FromLimits(At(right1, forward1), At(right2, forward2));
        Rot4 rot = def.rotatable ? Turn(localRot) : Rot4.North;
        foreach (IntVec3 c in footprint)
        {
            if (GenAdj.OccupiedRect(c, rot, def.size) == footprint)
            {
                return TrySpawn(def, c, rot, stuff, paint, quality);
            }
        }
        return null;
    }

    // The same piece on both sides of the room's centre line, each facing as the mirror image
    // of the other. A piece on the centre line itself is spawned once.
    public void TrySpawnPair(ThingDef def, int rightward, int forwardward, Rot4 localRot, ThingDef stuff = null, ColorDef paint = null, QualityCategory? quality = null)
    {
        TrySpawn(def, rightward, forwardward, localRot, stuff, paint, quality);
        if (rightward != 0)
        {
            Rot4 mirrored = localRot.IsHorizontal ? localRot.Opposite : localRot;
            TrySpawn(def, -rightward, forwardward, mirrored, stuff, paint, quality);
        }
    }

    // Builds a thing in the lair's style without placing it.
    public Thing Make(ThingDef def, ThingDef stuff = null, ColorDef paint = null, QualityCategory? quality = null)
    {
        ThingDef madeOf = null;
        if (def.MadeFromStuff)
        {
            madeOf = stuff ?? (Style.Blocks.stuffProps.CanMake(def) ? Style.Blocks : GenStuff.DefaultStuffFor(def));
        }
        Thing thing = ThingMaker.MakeThing(def, madeOf);
        CompQuality compQuality = thing.TryGetComp<CompQuality>();
        if (compQuality != null)
        {
            compQuality.SetQuality(quality ?? QualityUtility.GenerateQualityBaseGen(), ArtGenerationContext.Outsider);
        }
        CompArt compArt = thing.TryGetComp<CompArt>();
        if (compArt != null && !compArt.Active)
        {
            compArt.InitializeArt(ArtGenerationContext.Outsider);
        }
        CompRefuelable refuelable = thing.TryGetComp<CompRefuelable>();
        if (refuelable != null)
        {
            refuelable.Refuel(refuelable.Props.fuelCapacity - refuelable.Fuel);
        }
        if (paint != null && thing is Building building && def.building != null && def.building.paintable)
        {
            building.ChangePaint(paint);
        }
        return thing;
    }

    // A stack of items on a free cell (a shelf cell counts: pass onShelf to allow a cell that
    // already holds a storage building). Forbidden, as loot on a map that is not the
    // player's is everywhere in vanilla.
    public Thing TrySpawnItem(ThingDef def, IntVec3 c, int count = 1, ThingDef stuff = null, bool onShelf = false)
    {
        if (def == null || !Contains(c) || (!onShelf && !IsFree(c)))
        {
            return null;
        }
        Thing thing = ThingMaker.MakeThing(def, def.MadeFromStuff ? stuff ?? GenStuff.DefaultStuffFor(def) : null);
        thing.stackCount = Math.Min(Math.Max(1, count), def.stackLimit);
        CompQuality compQuality = thing.TryGetComp<CompQuality>();
        if (compQuality != null)
        {
            compQuality.SetQuality(QualityUtility.GenerateQualityBaseGen(), ArtGenerationContext.Outsider);
        }
        GenSpawn.Spawn(thing, c, Map);
        thing.SetForbidden(true, warnOnFail: false);
        spawned.Add(thing);
        return thing;
    }

    // ----- Inhabitants -----
    // Each returns null when the pawn could not be made or placed; workers carry on without.

    // An awake defender standing guard near a cell of this room.
    public Pawn TrySpawnGuard(LairPawnTier tier, IntVec3 near)
    {
        return LairPawns.SpawnGuard(this, tier, near);
    }

    // A defender asleep in one of this room's ordinary beds until the lair is alerted.
    public Pawn TrySpawnSleeper(LairPawnTier tier, Thing bed)
    {
        return LairPawns.SpawnSleeper(this, tier, bed);
    }

    // A sanguophage deathresting in a casket of this room, bound to the deathrest buildings
    // given (the casket itself need not be listed).
    public Pawn TrySpawnDeathrester(LairPawnTier tier, Thing casket, IEnumerable<Thing> boundBuildings)
    {
        return LairPawns.SpawnDeathrester(this, tier, casket, boundBuildings);
    }

    // A captive kept for blood, in the given bed (may be null) or standing near the cell.
    public Pawn TrySpawnPrisoner(Thing bed, IntVec3 near)
    {
        return LairPawns.SpawnPrisoner(this, bed, near);
    }

    // ----- Floors -----

    // Changes the floor of every cell of the room the test picks, given the cell's local
    // coordinates. A null terrain does nothing.
    public void Floor(TerrainDef terrain, Func<int, int, bool> where)
    {
        if (terrain == null)
        {
            return;
        }
        foreach (IntVec3 c in Plan.Cells)
        {
            Local(c, out int rightward, out int forwardward);
            if (where(rightward, forwardward))
            {
                Map.terrainGrid.SetTerrain(c, terrain);
            }
        }
    }

    // Changes the floor of the band of cells whose Inset lies in the given range: a ring that
    // follows the room's outline.
    public void FloorRing(TerrainDef terrain, int insetFrom, int insetTo)
    {
        if (terrain == null)
        {
            return;
        }
        foreach (IntVec3 c in Plan.Cells)
        {
            int inset = Inset(c);
            if (inset >= insetFrom && inset <= insetTo)
            {
                Map.terrainGrid.SetTerrain(c, terrain);
            }
        }
    }

    // How far a local cell is from the centre, measured in the room's own shape: straight-line
    // distance in a round room, the larger coordinate in a rectangular one, and in an octagon
    // the measure whose level lines are octagons parallel to the walls. Cells of equal Reach
    // therefore trace the room's outline at any distance from the centre.
    public float Reach(int rightward, int forwardward)
    {
        int r = Math.Abs(rightward);
        int f = Math.Abs(forwardward);
        switch (Plan.Shape)
        {
            case RoomShapeKind.Round:
                return (float)Math.Sqrt((r * r) + (f * f));
            case RoomShapeKind.Octagon:
                // The planner cuts octagon corners by about 0.28 of the width, which puts the
                // diagonal walls at this multiple of the distance to the straight ones.
                return Math.Max(Math.Max(r, f), (r + f) / 1.42f);
            default:
                return Math.Max(r, f);
        }
    }

    // Changes the floor of the band of cells whose Reach lies in [from, to): a ring concentric
    // with the room and parallel to its walls, at a fixed distance from the centre. Use this
    // for a ring around a centrepiece and FloorRing for a border measured from the walls.
    public void FloorBand(TerrainDef terrain, float from, float to)
    {
        Floor(terrain, (rightward, forwardward) =>
        {
            float reach = Reach(rightward, forwardward);
            return reach >= from && reach < to;
        });
    }

    // ----- Light -----

    public Thing TryTorch(IntVec3 c)
    {
        return TrySpawn(ThingDefOf.SanguphageMeetingTorch, c, Rot4.North);
    }

    // Blood torches standing against the walls, mirrored about the room's centre line and no
    // closer together than the spacing. Cells in front of doorways are never used.
    public void TorchesAlongWalls(int spacing)
    {
        var placed = new List<IntVec3>();
        foreach (Thing thing in spawned)
        {
            if (thing.def == ThingDefOf.SanguphageMeetingTorch)
            {
                placed.Add(thing.Position);
            }
        }
        var candidates = new List<IntVec3>();
        foreach (KeyValuePair<IntVec3, Rot4> side in WallSideCells())
        {
            Local(side.Key, out int rightward, out _);
            if (rightward >= 0 && !candidates.Contains(side.Key))
            {
                candidates.Add(side.Key);
            }
        }
        // Far wall first, then outward from the centre line: the far wall's torches frame
        // whatever the room is about.
        candidates.Sort((a, b) =>
        {
            Local(a, out int ar, out int af);
            Local(b, out int br, out int bf);
            return af != bf ? bf.CompareTo(af) : ar.CompareTo(br);
        });
        foreach (IntVec3 c in candidates)
        {
            Local(c, out int rightward, out int forwardward);
            IntVec3 twin = At(-rightward, forwardward);
            if (!FarFrom(c, placed, spacing) || !FarFrom(twin, placed, spacing))
            {
                continue;
            }
            // A torch on the centre line would stand in the middle of the far wall, where the
            // room's focal piece goes; the pair either side of it reads better.
            if (rightward == 0 || !IsFree(twin) || TryTorch(c) == null)
            {
                continue;
            }
            placed.Add(c);
            if (TryTorch(twin) != null)
            {
                placed.Add(twin);
            }
        }
    }

    private static bool FarFrom(IntVec3 c, List<IntVec3> others, int spacing)
    {
        for (int i = 0; i < others.Count; i++)
        {
            if (Math.Abs(others[i].x - c.x) < spacing && Math.Abs(others[i].z - c.z) < spacing)
            {
                return false;
            }
        }
        return true;
    }

    // ----- Safety nets, run by the contents step after every worker -----

    // Every doorway of the room must be able to reach every other across its floor. If a
    // worker's furniture cut one off, the most recently placed pieces are removed until the
    // room is walkable again. Returns how many pieces had to go.
    public int EnsureWalkable()
    {
        int removed = 0;
        while (!DoorwaysConnected())
        {
            int last = spawned.FindLastIndex(t => !t.Destroyed && t.def.passability != Traversability.Standable);
            if (last < 0)
            {
                break;
            }
            spawned[last].Destroy();
            spawned.RemoveAt(last);
            removed++;
        }
        return removed;
    }

    private bool DoorwaysConnected()
    {
        IntVec3 start = IntVec3.Invalid;
        foreach (IntVec3 c in doorways)
        {
            if (Contains(c) && c.Walkable(Map))
            {
                start = c;
                break;
            }
        }
        if (!start.IsValid)
        {
            return true;
        }
        var reached = new HashSet<IntVec3> { start };
        var frontier = new Queue<IntVec3>();
        frontier.Enqueue(start);
        while (frontier.Count > 0)
        {
            IntVec3 c = frontier.Dequeue();
            for (int i = 0; i < 4; i++)
            {
                IntVec3 n = c + GenAdj.CardinalDirections[i];
                if (Contains(n) && n.Walkable(Map) && reached.Add(n))
                {
                    frontier.Enqueue(n);
                }
            }
        }
        foreach (IntVec3 c in doorways)
        {
            if (Contains(c) && !reached.Contains(c))
            {
                return false;
            }
        }
        return true;
    }

    // Thick rock roof falls where it is too far from anything that holds it up, the moment a
    // nearby wall is lost. While any cell of the room is out of reach of a wall or column, a
    // column goes on the free cell nearest the worst-placed one (and on its mirror image, to
    // keep the room symmetric). Returns how many columns were added; a well-designed worker
    // leaves this nothing to do.
    public int EnsureRoofSupport()
    {
        int added = 0;
        for (int guard = 0; guard < 16; guard++)
        {
            IntVec3 worst = IntVec3.Invalid;
            int worstDist = int.MaxValue;
            foreach (IntVec3 c in Plan.Cells)
            {
                int dist = (c - Center).LengthHorizontalSquared;
                if (dist < worstDist && !RoofCollapseUtility.WithinRangeOfRoofHolder(c, Map))
                {
                    worst = c;
                    worstDist = dist;
                }
            }
            if (!worst.IsValid)
            {
                break;
            }
            IntVec3 site = IntVec3.Invalid;
            int siteDist = int.MaxValue;
            foreach (IntVec3 c in Plan.Cells)
            {
                int dist = (c - worst).LengthHorizontalSquared;
                if (dist < siteDist && IsFree(c))
                {
                    site = c;
                    siteDist = dist;
                }
            }
            if (!site.IsValid || siteDist > 36)
            {
                break;
            }
            Local(site, out int rightward, out int forwardward);
            if (TrySpawn(ThingDefOf.Column, site, Rot4.North, paint: Style.WallPaint) != null)
            {
                added++;
            }
            if (rightward != 0 && TrySpawn(ThingDefOf.Column, At(-rightward, forwardward), Rot4.North, paint: Style.WallPaint) != null)
            {
                added++;
            }
        }
        return added;
    }
}
