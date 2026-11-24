using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SanguophageLair;

// One lair being generated: its plan in map coordinates, its layout def and its resolved
// style. GenStep_LairStructure creates it and publishes it as a MapGenerator var; the steps
// that follow (exit placement, room contents) read it back, so the lair's generation can be
// split across gensteps without any of them re-deriving the plan. It lives only for the
// duration of map generation and is never saved.
//
// It also carries the inhabitants' generation state: the owning faction, the pawn budget the
// room workers' requests are checked against, and the defenders spawned so far, which
// GenStep_LairDefenders turns into lords. What must outlive generation lives in those lords
// and in the LairAlarm thing, not here.
public sealed class LairSite
{
    private const string VarName = "SL_LairSite";

    public Map Map { get; }

    public LairPlan Plan { get; }

    public LairLayoutDef Layout { get; }

    public LairStyle Style { get; }

    // Room handles in plan order, created by the contents step.
    public List<LairRoom> Rooms { get; } = new List<LairRoom>();

    // ----- Inhabitants (LairPawns, GenStep_LairDefenders) -----

    private Faction faction;
    private LairPawnBudget pawnBudget;

    // The faction owning the lair's inhabitants and their beds, found or created on first use.
    public Faction Faction => faction ??= LairFaction.FindOrCreate();

    // Threat points the inhabitants are bought with. Set it before the contents step runs to
    // scale the lair (a quest's threat points); read once, on the first pawn request.
    public float DefenderPoints { get; set; } = LairPawnBudget.DefaultPoints;

    public LairPawnBudget PawnBudget => pawnBudget ??= LairPawns.MakeBudget(DefenderPoints);

    // Defenders in the order the room workers spawned them, for GenStep_LairDefenders.
    public List<LairDefender> Defenders { get; } = new List<LairDefender>();

    public LairSite(Map map, LairPlan plan, LairLayoutDef layout, LairStyle style)
    {
        Map = map;
        Plan = plan;
        Layout = layout;
        Style = style;
    }

    // The lair generated earlier in the current map generation, or null (the structure step
    // did not run, or could not fit a plan into the map).
    public static LairSite Current => MapGenerator.TryGetVar(VarName, out LairSite site) ? site : null;

    public void Publish()
    {
        MapGenerator.SetVar(VarName, this);
    }
}
