using System;
using Verse;

namespace SanguophageLair;

// GenStep of SL_LairContents: runs each stamped room's worker, then the two safety nets every
// room gets (doorways still connected, roof held up). Does nothing when the structure step
// could not place a lair.
//
// Each worker runs inside its own try/catch: one room's exception must not leave every room
// after it empty, and a lair with one bare room is still playable.
public class GenStep_LairContents : GenStep
{
    public override int SeedPart => 2093477151;

    public override void Generate(Map map, GenStepParams parms)
    {
        LairSite site = LairSite.Current;
        if (site == null)
        {
            return;
        }
        foreach (PlanRoom planRoom in site.Plan.Rooms)
        {
            var room = new LairRoom(site, planRoom);
            site.Rooms.Add(room);
            try
            {
                room.Def.Worker.Fill(room);
            }
            catch (Exception e)
            {
                Log.Error("[Sanguophage Lair] Furnishing the " + room.Def.defName + " room at " + room.Center + " failed: " + e);
            }
            int removed = room.EnsureWalkable();
            int columns = room.EnsureRoofSupport();
            if (Prefs.DevMode && (removed > 0 || columns > 0))
            {
                Log.Message("[Sanguophage Lair] " + room.Def.defName + " at " + room.Center + ": removed " + removed + " blocking pieces, added " + columns + " roof columns.");
            }
        }
    }
}
