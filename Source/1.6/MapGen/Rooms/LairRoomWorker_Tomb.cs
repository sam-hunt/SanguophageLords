using System;
using RimWorld;
using Verse;

namespace SanguophageLair;

// The tomb: a formal burial chamber for the lair's honoured dead. Two of them open off the
// processional's long sides, so one faces the other across the main hall.
//
// Layout in the room's local frame (right -wall..wall, forward -deep..deep), for the seven to
// nine cells across the def gives it:
//   right 0, +-1   carpet runner from the door to the far wall, the walking aisle
//   right +-2      burgundy carpet edging, only in the nine-wide rooms (seven-wide ones have
//                  no spare cell for it between the aisle and the sarcophagi)
//   right +-wall   sarcophagi lying across the side rows (two cells long, so they take
//                  right wall-1..wall), heads to the wall, one every second cell, blood torches
//                  standing in every other gap against the wall
//   far wall       a grand stele filling the end of the aisle (right -1..1, the last three
//                  rows), a torch in each far corner
// Sarcophagi are red-painted stone. A few hold a desiccated corpse (see TryInter); most are
// empty. The helpers below are shared with the other funerary workers.
//
// Facing and footprints. A sarcophagus is 1x2 and rotatable; the cell a placement is centred on
// depends on the rotation, so TrySpawnCovering searches for the centre whose footprint is the
// wanted pair of cells instead of computing it. The head end is assumed to be the end the
// rotation points to (not verifiable here: the art ships in the game's asset bundles).
public class LairRoomWorker_Tomb : LairRoomWorker
{
    private const float BodyChance = 0.3f;
    private const int MaxBodies = 3;

    public override void Fill(LairRoom room)
    {
        LairStyle style = room.Style;
        int wall = room.HalfAcross;
        int deep = room.HalfDeep;

        if (wall >= 4)
        {
            room.Floor(style.Carpet(2), (right, forward) => Math.Abs(right) == 2);
        }
        room.Floor(style.Carpet(0), (right, forward) => Math.Abs(right) <= 1);

        int bodies = 0;
        int gap = 0;
        for (int forward = -deep + 2; forward <= deep - 2; forward += 2, gap++)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                Rot4 head = side > 0 ? Rot4.East : Rot4.West;
                Thing sarcophagus = TrySpawnBerth(room, ThingDefOf.Sarcophagus, room.At(side * wall, forward), room.Turn(head), paint: style.Paint(0));
                if (sarcophagus != null && bodies < MaxBodies && Rand.Chance(BodyChance) && TryInter(sarcophagus))
                {
                    bodies++;
                }
            }
            if (gap % 2 == 0 && forward + 1 < deep)
            {
                room.TryTorch(room.At(wall, forward + 1));
                room.TryTorch(room.At(-wall, forward + 1));
            }
        }
        room.TryTorch(room.At(wall, deep));
        room.TryTorch(room.At(-wall, deep));

        // The focal piece. A room too short for the stele (or one with a door in its far wall)
        // gets a sculpture at the aisle's end instead.
        Thing stele = TrySpawnCovering(room, ThingDefOf.SteleGrand, room.At(-1, deep - 2), room.At(1, deep), Rot4.North, paint: style.Paint(1));
        if (stele == null)
        {
            room.TrySpawn(SL_FuneraryDefOf.SculptureLarge, 0, deep, Rot4.North, paint: style.Paint(1));
        }
    }

    // Spawns a def so that it covers exactly the cells of the rectangle between the two corner
    // cells, facing rot. Null when no centre gives that footprint or the footprint is not free.
    internal static Thing TrySpawnCovering(LairRoom room, ThingDef def, IntVec3 cornerA, IntVec3 cornerB, Rot4 rot, ThingDef stuff = null, ColorDef paint = null, QualityCategory? quality = null)
    {
        int minX = Math.Min(cornerA.x, cornerB.x);
        int maxX = Math.Max(cornerA.x, cornerB.x);
        int minZ = Math.Min(cornerA.z, cornerB.z);
        int maxZ = Math.Max(cornerA.z, cornerB.z);
        if (!def.rotatable)
        {
            rot = Rot4.North;
        }
        var search = new CellRect(minX - 1, minZ - 1, maxX - minX + 3, maxZ - minZ + 3);
        foreach (IntVec3 c in search)
        {
            CellRect footprint = GenAdj.OccupiedRect(c, rot, def.size);
            if (footprint.minX == minX && footprint.maxX == maxX && footprint.minZ == minZ && footprint.maxZ == maxZ)
            {
                return room.TrySpawn(def, c, rot, stuff, paint, quality);
            }
        }
        return null;
    }

    // Spawns a 1x2 piece lying with its head on the given cell and its foot one cell back from
    // it, the head toward the map direction headDir (a wall, usually).
    internal static Thing TrySpawnBerth(LairRoom room, ThingDef def, IntVec3 head, Rot4 headDir, ThingDef stuff = null, ColorDef paint = null, QualityCategory? quality = null)
    {
        return TrySpawnCovering(room, def, head, head - headDir.FacingCell, headDir, stuff, paint, quality);
    }

    // Lays a desiccated humanlike corpse in a sarcophagus or grave, the way vanilla fills the
    // caskets of its terror buildings (a generated pawn, killed, accepted by the casket) and
    // ages its desiccated corpses. No clothes, no relations, no belongings. Returns whether the
    // vessel now holds a body.
    internal static bool TryInter(Thing vessel)
    {
        if (!(vessel is Building_Casket casket) || casket.HasAnyContents)
        {
            return false;
        }
        var request = new PawnGenerationRequest(
            PawnKindDefOf.Villager,
            null,
            PawnGenerationContext.NonPlayer,
            casket.Map.Tile,
            canGeneratePawnRelations: false,
            allowDead: true,
            allowFood: false,
            forceNoIdeo: true,
            forceNoBackstory: true,
            forbidAnyTitle: true,
            forceNoGear: true);
        Pawn pawn = PawnGenerator.GeneratePawn(request);
        if (!pawn.Dead)
        {
            pawn.Kill(null, null);
        }
        Corpse corpse = pawn.Corpse;
        if (corpse == null)
        {
            return false;
        }
        pawn.relations.hidePawnRelations = true;
        if (!casket.TryAcceptThing(corpse, allowSpecialEffects: false))
        {
            corpse.Destroy();
            return false;
        }
        corpse.GetComp<CompRottable>()?.RotImmediately(RotStage.Dessicated);
        return true;
    }
}
