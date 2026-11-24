using System;
using RimWorld;
using Verse;

namespace SanguophageLair;

// The gallery: the vanity of immortals, a long narrow hall of sculpture. Art is the room's
// loot, but a modest hoard: a few thousand silver for the whole hall.
//
// Layout in the room's local frame (right -3..3, forward -deep..deep):
//   right 0, +-1   scarlet carpet runner the length of the hall, the viewing aisle
//   right +-3      the sculpture bays against the walls, in mirrored pairs, one cell between
//                  bays, small and large by turns from the door inward, as many as fit before
//                  the far wall
//   gap cells      a blood torch after every small bay
//   far wall       one grand sculpture (2x2, right -1..0, so it sits off the centre line by
//                  half a cell: an even footprint cannot straddle an odd room's middle),
//                  painted in the layout's strongest accent, at the end of the runner with a
//                  torch either side of it
// A short hall simply ends the sequence earlier.
//
// Value. Market value of a stone building here is (cost units x block value + work x 0.0036)
// x quality factor, the stone's own value factor not applying (StatWorker_MarketValue). Blocks
// are worth 0.9 each, and the work is the def's WorkToMake times the blocks' WorkToMake factor
// (1.3 for granite, limestone and slate; 1.1 sandstone; 1.15 marble). At the factor 1.3:
//   small sculpture    50 blocks, 18000 work     45 + 84.24 = 129.24
//   large sculpture   100 blocks, 30000 work     90 + 140.4 = 230.40
//   grand sculpture   400 blocks, 105000 work   360 + 491.4 = 851.40
// The largest hall (15 deep) holds six bays, three pairs of each size, and the grand piece:
//   bays   6 x 129.24 + 6 x 230.40 = 2157.84 at normal quality, 2697.3 at good (x1.25)
//   grand  851.40 x 1.5 (excellent) = 1277.1
//   total  3435 to 3974 in granite; sandstone's lighter work gives 3114 to 3602.
// Bays are normal or good and the grand piece excellent, so quality never moves the total out
// of that band. Shorter halls hold fewer bays and are worth less.
public class LairRoomWorker_Gallery : LairRoomWorker
{
    public override void Fill(LairRoom room)
    {
        LairStyle style = room.Style;
        int wall = room.HalfAcross;
        int deep = room.HalfDeep;
        room.Floor(style.Carpet(0), (across, along) => Math.Abs(across) <= 1);

        int forward = -deep + 2;
        int limit = deep - 2;
        for (int i = 0; forward <= limit; i++, forward += 2)
        {
            QualityCategory quality = Rand.Chance(0.5f) ? QualityCategory.Normal : QualityCategory.Good;
            if (i % 2 == 0)
            {
                room.TrySpawnPair(SL_LairDefOf.SculptureSmall, wall, forward, Rot4.North, paint: style.Paint(2), quality: quality);
                room.TryTorch(room.At(wall, forward + 1));
                room.TryTorch(room.At(-wall, forward + 1));
            }
            else
            {
                room.TrySpawnPair(SL_LairDefOf.SculptureLarge, wall, forward, Rot4.North, paint: style.Paint(1), quality: quality);
            }
        }

        room.TrySpawnOver(SL_LairDefOf.SculptureGrand, -1, deep - 1, 0, deep, Rot4.North, paint: style.Paint(0), quality: QualityCategory.Excellent);
        room.TryTorch(room.At(2, deep));
        room.TryTorch(room.At(-2, deep));
    }
}
