using System;
using RimWorld;
using Verse;

namespace SanguophageLords;

// The temple: a long nave to whatever the lair's masters worship, in Ideology's Morbid style.
// Only requested when Ideology is active; a bare room is left otherwise.
//
// Layout in the room's local frame (far = HalfDeep, wall = HalfAcross):
//   right -1..1       the aisle: a red carpet runner from the door to the dais
//   far-3..far        the dais, a scarlet field across right -3..3
//   right 0, far-1    the grand altar against the far wall, its interaction cell on the dais
//   right +-2, far-2  a lectern each, beside the altar
//   right +-3, far-1  a skullspike each, at the dais edge
//   right 0, far-5    the ideogram in the floor before the dais steps
//   right +-3         pews in rows two cells apart, all facing the altar
//   right +-5 or 6    a column every fourth cell holding the roof, against the side walls
// Blood torches go along the walls, far wall first (LairRoom.TorchesAlongWalls), because the
// lair's light is red everywhere else; darktorches would glow cyan.
//
// The lair's faction has no ideoligion, so nothing here is linked to a precept. Altars,
// ideograms, lecterns, pews and skullspikes are inert buildings without one: their comps are
// CompStyleable (null precept is handled everywhere it is read) and ritual-only effecters;
// RoomRoleWorker_WorshipRoom counts only altars with a precept, so the room is not labelled
// a temple by the game, which is cosmetic. Morbid styles are applied through Thing.StyleDef
// after the spawn (Thing.StyleDef clears the cached graphic; the map mesh is dirtied so the
// styled graphic is drawn). Of the pieces used, only altar, ideogram and column have a Morbid
// style; pews, lecterns and skullspikes always draw their default look.
//
// Facings (decompile-verified on 1.6): a building's interaction cell is its offset rotated by
// the building's rotation, and the altar's offset is (0,0,-2), so the altar's local North puts
// the interaction cell on the entrance side. A seat's rotation points at what it faces
// (SpectatorCellFinder.IsCorrectlyRotatedChair), so pews also take local North.
public class LairRoomWorker_Temple : LairRoomWorker
{
    private const int PewRowSpacing = 2;
    private const int ColumnSpacing = 4;

    public override void Fill(LairRoom room)
    {
        if (!ModsConfig.IdeologyActive || SL_OptionalRoomDefOf.Altar_Grand == null)
        {
            return;
        }

        int far = room.HalfDeep;
        int wall = room.HalfAcross;
        LairStyle style = room.Style;

        room.Floor(style.Carpet(1), (right, forward) => Math.Abs(right) <= 1 && forward < far - 3);
        room.Floor(style.Carpet(0), (right, forward) => Math.Abs(right) <= 3 && forward >= far - 3);

        ApplyStyle(room, room.TrySpawn(SL_OptionalRoomDefOf.Altar_Grand, 0, far - 1, Rot4.North), SL_OptionalRoomDefOf.Morbid_AltarGrand);
        ApplyStyle(room, room.TrySpawn(SL_OptionalRoomDefOf.Ideogram, 0, far - 5, Rot4.North), SL_OptionalRoomDefOf.Morbid_IdeogramA);
        room.TrySpawnPair(ThingDefOf.Lectern, 2, far - 2, Rot4.North);
        room.TrySpawnPair(ThingDefOf.Skullspike, 3, far - 1, Rot4.North, stuff: ThingDefOf.Steel);

        // Columns first, so the pews and torches fit around them.
        int columnRight = Math.Max(5, wall - 1);
        for (int forward = -far + 3; forward <= far - 4; forward += ColumnSpacing)
        {
            ApplyStyle(room, room.TrySpawn(ThingDefOf.Column, columnRight, forward, Rot4.North, paint: style.WallPaint), SL_OptionalRoomDefOf.Morbid_Column);
            ApplyStyle(room, room.TrySpawn(ThingDefOf.Column, -columnRight, forward, Rot4.North, paint: style.WallPaint), SL_OptionalRoomDefOf.Morbid_Column);
        }

        // Rows stop short of the ideogram (which covers far-6..far-4).
        for (int forward = -far + 3; forward <= far - 7; forward += PewRowSpacing)
        {
            room.TrySpawnPair(SL_OptionalRoomDefOf.Pew, 3, forward, Rot4.North, paint: style.Paint(1));
        }

        room.TorchesAlongWalls(4);
    }

    // Sets a Morbid style on a thing the room just spawned. A null thing (it did not fit) or a
    // null style does nothing.
    private static void ApplyStyle(LairRoom room, Thing thing, ThingStyleDef styleDef)
    {
        if (thing == null || styleDef == null)
        {
            return;
        }
        thing.StyleDef = styleDef;
        thing.DirtyMapMesh(room.Map);
    }
}
