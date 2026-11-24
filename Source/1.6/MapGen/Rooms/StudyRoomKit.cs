using RimWorld;
using Verse;

namespace SanguophageLair;

// Placement helpers shared by the library, laboratory and prison workers: pieces described by
// the cells they should cover, in the room's local frame, rather than by an anchor cell.
//
// Multi-cell furniture is anchored on a centre cell whose meaning shifts with size, rotation
// and parity (GenAdj.OccupiedRect), so a worker that says "a bank across right 2..3 on the
// far wall" would otherwise have to work out the anchor for each of the four facings. These
// helpers search the anchor and the facing instead, and verify the facing against the def's
// own interaction cell, so a bench never ends up with its user cell inside a wall.
//
// Facing. The "face" of a piece is the side a person uses it from, as a direction pointing
// from the piece into the room. It maps onto the rotation as follows:
//   - defs with an interaction cell (benches, the gene assembler and extractor): the rotation
//     whose interaction cell lies directly beyond the footprint on the face side;
//   - beds: the rotation equal to the face, which puts the head at the opposite end
//     (BedUtility.GetSlotPos: rotation South has its head at the north end);
//   - everything else that rotates (shelves, bookcases, chairs, gene banks): the rotation
//     equal to the face, as vanilla places a shelf facing south with its back to the north wall.
public static class StudyRoomKit
{
    // The map cells between two local-frame corners, inclusive and in any order.
    public static CellRect LocalRect(LairRoom room, int right0, int forward0, int right1, int forward1)
    {
        return CellRect.FromLimits(room.At(right0, forward0), room.At(right1, forward1));
    }

    // Spawns the def over the local-frame rectangle, facing the local direction. Null when the
    // rectangle does not suit the def's size or any of it is not free floor of the room.
    public static Thing Place(LairRoom room, ThingDef def, int right0, int forward0, int right1, int forward1, Rot4 localFace, ThingDef stuff = null, ColorDef paint = null, QualityCategory? quality = null)
    {
        return Place(room, def, LocalRect(room, right0, forward0, right1, forward1), room.Turn(localFace), stuff, paint, quality);
    }

    // The same with the rectangle and face already in map terms.
    public static Thing Place(LairRoom room, ThingDef def, CellRect rect, Rot4 face, ThingDef stuff = null, ColorDef paint = null, QualityCategory? quality = null)
    {
        if (def == null)
        {
            return null;
        }
        for (int i = 0; i < 4; i++)
        {
            var rot = new Rot4(i);
            if (!def.rotatable && rot != Rot4.North)
            {
                continue;
            }
            IntVec3 anchor = AnchorFor(def, rect, rot);
            if (!anchor.IsValid || (def.rotatable && !FacesCorrectly(room, def, rect, anchor, rot, face)))
            {
                continue;
            }
            return room.TrySpawn(def, anchor, rot, stuff, paint, quality);
        }
        return null;
    }

    // The stone the lair is built of where the def accepts stone, else steel where it accepts
    // metal, else null (the def's own default). Keeps furniture out of wood where it can be.
    public static ThingDef Stuff(LairRoom room, ThingDef def)
    {
        if (def == null || !def.MadeFromStuff)
        {
            return null;
        }
        if (room.Style.Blocks.stuffProps.CanMake(def))
        {
            return room.Style.Blocks;
        }
        return ThingDefOf.Steel.stuffProps.CanMake(def) ? ThingDefOf.Steel : null;
    }

    private static IntVec3 AnchorFor(ThingDef def, CellRect rect, Rot4 rot)
    {
        foreach (IntVec3 c in rect)
        {
            if (GenAdj.OccupiedRect(c, rot, def.size) == rect)
            {
                return c;
            }
        }
        return IntVec3.Invalid;
    }

    private static bool FacesCorrectly(LairRoom room, ThingDef def, CellRect rect, IntVec3 anchor, Rot4 rot, Rot4 face)
    {
        if (!def.hasInteractionCell)
        {
            return rot == face;
        }
        IntVec3 use = ThingUtility.InteractionCellWhenAt(def, anchor, rot, room.Map);
        switch (face.AsInt)
        {
            case 0:
                return use.z == rect.maxZ + 1 && use.x >= rect.minX && use.x <= rect.maxX;
            case 2:
                return use.z == rect.minZ - 1 && use.x >= rect.minX && use.x <= rect.maxX;
            case 1:
                return use.x == rect.maxX + 1 && use.z >= rect.minZ && use.z <= rect.maxZ;
            default:
                return use.x == rect.minX - 1 && use.z >= rect.minZ && use.z <= rect.maxZ;
        }
    }
}
