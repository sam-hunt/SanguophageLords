using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SanguophageLair;

// The gene laboratory, where the lair's masters study and edit what they are. A working lab
// on the lair's stone tile (sterile floor would be wrong here), laid out like a bench room:
//
// Layout in the room's local frame, for the 9 to 11 cell square the def gives it:
//   far wall      hi-tech research bench, centred, used from the room side
//   right wall    a row of gene banks standing end to end, each holding genepacks
//   left wall     gene assembler at F -1..1, gene extractor above it, gene processor below
//   centre        a hospital bed for the subject, head toward the far wall
//   rest          blood torches along whatever wall is left
// Every machine is placed over the cells it should cover and its facing is found from the
// def's own interaction cell (StudyRoomKit), so each is used from the room side. The bench's
// user cell and the bed stay clear of each other at every size. The banks run from the far
// corner toward the entrance as far as the right wall goes, up to four, and the three
// left-wall machines need only seven cells of wall, so both walls fit at 9 deep.
//
// Rooms of this size are all within reach of a wall, so there are no columns. Everything
// here is a powered building: the contents step wires them into the lair's power net, so
// this worker only spawns them.
//
// Loot: three to five genepacks in the banks (each bank holds up to ten) and the machines
// themselves.
public class LairRoomWorker_Laboratory : LairRoomWorker
{
    private const int MaxBanks = 4;

    public override void Fill(LairRoom room)
    {
        int halfAcross = room.HalfAcross;
        int halfDeep = room.HalfDeep;
        ColorDef paint = room.Style.Paint(1);

        StudyRoomKit.Place(room, SL_StudyDefOf.HiTechResearchBench, -2, halfDeep - 1, 2, halfDeep, Rot4.South, StudyRoomKit.Stuff(room, SL_StudyDefOf.HiTechResearchBench));

        var banks = new List<Thing>();
        for (int i = 0; i < MaxBanks; i++)
        {
            int top = halfDeep - (2 * i);
            Thing bank = StudyRoomKit.Place(room, ThingDefOf.GeneBank, halfAcross, top - 1, halfAcross, top, Rot4.West);
            if (bank != null)
            {
                banks.Add(bank);
            }
        }

        StudyRoomKit.Place(room, ThingDefOf.GeneAssembler, -halfAcross, -1, -halfAcross + 1, 1, Rot4.East);
        StudyRoomKit.Place(room, SL_StudyDefOf.GeneExtractor, -halfAcross, 2, -halfAcross + 1, 3, Rot4.East);
        StudyRoomKit.Place(room, SL_StudyDefOf.GeneProcessor, -halfAcross, -3, -halfAcross + 1, -2, Rot4.East);

        StudyRoomKit.Place(room, SL_StudyDefOf.HospitalBed, 0, 0, 0, 1, Rot4.South, StudyRoomKit.Stuff(room, SL_StudyDefOf.HospitalBed), paint);

        room.TorchesAlongWalls(4);

        StockBanks(banks);
    }

    // Genepacks are made by the game's own recipe (a random gene set rolled in PostMake) and
    // added straight to a bank's container, one bank at a time in turn.
    private static void StockBanks(List<Thing> banks)
    {
        if (banks.Count == 0)
        {
            return;
        }
        int packs = Rand.RangeInclusive(3, 5);
        for (int i = 0; i < packs; i++)
        {
            CompGenepackContainer container = banks[i % banks.Count].TryGetComp<CompGenepackContainer>();
            if (container == null || container.Full)
            {
                continue;
            }
            Thing pack = ThingMaker.MakeThing(ThingDefOf.Genepack);
            if (pack.Destroyed || !container.innerContainer.TryAdd(pack))
            {
                pack.Destroy();
            }
        }
    }
}
