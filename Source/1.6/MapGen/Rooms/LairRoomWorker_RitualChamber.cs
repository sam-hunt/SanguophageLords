using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SanguophageLords;

// The ritual chamber: a round room deep in the lair, behind a corridor, where its masters
// did something they should not have. Only requested when Anomaly is active; a bare room is
// left otherwise. It is the darkest room of the lair: two blood torches, flanking the way in.
//
// Layout in the room's local frame (angles run from the far wall, clockwise seen from above,
// so +90 is the right-hand wall and 180 the door):
//   centre          the psychic ritual spot (3x3) on a bioferrite plate, inside a ring of
//                   burgundy carpet
//   +-3, +-3        the four shard beacons, on the carpet ring
//   0, +-60, +-120  the void sculptures, against the wall
//   +-90            two shelves each, with a tome on each
//   +-150           a blood torch each, either side of the door
// Everything is mirrored about the centre line.
//
// A player who claims the room finds a correct ritual site: the spot's own radius (3.9 cells)
// holds nothing that is not walkable, since a ritual is refused while it is obstructed, and
// the counts stay within what one spot links (four beacons, six sculptures; five are used).
//
// Spawn-time safety (decompile-verified on 1.6): the spot's comp only caches its radius, ticks
// to list obstructions and offers ritual gizmos to the player's own; with no owner it does
// nothing else. None of spot, beacon, sculpture, tome or shelf has an entity codex entry or
// hiddenWhileUndiscovered, so Thing.SpawnSetup's codex and hidden-item hooks do nothing for
// them; they touch neither Find.Anomaly nor the codex. Nothing that advances the storyline
// (monolith, obelisks, pit gate, fleshmass, grey statues, floor etchings) is used. The tomes
// are loot, as vanilla's are: reading one grants hidden Anomaly research at a mental-break
// risk, and nothing happens until it is read.
public class LairRoomWorker_RitualChamber : LairRoomWorker
{
    // Squared distances from the centre: the carpet ring reaches past the beacons' cells
    // (3,3), which stand just outside the ritual spot's obstruction radius of 3.9.
    private const int CarpetRingSquared = 20;
    private const int PlateHalfWidth = 1;

    public override void Fill(LairRoom room)
    {
        if (!ModsConfig.AnomalyActive || ThingDefOf.PsychicRitualSpot == null)
        {
            return;
        }

        room.Floor(room.Style.Carpet(2), (right, forward) => (right * right) + (forward * forward) <= CarpetRingSquared);
        room.Floor(SL_OptionalRoomDefOf.BioferritePlate, (right, forward) => Math.Abs(right) <= PlateHalfWidth && Math.Abs(forward) <= PlateHalfWidth);
        room.TrySpawn(ThingDefOf.PsychicRitualSpot, 0, 0, Rot4.North);

        room.TrySpawnPair(SL_OptionalRoomDefOf.ShardBeacon, 3, 3, Rot4.North);
        room.TrySpawnPair(SL_OptionalRoomDefOf.ShardBeacon, 3, -3, Rot4.North);

        // Sculptures and shelves are found by angle on the right-hand side and mirrored, so a
        // stepped outline gives the same pieces on both sides.
        SpawnOnWall(room, SL_OptionalRoomDefOf.VoidSculpture, 0, null);
        SpawnOnWall(room, SL_OptionalRoomDefOf.VoidSculpture, 60, null);
        SpawnOnWall(room, SL_OptionalRoomDefOf.VoidSculpture, 120, null);
        for (int i = 0; i < 2; i++)
        {
            Thing shelf = SpawnOnWall(room, ThingDefOf.ShelfSmall, 90, room.Style.Paint(1));
            if (shelf != null)
            {
                room.TrySpawnItem(ThingDefOf.Tome, shelf.Position, 1, onShelf: true);
            }
        }
        SpawnOnWall(room, ThingDefOf.SanguphageMeetingTorch, 150, null);
    }

    // Spawns the piece at the free cell beside a wall nearest the given angle, and its mirror
    // image at the negative angle (a piece on the centre line is spawned once). Returns the
    // piece on the right-hand side, or null when it did not fit.
    private static Thing SpawnOnWall(LairRoom room, ThingDef def, int degrees, ColorDef paint)
    {
        if (!FindWallCell(room, degrees, out IntVec3 cell, out Rot4 wallDir))
        {
            return null;
        }
        room.Local(cell, out int rightward, out int forwardward);
        Thing placed = room.TrySpawn(def, cell, wallDir, paint: paint);
        if (degrees != 0 && degrees != 180 && placed != null)
        {
            IntVec3 twin = room.At(-rightward, forwardward);
            room.TrySpawn(def, twin, MirroredWall(room, twin), paint: paint);
        }
        return placed;
    }

    // The free floor cell with a wall directly beside it whose angle from the room's centre
    // is nearest the target, with the map direction of that wall. Far from the centre wins a
    // tie, so the cell sits on the outermost ring.
    private static bool FindWallCell(LairRoom room, int degrees, out IntVec3 cell, out Rot4 wallDir)
    {
        cell = IntVec3.Invalid;
        wallDir = Rot4.North;
        float bestScore = float.MaxValue;
        foreach (KeyValuePair<IntVec3, Rot4> side in room.WallSideCells())
        {
            room.Local(side.Key, out int rightward, out int forwardward);
            float angle = (float)(Math.Atan2(rightward, forwardward) * 180.0 / Math.PI);
            float diff = Math.Abs(angle - degrees);
            float score = diff - (((rightward * rightward) + (forwardward * forwardward)) * 0.001f);
            if (score < bestScore)
            {
                bestScore = score;
                cell = side.Key;
                wallDir = side.Value;
            }
        }
        return cell.IsValid;
    }

    // The direction of a wall beside the cell, for a mirrored piece (the mirror cell's wall
    // is the mirror of the original's, but looking it up keeps stepped corners honest).
    private static Rot4 MirroredWall(LairRoom room, IntVec3 cell)
    {
        for (int i = 0; i < 4; i++)
        {
            var dir = new Rot4(i);
            if (room.IsWall(cell + dir.FacingCell))
            {
                return dir;
            }
        }
        return Rot4.North;
    }
}
