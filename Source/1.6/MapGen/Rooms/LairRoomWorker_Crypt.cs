using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace SanguophageLords;

// The crypt: a humble burial niche hewn from the rock on the lair's outskirts. Its outline is
// ragged and its floor is the bare rock the room was cut from, so nothing here is symmetric:
// the worker works from the walls that actually exist (LairRoom.WallSideCells) and rolls one
// of three arrangements.
//   niches    a row of up to three sarcophagi, heads to the longest straight stretch of wall
//             and one cell apart, and a couple of urns in the corners
//   graves    up to three plain graves side by side against a wall, dug only where the floor
//             accepts a spade (Diggable), else the arrangement falls back to niches
//   ossuary   one sarcophagus and a crowd of urns heaped in a corner
// Light is one blood torch, sometimes two. Sarcophagi and graves sometimes hold a desiccated
// corpse (LairBurial.TryInter). Skulls and bones: neither Core nor Biotech has a
// decoration item for them (the skull item and skullspike are Ideology), so there are none.
public class LairRoomWorker_Crypt : LairRoomWorker
{
    private const float BodyChance = 0.35f;
    private const int MaxBodies = 2;

    public override void Fill(LairRoom room)
    {
        var bodies = new int[1];
        switch (Rand.RangeInclusive(0, 2))
        {
            case 1:
                if (!Graves(room, bodies))
                {
                    Niches(room, bodies);
                }
                Urns(room, Rand.RangeInclusive(1, 2));
                break;
            case 2:
                Niche(room, 1, bodies);
                Ossuary(room);
                break;
            default:
                Niches(room, bodies);
                Urns(room, Rand.RangeInclusive(1, 2));
                break;
        }
        Torches(room);
    }

    private static void Niches(LairRoom room, int[] bodies)
    {
        Niche(room, Rand.RangeInclusive(2, 3), bodies);
    }

    // Sarcophagi lined along the wall direction with the most room for them, one cell apart.
    private static void Niche(LairRoom room, int count, int[] bodies)
    {
        List<KeyValuePair<IntVec3, Rot4>> spots = SpotsAlongBestWall(room, ThingDefOf.Sarcophagus.size.z);
        int placed = 0;
        IntVec3 last = IntVec3.Invalid;
        foreach (KeyValuePair<IntVec3, Rot4> spot in spots)
        {
            if (placed >= count)
            {
                break;
            }
            if (last.IsValid && Math.Abs(spot.Key.x - last.x) + Math.Abs(spot.Key.z - last.z) < 2)
            {
                continue;
            }
            Thing sarcophagus = Berth(room, ThingDefOf.Sarcophagus, spot, room.Style.Paint(2));
            if (sarcophagus == null)
            {
                continue;
            }
            placed++;
            last = spot.Key;
            MaybeInter(sarcophagus, bodies);
        }
    }

    private static bool Graves(LairRoom room, int[] bodies)
    {
        List<KeyValuePair<IntVec3, Rot4>> spots = SpotsAlongBestWall(room, ThingDefOf.Grave.size.z);
        int placed = 0;
        int wanted = Rand.RangeInclusive(2, 3);
        foreach (KeyValuePair<IntVec3, Rot4> spot in spots)
        {
            if (placed >= wanted)
            {
                break;
            }
            IntVec3 foot = spot.Key - spot.Value.FacingCell;
            if (!Diggable(room, spot.Key) || !Diggable(room, foot))
            {
                continue;
            }
            Thing grave = Berth(room, ThingDefOf.Grave, spot, null);
            if (grave != null)
            {
                placed++;
                MaybeInter(grave, bodies);
            }
        }
        return placed > 0;
    }

    // A 1x2 piece lying with its head on the wall-side cell and its foot one cell back from
    // it, the head toward the wall.
    private static Thing Berth(LairRoom room, ThingDef def, KeyValuePair<IntVec3, Rot4> spot, ColorDef paint)
    {
        return room.TrySpawnOver(def, CellRect.FromLimits(spot.Key, spot.Key - spot.Value.FacingCell), spot.Value, paint: paint);
    }

    private static bool Diggable(LairRoom room, IntVec3 c)
    {
        return c.GetTerrain(room.Map).affordances.Contains(SL_LairDefOf.Diggable);
    }

    // The free wall-side cells of the wall direction with the most cells whose foot cell is
    // free too, in order along the wall, so a caller can take them one after another.
    private static List<KeyValuePair<IntVec3, Rot4>> SpotsAlongBestWall(LairRoom room, int length)
    {
        var byDir = new List<KeyValuePair<IntVec3, Rot4>>[4];
        for (int i = 0; i < 4; i++)
        {
            byDir[i] = new List<KeyValuePair<IntVec3, Rot4>>();
        }
        foreach (KeyValuePair<IntVec3, Rot4> side in room.WallSideCells())
        {
            IntVec3 foot = side.Key - (side.Value.FacingCell * (length - 1));
            if (length == 1 || room.IsFree(foot))
            {
                byDir[side.Value.AsInt].Add(side);
            }
        }
        int best = 0;
        for (int i = 1; i < 4; i++)
        {
            if (byDir[i].Count > byDir[best].Count || (byDir[i].Count == byDir[best].Count && Rand.Bool))
            {
                best = i;
            }
        }
        List<KeyValuePair<IntVec3, Rot4>> list = byDir[best];
        bool alongX = !new Rot4(best).IsHorizontal;
        list.Sort((a, b) => alongX ? a.Key.x.CompareTo(b.Key.x) : a.Key.z.CompareTo(b.Key.z));
        return list;
    }

    // Urns on random wall-side cells.
    private static void Urns(LairRoom room, int count)
    {
        List<IntVec3> cells = room.WallSideCells().Select(side => side.Key).Distinct().InRandomOrder().ToList();
        foreach (IntVec3 c in cells)
        {
            if (count <= 0)
            {
                break;
            }
            if (room.TrySpawn(ThingDefOf.Urn, c, Rot4.North) != null)
            {
                count--;
            }
        }
    }

    // Urns heaped around one corner cell: the corner and the free cells touching it.
    private static void Ossuary(LairRoom room)
    {
        IntVec3 corner = IntVec3.Invalid;
        foreach (IntVec3 c in room.Cells.InRandomOrder())
        {
            if (room.Inset(c) == 0 && room.IsFree(c) && CountWalls(room, c) >= 2)
            {
                corner = c;
                break;
            }
        }
        if (!corner.IsValid)
        {
            Urns(room, 3);
            return;
        }
        int count = Rand.RangeInclusive(3, 5);
        room.TrySpawn(ThingDefOf.Urn, corner, Rot4.North);
        count--;
        foreach (IntVec3 c in GenAdj.AdjacentCells.Select(offset => corner + offset).InRandomOrder())
        {
            if (count <= 0)
            {
                break;
            }
            if (room.TrySpawn(ThingDefOf.Urn, c, Rot4.North) != null)
            {
                count--;
            }
        }
    }

    private static int CountWalls(LairRoom room, IntVec3 c)
    {
        int walls = 0;
        for (int i = 0; i < 4; i++)
        {
            if (room.IsWall(c + GenAdj.CardinalDirections[i]))
            {
                walls++;
            }
        }
        return walls;
    }

    // One torch, sometimes a second well away from the first.
    private static void Torches(LairRoom room)
    {
        int wanted = Rand.Chance(0.5f) ? 2 : 1;
        var lit = new List<IntVec3>();
        foreach (IntVec3 c in room.WallSideCells().Select(side => side.Key).Distinct().InRandomOrder())
        {
            if (lit.Count >= wanted)
            {
                break;
            }
            if (lit.Any(other => (other - c).LengthHorizontalSquared < 16))
            {
                continue;
            }
            if (room.TryTorch(c) != null)
            {
                lit.Add(c);
            }
        }
    }

    private static void MaybeInter(Thing vessel, int[] bodies)
    {
        if (bodies[0] < MaxBodies && Rand.Chance(BodyChance) && LairBurial.TryInter(vessel))
        {
            bodies[0]++;
        }
    }
}
