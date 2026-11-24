using System;
using Verse;

namespace SanguophageLair;

public enum RoomShapeKind
{
    Rect,
    Octagon,
    Round,
}

// The interior footprint of one lair room: an immutable cell mask inside a bounding box, in the
// room's own coordinates (x right, z up, origin at the box's min corner). Walls are not part of
// a shape; the plan derives them as the ring of cells around the interior (LairPlan).
//
// Every shape here is convex and symmetric about both of its axes. The planner relies on both:
// symmetry lets a room be mirrored across the lair's axis by moving its origin alone, and
// convexity makes each side's outermost row a single contiguous run, which is where doors go
// (FaceRun). Stepped diagonals rather than true curves are deliberate: walls only link to their
// four orthogonal neighbours, so a stepped outline is the only one that draws as a continuous
// wall, and it is the outline of the Biotech sanguophage base the lair is modelled on.
public sealed class RoomShape
{
    private readonly bool[] mask;

    public RoomShapeKind Kind { get; }

    public int Width { get; }

    public int Height { get; }

    public int CellCount { get; }

    private RoomShape(RoomShapeKind kind, int width, int height, Func<int, int, bool> inside)
    {
        Kind = kind;
        Width = width;
        Height = height;
        mask = new bool[width * height];
        int count = 0;
        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                if (inside(x, z))
                {
                    mask[(z * width) + x] = true;
                    count++;
                }
            }
        }
        CellCount = count;
    }

    public static RoomShape Rect(int width, int height)
    {
        return new RoomShape(RoomShapeKind.Rect, width, height, (_, _) => true);
    }

    // A rectangle with each corner cut back in one-cell steps: a cell is inside when its
    // distances to the nearest vertical and horizontal edges sum to at least the chamfer. The
    // chamfer is clamped so every side keeps a flat run of at least three cells for a doorway.
    public static RoomShape Octagon(int width, int height, int chamfer)
    {
        int maxChamfer = Math.Max(0, (Math.Min(width, height) - 3) / 2);
        int c = Math.Min(Math.Max(chamfer, 0), maxChamfer);
        return new RoomShape(RoomShapeKind.Octagon, width, height, (x, z) =>
            Math.Min(x, width - 1 - x) + Math.Min(z, height - 1 - z) >= c);
    }

    // A digital disc. Measuring from the box centre with radius diameter/2 leaves a flat run on
    // each cardinal side (three cells at diameter 7, five from 9 up), which a doorway needs.
    public static RoomShape Round(int diameter)
    {
        float centre = (diameter - 1) / 2f;
        float radiusSq = diameter * diameter / 4f;
        return new RoomShape(RoomShapeKind.Round, diameter, diameter, (x, z) =>
            ((x - centre) * (x - centre)) + ((z - centre) * (z - centre)) <= radiusSq);
    }

    public bool Contains(int x, int z)
    {
        return x >= 0 && z >= 0 && x < Width && z < Height && mask[(z * Width) + x];
    }

    // The same shape turned a quarter turn. All shapes are symmetric about both axes, so the
    // direction of the turn does not matter and two turns give the original back.
    public RoomShape Transposed()
    {
        return new RoomShape(Kind, Height, Width, (x, z) => mask[(x * Width) + z]);
    }

    // The contiguous run of interior cells on the shape's outermost row or column facing dir,
    // as an offset range along that side's own axis (x for North/South, z for East/West).
    public void FaceRun(Rot4 dir, out int start, out int length)
    {
        bool horizontal = dir == Rot4.North || dir == Rot4.South;
        int span = horizontal ? Width : Height;
        int fixedCoord = dir == Rot4.North ? Height - 1 : dir == Rot4.East ? Width - 1 : 0;
        start = -1;
        length = 0;
        for (int i = 0; i < span; i++)
        {
            bool inside = horizontal ? Contains(i, fixedCoord) : Contains(fixedCoord, i);
            if (inside)
            {
                if (start < 0)
                {
                    start = i;
                }
                length++;
            }
        }
    }
}
