using System;
using RimWorld;
using Verse;

namespace SanguophageLair;

// The gallery: the vanity of immortals, a long narrow hall of sculpture. Art is the room's
// loot, so the pieces run from good to masterwork quality.
//
// Layout in the room's local frame (right -3..3, forward -deep..deep):
//   right 0, +-1   scarlet carpet runner the length of the hall, the viewing aisle
//   right +-3      the sculpture bays against the walls, in mirrored pairs, one cell between
//                  bays: small, large, grand, large, small from the door inward, as many as
//                  fit before the far wall (a grand sculpture is 2x2 and takes right 2..3)
//   gap cells      a blood torch after every other bay
//   far wall       the best piece, a masterwork sculpture painted in the layout's strongest
//                  accent, at the end of the runner with a torch either side of it
// A short hall simply ends the sequence earlier. Value: about 800 silver of material and work
// per large sculpture and 3200 per grand one at normal quality, before the quality factor.
public class LairRoomWorker_Gallery : LairRoomWorker
{
    private enum Bay
    {
        Small,
        Large,
        Grand,
    }

    private static readonly Bay[] Sequence = { Bay.Small, Bay.Large, Bay.Grand, Bay.Large, Bay.Small };

    public override void Fill(LairRoom room)
    {
        LairStyle style = room.Style;
        int wall = room.HalfAcross;
        int deep = room.HalfDeep;
        room.Floor(style.Carpet(0), (across, along) => Math.Abs(across) <= 1);

        int forward = -deep + 2;
        int limit = deep - 2;
        for (int i = 0; i < Sequence.Length; i++)
        {
            int length = Sequence[i] == Bay.Grand ? 2 : 1;
            if (forward + length - 1 > limit)
            {
                break;
            }
            PlaceBay(room, Sequence[i], wall, forward);
            if (i % 2 == 0)
            {
                room.TryTorch(room.At(wall, forward + length));
                room.TryTorch(room.At(-wall, forward + length));
            }
            forward += length + 1;
        }

        room.TrySpawn(SL_FuneraryDefOf.SculptureLarge, 0, deep, Rot4.North, paint: style.Paint(0), quality: QualityCategory.Masterwork);
        room.TryTorch(room.At(2, deep));
        room.TryTorch(room.At(-2, deep));
    }

    private static void PlaceBay(LairRoom room, Bay bay, int wall, int forward)
    {
        QualityCategory quality = RandomQuality();
        switch (bay)
        {
            case Bay.Small:
                room.TrySpawnPair(SL_LairDefOf.SculptureSmall, wall, forward, Rot4.North, paint: room.Style.Paint(2), quality: quality);
                break;
            case Bay.Large:
                room.TrySpawnPair(SL_FuneraryDefOf.SculptureLarge, wall, forward, Rot4.North, paint: room.Style.Paint(1), quality: quality);
                break;
            default:
                ThingDef grand = SL_FuneraryDefOf.SculptureGrand;
                LairRoomWorker_Tomb.TrySpawnCovering(room, grand, room.At(wall - 1, forward), room.At(wall, forward + 1), Rot4.North, paint: room.Style.Paint(1), quality: quality);
                LairRoomWorker_Tomb.TrySpawnCovering(room, grand, room.At(-wall, forward), room.At(-(wall - 1), forward + 1), Rot4.North, paint: room.Style.Paint(1), quality: quality);
                break;
        }
    }

    // Good, excellent or masterwork, the middle one most often.
    private static QualityCategory RandomQuality()
    {
        float roll = Rand.Value;
        if (roll < 0.25f)
        {
            return QualityCategory.Good;
        }
        return roll < 0.8f ? QualityCategory.Excellent : QualityCategory.Masterwork;
    }
}
