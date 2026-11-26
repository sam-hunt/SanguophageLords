using Verse;

namespace SanguophageLords;

// What a lair pawn is doing when the player arrives. Decides which lord it joins in
// GenStep_LairDefenders.
public enum LairPawnRole
{
    // Awake at a post in its room.
    Guard,
    // Asleep in an ordinary bed.
    Sleeper,
    // Deathresting in a casket.
    Deathrester,
}

// One defender as LairPawns registered it on LairSite.Defenders during room furnishing, read
// back by GenStep_LairDefenders to build lords. Generation-time only, never saved: once the
// genstep has run, the lords (LordJob_LairDefend) and the alarm (LairAlarm) hold everything
// that must persist.
public sealed class LairDefender
{
    public Pawn Pawn { get; }

    public LairPawnTier Tier { get; }

    public LairPawnRole Role { get; }

    public LairRoom Room { get; }

    public LairDefender(Pawn pawn, LairPawnTier tier, LairPawnRole role, LairRoom room)
    {
        Pawn = pawn;
        Tier = tier;
        Role = role;
        Room = room;
    }
}
