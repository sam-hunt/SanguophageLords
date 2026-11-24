using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SanguophageLair;

// One line of a lair's room program: a room kind, how many of it, and how likely it is to be
// asked for at all. A MayRequire on the <li> drops the line when its DLC or mod is absent,
// which is how the optional rooms are gated.
public class LairRoomEntry
{
    public LairRoomDef room;
    public IntRange count = IntRange.One;
    public float chance = 1f;
}

// A whole lair: the rooms of its spine, the program of rooms grown around it, and the look of
// its stonework. Shared by every generator that builds the complex (the underground pocket map
// today), so the surface variant builds the same lair from the same def.
public class LairLayoutDef : Def
{
    // The spine, in the order a visitor walks it.
    public LairRoomDef entranceHall;
    public LairRoomDef forecourt;
    public LairRoomDef vestibule;
    public LairRoomDef processional;
    public LairRoomDef sanctum;
    public LairRoomDef corridor;

    // Requested in list order, so the rooms that matter most come first and get the best
    // sites; whatever no longer fits is dropped.
    public List<LairRoomEntry> rooms = new List<LairRoomEntry>();

    // Planner tuning; see LairPlanParams.
    public int margin = 7;
    public int attempts = 4;
    public float wideCorridorChance = 0.7f;
    public float loopChance = 0.35f;
    public float hewnBulgeChance = 0.3f;
    public int forecourtDepth = 5;
    public int forecourtWidth = 21;

    // Paint over the lair's stone. Null leaves the stone its own colour.
    public ColorDef wallPaint;
    public ColorDef doorPaint;
    // Paints for furniture, strongest accent first; workers index into it.
    public List<ColorDef> furniturePaints = new List<ColorDef>();
    // Carpet terrains by defName, brightest first. Names rather than references because the
    // carpet defs are generated at load from colour defs and cannot be cross-referenced.
    // NoTranslate keeps the game's translation walker from offering the names to translators.
    [NoTranslate]
    public List<string> carpets = new List<string>();

    // Lays every utility line (power conduits, and hemogen pipes when Vanilla Races Expanded -
    // Sanguophage is active) hidden. False shows them, as Biotech's own sanguophage base does
    // along its halls, except under carpet, where they are always hidden.
    public bool hiddenConduits;

    public LairPlanParams ToPlanParams(int width, int height, bool undergroundApproach)
    {
        var parms = new LairPlanParams
        {
            Width = width,
            Height = height,
            Margin = margin,
            Attempts = attempts,
            UndergroundApproach = undergroundApproach,
            WideCorridorChance = wideCorridorChance,
            LoopChance = loopChance,
            HewnBulgeChance = hewnBulgeChance,
            ForecourtDepth = forecourtDepth,
            ForecourtWidth = forecourtWidth,
            EntranceHall = entranceHall.ToRequest(),
            Forecourt = forecourt.ToRequest(),
            Vestibule = vestibule.ToRequest(),
            Processional = processional.ToRequest(),
            Sanctum = sanctum.ToRequest(),
            Corridor = corridor.ToRequest(),
        };
        foreach (LairRoomEntry entry in rooms)
        {
            if (!Rand.Chance(entry.chance))
            {
                continue;
            }
            int count = entry.count.RandomInRange;
            for (int i = 0; i < count; i++)
            {
                parms.Program.Add(entry.room.ToRequest());
            }
        }
        return parms;
    }

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (string error in base.ConfigErrors())
        {
            yield return error;
        }
        if (entranceHall == null || forecourt == null || vestibule == null || processional == null || sanctum == null || corridor == null)
        {
            yield return "every spine room (entranceHall, forecourt, vestibule, processional, sanctum, corridor) must be set";
        }
    }
}
