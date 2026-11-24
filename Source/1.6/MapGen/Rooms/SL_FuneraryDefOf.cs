using RimWorld;
using Verse;

namespace SanguophageLair;

// Vanilla defs the funerary rooms (tomb, mausoleum, crypt, gallery) look up that ThingDefOf
// does not carry. All Core. Each field names its C# consumer.
[DefOf]
public static class SL_FuneraryDefOf
{
    // LairRoomWorker_Mausoleum: the sculptures of the ring around the founder's sarcophagus.
    // LairRoomWorker_Gallery: the room's mid-size sculpture and its best piece.
    // LairRoomWorker_Tomb: the sculpture that stands in for the far-wall stele in a room too
    // short for it.
    public static ThingDef SculptureLarge;

    // LairRoomWorker_Gallery: the 2x2 pair standing in the middle of the long walls.
    public static ThingDef SculptureGrand;

    // LairRoomWorker_Crypt: graves are only dug where the floor accepts a spade.
    public static TerrainAffordanceDef Diggable;

    static SL_FuneraryDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(SL_FuneraryDefOf));
    }
}
