using System.Collections.Generic;
using System.Linq;
using Verse;
using Xunit;

namespace SanguophageLair.Tests;

/// <summary>
/// The pure geometry behind LairRoom's footprint placement: finding the anchor cell that gives
/// a wanted footprint, the side a user cell lies on, and the order of wall-flush anchors.
/// </summary>
public class LairRoomFootprintTests
{
    public static IEnumerable<object[]> Sizes()
    {
        foreach ((int x, int z) in new[] { (1, 1), (1, 2), (2, 1), (2, 2), (3, 3), (3, 2), (5, 2), (6, 6) })
        {
            yield return new object[] { x, z };
        }
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void AnchorReproducesTheFootprintAtEveryRotation(int sx, int sz)
    {
        var size = new IntVec2(sx, sz);
        for (int r = 0; r < 4; r++)
        {
            var rot = new Rot4(r);
            IntVec3 centre = new IntVec3(40, 0, 25);
            CellRect wanted = GenAdj.OccupiedRect(centre, rot, size);

            IntVec3 anchor = LairRoom.FootprintAnchor(wanted, rot, size);

            Assert.True(anchor.IsValid);
            Assert.Equal(wanted, GenAdj.OccupiedRect(anchor, rot, size));
            Assert.Equal(centre, anchor);
        }
    }

    [Fact]
    public void AnchorIsInvalidWhenTheFootprintDoesNotMatchTheSize()
    {
        Assert.False(LairRoom.FootprintAnchor(CellRect.FromLimits(new IntVec3(3, 0, 3), new IntVec3(5, 0, 3)), Rot4.North, new IntVec2(2, 2)).IsValid);
        Assert.False(LairRoom.FootprintAnchor(CellRect.SingleCell(new IntVec3(3, 0, 3)), Rot4.East, new IntVec2(1, 2)).IsValid);
    }

    [Fact]
    public void AnchorSwapsTheSidesOfARectangularPieceWhenRotated()
    {
        CellRect tall = CellRect.FromLimits(new IntVec3(10, 0, 10), new IntVec3(10, 0, 11));
        CellRect wide = CellRect.FromLimits(new IntVec3(10, 0, 10), new IntVec3(11, 0, 10));
        var size = new IntVec2(1, 2);

        Assert.True(LairRoom.FootprintAnchor(tall, Rot4.North, size).IsValid);
        Assert.True(LairRoom.FootprintAnchor(wide, Rot4.East, size).IsValid);
        Assert.False(LairRoom.FootprintAnchor(wide, Rot4.North, size).IsValid);
        Assert.False(LairRoom.FootprintAnchor(tall, Rot4.West, size).IsValid);
    }

    [Fact]
    public void UseCellOnSideNamesTheSideBeyondTheFootprint()
    {
        CellRect rect = CellRect.FromLimits(new IntVec3(10, 0, 10), new IntVec3(11, 0, 11));

        Assert.True(LairRoom.UseCellOnSide(rect, new IntVec3(10, 0, 12), Rot4.North));
        Assert.True(LairRoom.UseCellOnSide(rect, new IntVec3(11, 0, 9), Rot4.South));
        Assert.True(LairRoom.UseCellOnSide(rect, new IntVec3(12, 0, 10), Rot4.East));
        Assert.True(LairRoom.UseCellOnSide(rect, new IntVec3(9, 0, 11), Rot4.West));

        Assert.False(LairRoom.UseCellOnSide(rect, new IntVec3(10, 0, 12), Rot4.South));
        Assert.False(LairRoom.UseCellOnSide(rect, new IntVec3(12, 0, 12), Rot4.North));
        Assert.False(LairRoom.UseCellOnSide(rect, new IntVec3(10, 0, 11), Rot4.North));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    public void AUserCellOffsetAlongTheFacingLiesBeyondTheFootprintOnThatSide(int sx, int sz)
    {
        // An interaction offset of (0,0,n) with n one past the front edge points straight out
        // of the piece's front, which is how the draincasket (2x2, offset 2) and the like face.
        var size = new IntVec2(sx, sz);
        var offset = new IntVec3(0, 0, (sz / 2) + 1);
        for (int r = 0; r < 4; r++)
        {
            var rot = new Rot4(r);
            IntVec3 anchor = new IntVec3(30, 0, 30);
            CellRect footprint = GenAdj.OccupiedRect(anchor, rot, size);
            IntVec3 use = anchor + offset.RotatedBy(rot);

            Assert.True(LairRoom.UseCellOnSide(footprint, use, rot), "rotation " + rot + " size " + size);
        }
    }

    [Fact]
    public void WallFlushAnchorsCoverAWallCellAndAreFreeOfDuplicates()
    {
        var wallSide = Enumerable.Range(0, 5).Select(x => new IntVec3(10 + x, 0, 10)).ToList();
        var size = new IntVec2(2, 2);

        List<IntVec3> anchors = LairRoom.WallFlushAnchors(wallSide, size, new IntVec3(12, 0, 10));

        Assert.Equal(anchors.Count, anchors.Distinct().Count());
        foreach (IntVec3 anchor in anchors)
        {
            CellRect footprint = GenAdj.OccupiedRect(anchor, Rot4.North, size);
            Assert.True(wallSide.Any(footprint.Contains));
        }
    }

    [Fact]
    public void WallFlushAnchorsComeNearestFirstWithTiesByCell()
    {
        var wallSide = Enumerable.Range(0, 7).Select(x => new IntVec3(10 + x, 0, 10)).ToList();
        var size = new IntVec2(1, 1);
        IntVec3 near = new IntVec3(13, 0, 10);

        List<IntVec3> anchors = LairRoom.WallFlushAnchors(wallSide, size, near);

        Assert.Equal(near, anchors[0]);
        // The next two are the cells either side of it, lower x first.
        Assert.Equal(new IntVec3(12, 0, 10), anchors[1]);
        Assert.Equal(new IntVec3(14, 0, 10), anchors[2]);
    }

    [Fact]
    public void WallFlushAnchorsDoNotDependOnTheOrderOfTheInput()
    {
        var wallSide = new List<IntVec3>
        {
            new IntVec3(10, 0, 10), new IntVec3(11, 0, 10), new IntVec3(12, 0, 10),
            new IntVec3(9, 0, 11), new IntVec3(9, 0, 12),
        };
        var size = new IntVec2(2, 1);
        IntVec3 near = new IntVec3(11, 0, 11);

        List<IntVec3> forward = LairRoom.WallFlushAnchors(wallSide, size, near);
        wallSide.Reverse();
        List<IntVec3> backward = LairRoom.WallFlushAnchors(wallSide, size, near);

        Assert.Equal(forward, backward);
    }
}
