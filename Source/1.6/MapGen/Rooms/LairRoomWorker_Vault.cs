using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SanguophageLair;

// The vault: the lair's treasury and the player's main material reward. A small tiled room
// behind one door, carpeted inside a ring of bare tile, with a painted shelf run down each
// side wall, a pair of large sculptures (and small shelves in the wider room) on the far
// wall around a torch, and a central display table carrying the pieces worth a sanguophage's
// pride: archite capsules and a deathrest capacity serum.
//
// Local frame, for a room HalfAcross = a and HalfDeep = d (both 2 or 3):
//   right +-a, forward -d+1..d   shelves along the side walls, two cells each
//   forward d                    sculptures at right +-(a-1), small shelves at +-1 when a = 3,
//                                a torch at (0, d)
//   (0, 0..1)                    the display table; the door's lane in front stays clear
//   forward -d, right +-a        a torch in each corner beside the door
//
// Loot, with market values before quality and the player's own factors:
//   2 archite capsules (display)     1400   (700 each, def)
//   1 deathrest capacity serum       1000   (display)
//   1500 silver (3 stacks of 500)    1500
//   120 gold                         1200   (10 each)
//   150 jade (2 stacks of 75)         750   (5 each)
//   4 genepacks with random genes     400   (100 each, plus whatever the genes add)
//   plasteel longsword                965   (100 plasteel at 9 plus 18000 work at 0.0036)
//   plasteel mace                     470   (50 plasteel plus 6000 work)
//   total                            7685
// Weapons carry the usual base-generation quality, which moves the total a few percent. The
// shelves take everything except what sits on the display; anything left over because a
// piece of furniture did not fit lies on the free floor nearest the centre, so the reward
// never depends on the furniture fitting.
public class LairRoomWorker_Vault : LairRoomWorker
{
    private sealed class Loot
    {
        public ThingDef Def;
        public int Count;
        public ThingDef Stuff;
    }

    public override void Fill(LairRoom room)
    {
        int wall = room.HalfAcross;
        int far = room.HalfDeep;
        LairStyle style = room.Style;
        ColorDef paint = style.Paint(0);

        room.FloorRing(style.Carpet(0), 1, int.MaxValue);

        var rightSide = new List<Thing>();
        var leftSide = new List<Thing>();
        HouseholdLayout.SideShelves(room, wall, -far + 1, far, paint, rightSide, leftSide);

        room.TrySpawnPair(SL_HouseholdDefOf.SculptureLarge, wall - 1, far, Rot4.South, paint: paint);
        if (wall - 1 > 1)
        {
            Thing smallRight = HouseholdLayout.PlacePair(room, ThingDefOf.ShelfSmall, 1, far, 1, far, Rot4.South, out Thing smallLeft, paint: paint);
            AddCells(rightSide, smallRight);
            AddCells(leftSide, smallLeft);
        }
        room.TryTorch(room.At(0, far));
        room.TryTorch(room.At(wall, -far));
        room.TryTorch(room.At(-wall, -far));

        // The display: capsules and serum on the table's two cells.
        var overflow = new List<Loot>();
        Thing table = HouseholdLayout.Place(room, ThingDefOf.Table1x2c, 0, 0, 0, 1, Rot4.North, paint: paint);
        IntVec3 first = room.At(0, 0);
        IntVec3 second = room.At(0, 1);
        if (table == null || room.TrySpawnItem(ThingDefOf.ArchiteCapsule, first, 2, onShelf: true) == null)
        {
            overflow.Add(new Loot { Def = ThingDefOf.ArchiteCapsule, Count = 2 });
        }
        if (table == null || room.TrySpawnItem(SL_HouseholdDefOf.DeathrestCapacitySerum, second, 1, onShelf: true) == null)
        {
            overflow.Add(new Loot { Def = SL_HouseholdDefOf.DeathrestCapacitySerum, Count = 1 });
        }

        // The shelves, alternating sides so a missing shelf costs both walls equally.
        var cells = new List<IntVec3>();
        for (int i = 0; i < rightSide.Count || i < leftSide.Count; i++)
        {
            AddShelfCells(cells, rightSide, i);
            AddShelfCells(cells, leftSide, i);
        }
        var goods = new List<Loot>();
        AddStacks(goods, ThingDefOf.Silver, 1500, null);
        AddStacks(goods, ThingDefOf.Gold, 120, null);
        AddStacks(goods, ThingDefOf.Jade, 150, null);
        for (int i = 0; i < 4; i++)
        {
            goods.Add(new Loot { Def = ThingDefOf.Genepack, Count = 1 });
        }
        goods.Add(new Loot { Def = SL_HouseholdDefOf.MeleeWeapon_LongSword, Count = 1, Stuff = ThingDefOf.Plasteel });
        goods.Add(new Loot { Def = SL_HouseholdDefOf.MeleeWeapon_Mace, Count = 1, Stuff = ThingDefOf.Plasteel });

        // Interleave so each cell gets a mixed handful, not one commodity.
        for (int i = 0; i < goods.Count; i++)
        {
            Loot loot = goods[i];
            bool placed = false;
            for (int attempt = 0; attempt < cells.Count && !placed; attempt++)
            {
                IntVec3 c = cells[(i + attempt) % cells.Count];
                placed = room.TrySpawnItem(loot.Def, c, loot.Count, loot.Stuff, onShelf: true) != null;
            }
            if (!placed)
            {
                overflow.Add(loot);
            }
        }

        foreach (Loot loot in overflow)
        {
            DropOnFloor(room, loot);
        }
    }

    // The cells of the shelf standing in the list at the index, if any.
    private static void AddShelfCells(List<IntVec3> cells, List<Thing> shelves, int index)
    {
        if (index < shelves.Count)
        {
            foreach (IntVec3 c in shelves[index].OccupiedRect())
            {
                cells.Add(c);
            }
        }
    }

    private static void AddCells(List<Thing> shelves, Thing shelf)
    {
        if (shelf != null)
        {
            shelves.Add(shelf);
        }
    }

    // Splits a quantity into stacks the item can hold.
    private static void AddStacks(List<Loot> list, ThingDef def, int total, ThingDef stuff)
    {
        while (total > 0)
        {
            int stack = System.Math.Min(total, def.stackLimit);
            list.Add(new Loot { Def = def, Count = stack, Stuff = stuff });
            total -= stack;
        }
    }

    // The free floor cell nearest the centre of the room.
    private static void DropOnFloor(LairRoom room, Loot loot)
    {
        IntVec3 best = IntVec3.Invalid;
        int bestDist = int.MaxValue;
        foreach (IntVec3 c in room.Cells)
        {
            int dist = (c - room.Center).LengthHorizontalSquared;
            if (dist < bestDist && room.IsFree(c))
            {
                best = c;
                bestDist = dist;
            }
        }
        if (best.IsValid)
        {
            room.TrySpawnItem(loot.Def, best, loot.Count, loot.Stuff);
        }
    }
}
