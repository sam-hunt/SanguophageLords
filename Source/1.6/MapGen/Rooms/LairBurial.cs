using RimWorld;
using Verse;

namespace SanguophageLords;

// Burial contents shared by the funerary rooms (tomb, crypt, mausoleum): what goes inside a
// sarcophagus or grave. Kept apart from any one worker because each of them rolls its own
// chance and cap for how many vessels are occupied.
public static class LairBurial
{
    // Lays a desiccated humanlike corpse in a sarcophagus or grave, the way vanilla fills the
    // caskets of its terror buildings (a generated pawn, killed, accepted by the casket) and
    // ages its desiccated corpses. No clothes, no relations, no belongings. Returns whether the
    // vessel now holds a body.
    public static bool TryInter(Thing vessel)
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
