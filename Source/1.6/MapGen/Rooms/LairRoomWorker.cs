namespace SanguophageLair;

// Furnishes one kind of lair room. A LairRoomDef names its worker class; the def's single
// worker instance is asked to fill every room of that kind, so workers hold no per-room state.
//
// Fill runs after the whole structure is stamped (walls, doors and floors exist; nothing else
// does) and before fog. Everything goes through the LairRoom handle: its local frame, its
// TrySpawn family (which refuse to place anything outside the room, on top of something else
// or in front of a doorway), its floor and torch helpers. A worker lays out its ideal room and
// lets TrySpawn drop what a small or oddly shaped room cannot take. After Fill the contents
// step re-checks that the room's doorways still connect and that its roof is held up, so a
// worker need not prove either, but should design for both: keep a walking lane open and put
// columns in rooms more than 13 cells across.
//
// The base class leaves a room bare: stamped floor and walls, nothing else.
public class LairRoomWorker
{
    public LairRoomDef def;

    public virtual void Fill(LairRoom room)
    {
    }
}
