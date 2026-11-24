using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace SLHarness
{
    // Throwaway harness. Contract with run.sh: params.txt (key=value) is read from
    // <SaveData>/SLHarness/, every output goes to <SaveData>/SLHarness/out/, and the line
    // "[SL-Harness] DONE" ends the run (then Root.Shutdown()).
    //
    // Capture modes, all attempted every run, each in its own try/catch:
    //   (c) schematic PNG + (d) ASCII dump + stats  -> managed code only, no render APIs.
    //   (a) Progress Renderer full-map render       -> only if that mod is in the list (reflection).
    //   (b) one zoomed-out ScreenCapture screenshot -> the whole map fits one frame (orthographic
    //       camera, RootSize = half the visible height in cells); run.sh crops it to the map.
    public class HarnessMod : Mod
    {
        public HarnessMod(ModContentPack content) : base(content)
        {
            DebugSettings.pauseOnError = false;
            Prefs.AutomaticPauseMode = AutomaticPauseMode.Never;
            Log.Message("[SL-Harness] mod loaded");
        }
    }

    public class GameComponent_Harness : GameComponent
    {
        static int phase, frames;
        static Map lairMap;
        static string outDir, prDir;
        static Dictionary<string, string> prm = new Dictionary<string, string>();
        static string shotPath;
        static bool prRequested;
        static DateTime prStart;

        public GameComponent_Harness(Game game) { }

        static void L(string s) => Log.Message("[SL-Harness] " + s);

        static void Step(string name, Action a)
        {
            try { a(); }
            catch (Exception e) { L($"EXCEPTION in step {name}: {e}"); }
        }

        static string P(string key, string def) => prm.TryGetValue(key, out string v) && v.Length > 0 ? v : def;
        static int PI(string key, int def) => int.TryParse(P(key, ""), out int v) ? v : def;
        static bool PB(string key, bool def) => P(key, def ? "1" : "0") == "1";

        public override void StartedNewGame() { DebugSettings.pauseOnError = false; }

        public override void GameComponentUpdate()
        {
            if (Current.ProgramState != ProgramState.Playing || phase >= 100) return;
            Map map = Find.CurrentMap;
            if (map == null) return;
            if (phase == 0)
            {
                // Let the colony map finish initialising before touching anything.
                if (Find.TickManager.Paused) Find.TickManager.TogglePaused();
                Find.TickManager.CurTimeSpeed = TimeSpeed.Superfast;
                if (Find.TickManager.TicksGame < 30) return;
            }
            try { Run(map); }
            catch (Exception e) { L($"EXCEPTION in phase {phase}: {e}"); phase = 90; frames = 0; }
        }

        static void Run(Map map)
        {
            frames++;
            switch (phase)
            {
                case 0:
                    Step("init", Init);
                    Step("generate", () => Generate(map));
                    phase = 1; frames = 0;
                    break;
                case 1:
                    // Static products first: they need no rendering.
                    if (lairMap != null)
                    {
                        Step("ascii", () => WriteAscii(lairMap));
                        Step("schematic", () => WriteSchematic(lairMap));
                        Step("stats", () => LogStats(lairMap));
                    }
                    phase = lairMap != null ? 2 : 90; frames = 0;
                    break;
                case 2:
                    // Switch to the lair, let its map mesh build for a few frames, then (a) if available.
                    if (frames == 1) Step("switch", SwitchToLair);
                    if (frames == 30)
                    {
                        if (PB("pr", true) && ProgressRendererLoaded()) Step("pr-start", StartProgressRenderer);
                        else L("(a) Progress Renderer not loaded or disabled, skipping");
                    }
                    if (frames > 30 && (!prRequested || PrFinished() || (DateTime.UtcNow - prStart).TotalSeconds > PI("prtimeout", 90)))
                    { phase = 3; frames = 0; }
                    break;
                case 3:
                    if (frames == 1) Step("camera", SetupCamera);
                    if (frames == 90) Step("shot", Shot);
                    if (frames > 240) { Step("shot-check", () => L($"(b) screenshot exists={File.Exists(shotPath)} path={shotPath}")); phase = 90; frames = 0; }
                    break;
                case 90:
                    if (frames < 30) break;
                    L("DONE");
                    phase = 100;
                    Root.Shutdown();
                    break;
            }
        }

        static void Init()
        {
            string baseDir = Path.Combine(GenFilePaths.SaveDataFolderPath, "SLHarness");
            outDir = Path.Combine(baseDir, "out");
            prDir = Path.Combine(outDir, "pr");
            Directory.CreateDirectory(outDir);
            Directory.CreateDirectory(prDir);
            string pf = Path.Combine(baseDir, "params.txt");
            if (File.Exists(pf))
                foreach (string line in File.ReadAllLines(pf))
                {
                    int i = line.IndexOf('=');
                    if (i > 0 && !line.TrimStart().StartsWith("#")) prm[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
                }
            L($"params: {string.Join(" ", prm.Select(kv => kv.Key + "=" + kv.Value))}");
            L($"env: screen={Screen.width}x{Screen.height} colonyMap={Find.CurrentMap.Size} biotech={ModsConfig.BiotechActive} odyssey={ModsConfig.OdysseyActive} anomaly={ModsConfig.AnomalyActive}");

            // Keep the capture clean and deterministic.
            Prefs.EdgeScreenScroll = false;
            Prefs.ZoomToMouse = false;
            Prefs.RunInBackground = true;
        }

        // ---- generation --------------------------------------------------------------------

        static void Generate(Map source)
        {
            int seed = PI("seed", 12345);
            string size = P("size", "150");
            int w, h;
            int x = size.IndexOf('x');
            if (x > 0) { w = int.Parse(size.Substring(0, x)); h = int.Parse(size.Substring(x + 1)); }
            else { w = h = int.Parse(size); }
            string defName = P("def", "SL_Lair");
            MapGeneratorDef def = DefDatabase<MapGeneratorDef>.GetNamed(defName);

            // Pocket-map seed = HashCombineInt(world seed, parent.ID) (MapGenerator.GenerateMap,
            // isPocketMap branch), so pinning both pins the map. The quicktest world seed is random.
            Find.World.info.seedString = "slharness-" + seed;

            MapPortal portal = null;
            IEnumerable<GenStepWithParams> extra = null;
            if (PB("portal", true))
                Step("portal", () =>
                {
                    portal = SpawnPortal(source);
                    if (portal != null)
                    {
                        MethodInfo m = typeof(MapPortal).GetMethod("GetExtraGenSteps", BindingFlags.NonPublic | BindingFlags.Instance);
                        extra = (IEnumerable<GenStepWithParams>)m?.Invoke(portal, null);
                    }
                });
            L($"generating {defName} size={w}x{h} seed={seed} portal={(portal != null ? portal.def.defName + " stuff=" + portal.Stuff?.defName : "none")}");

            var parent = (PocketMapParent)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.PocketMap);
            parent.ID = seed;
            parent.sourceMap = source;
            parent.mapGenerator = def;
            PocketMapUtility.currentlyGeneratingPortal = portal;
            DateTime t0 = DateTime.UtcNow;
            try
            {
                lairMap = MapGenerator.GenerateMap(new IntVec3(w, 1, h), parent, def, extra, null, isPocketMap: true);
            }
            finally { PocketMapUtility.currentlyGeneratingPortal = null; }
            Find.World.pocketMaps.Add(parent);
            L($"generated in {(DateTime.UtcNow - t0).TotalSeconds:F1}s: map id={lairMap.uniqueID} size={lairMap.Size} exitLinked={(portal != null && portal.exit != null && portal.exit.Map == lairMap)}");
        }

        static MapPortal SpawnPortal(Map source)
        {
            ThingDef pd = DefDatabase<ThingDef>.GetNamedSilentFail("SL_AncientStairwell");
            if (pd == null) { L("portal def SL_AncientStairwell not found"); return null; }
            ThingDef stuff = DefDatabase<ThingDef>.GetNamedSilentFail("BlocksGranite");
            IntVec3 c = source.Center;
            foreach (IntVec3 cell in CellRect.CenteredOn(c, 4))
            {
                foreach (Thing t in cell.GetThingList(source).ToList())
                    if (!(t is Pawn) && t.def.destroyable) t.Destroy();
            }
            Thing thing = ThingMaker.MakeThing(pd, pd.MadeFromStuff ? stuff : null);
            GenSpawn.Spawn(thing, c, source);
            return thing as MapPortal;
        }

        static void SwitchToLair()
        {
            Current.Game.CurrentMap = lairMap;
            if (PB("clearfog", true)) lairMap.fogGrid.ClearAllFog();
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            if (PB("bright", true))
            {
                // Pocket caves are unlit; without this the capture is nearly black.
                DebugViewSettings.drawLightingOverlay = false;
                DebugViewSettings.drawDarknessOverlay = false;
            }
            Find.PlaySettings.showRoofOverlay = PB("roofs", false);
            Find.WindowStack.Windows.ToList().ForEach(win => Find.WindowStack.TryRemove(win, true));
            L("switched current map to the lair");
        }

        // ---- (a) Progress Renderer ------------------------------------------------------------

        static bool ProgressRendererLoaded() =>
            LoadedModManager.RunningModsListForReading.Any(m => m.PackageIdPlayerFacing.ToLowerInvariant().StartsWith("neptimus7.progressrenderer"));

        static void SetStatic(Type t, string field, object value)
        {
            FieldInfo f = t.GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (f == null) { L($"(a) PrModSettings.{field} missing"); return; }
            f.SetValue(null, f.FieldType.IsEnum ? Enum.Parse(f.FieldType, value.ToString()) : Convert.ChangeType(value, f.FieldType));
        }

        static void StartProgressRenderer()
        {
            Type st = GenTypes.GetTypeInAnyAssembly("ProgressRenderer.PrModSettings");
            Type rm = GenTypes.GetTypeInAnyAssembly("ProgressRenderer.MapComponent_RenderManager");
            if (st == null || rm == null) { L("(a) Progress Renderer types not found"); return; }
            SetStatic(st, "ExportPath", prDir);
            SetStatic(st, "Encoding", "UnityPNG");
            SetStatic(st, "ScaleOutputImage", true);
            SetStatic(st, "OutputImageFixedHeight", PI("maxpx", 2000));
            SetStatic(st, "RenderNonPlayerHomes", true);
            SetStatic(st, "RenderFeedback", "None");
            SetStatic(st, "RenderWeather", false);
            SetStatic(st, "RenderGameConditions", false);
            SetStatic(st, "RenderZones", false);
            SetStatic(st, "RenderDesignations", false);
            MethodInfo trig = rm.GetMethod("TriggerCurrentMapManualRendering", BindingFlags.Public | BindingFlags.Static);
            if (trig == null) { L("(a) TriggerCurrentMapManualRendering missing"); return; }
            prStart = DateTime.UtcNow;
            prRequested = true;
            trig.Invoke(null, new object[] { true });
            L("(a) Progress Renderer full-map render requested -> " + prDir);
        }

        static bool PrFinished()
        {
            // The encoder runs on a worker thread; a non-empty png that stops growing is done.
            var f = new DirectoryInfo(prDir).GetFiles("*.png", SearchOption.AllDirectories).FirstOrDefault(x => x.Length > 0);
            if (f == null) return false;
            long a = f.Length;
            System.Threading.Thread.Sleep(200);
            f.Refresh();
            bool done = f.Length == a;
            if (done) L($"(a) Progress Renderer wrote {f.FullName} ({f.Length} bytes)");
            return done;
        }

        // ---- (b) zoomed-out screenshot ----------------------------------------------------------

        static void SetupCamera()
        {
            Map map = lairMap;
            float sw = Screen.width, sh = Screen.height;
            float aspect = sw / sh;
            // Orthographic: the view is 2*RootSize cells tall and 2*RootSize*aspect wide.
            float r = Math.Max(map.Size.z / 2f, map.Size.x / (2f * aspect)) + 0.5f;
            Find.CameraDriver.config.sizeRange.max = Math.Max(Find.CameraDriver.config.sizeRange.max, r + 1f);
            Find.CameraDriver.SetRootPosAndSize(new Vector3(map.Size.x / 2f, 15f, map.Size.z / 2f), r);
            Find.ScreenshotModeHandler.Active = true;
            float cellPx = sh / (2f * r);
            float cw = map.Size.x * cellPx, ch = map.Size.z * cellPx;
            // crop rect (top-left origin, screenshot pixels) for run.sh
            File.WriteAllText(Path.Combine(outDir, "crop.txt"),
                string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0} {1} {2} {3}\n",
                    (int)Math.Round((sw - cw) / 2f), (int)Math.Round((sh - ch) / 2f), (int)Math.Round(cw), (int)Math.Round(ch)));
            L($"(b) camera rootSize={r:F1} cellPx={cellPx:F2} screen={sw}x{sh} crop={(sw - cw) / 2f:F0},{(sh - ch) / 2f:F0} {cw:F0}x{ch:F0}");
        }

        static void Shot()
        {
            shotPath = Path.Combine(outDir, "sl-lair-screen.png");
            if (File.Exists(shotPath)) File.Delete(shotPath);
            L($"(b) camera now rootSize={Find.CameraDriver.RootSize:F1} pos={Find.CameraDriver.MapPosition}");
            ScreenCapture.CaptureScreenshot(shotPath);
            L("(b) ScreenCapture requested -> " + shotPath);
        }

        // ---- (d) ASCII dump -----------------------------------------------------------------

        enum K : byte { Floor, Water, Rock, Ore, Wall, Door, Building, Plant, Item, Pawn, Portal, Void }

        static readonly char[] Ch = { '.', '~', '#', '$', 'W', 'D', 'B', 'p', 'i', '@', 'E', ' ' };

        static K Classify(Map map, IntVec3 c, out Thing top)
        {
            top = null;
            TerrainDef t = c.GetTerrain(map);
            K k = t != null && t.IsWater ? K.Water : K.Floor;
            Building ed = c.GetEdifice(map);
            if (ed != null)
            {
                top = ed;
                if (ed is MapPortal || ed.def.defName.StartsWith("SL_Ancient")) return K.Portal;
                if (ed.def.building != null && ed.def.building.isNaturalRock)
                    return ed.def.building.isResourceRock ? K.Ore : K.Rock;
                if (ed.def.IsDoor) return K.Door;
                if (ed.def.passability == Traversability.Impassable) return K.Wall;
                k = K.Building;
            }
            foreach (Thing th in c.GetThingList(map))
            {
                if (th == ed) continue;
                if (th is Pawn) { top = th; return K.Pawn; }
                if (th.def.category == ThingCategory.Building && th.def.passability != Traversability.Standable) { top = th; k = K.Building; }
                else if (th.def.category == ThingCategory.Plant && k < K.Plant) { top = th; k = K.Plant; }
                else if (th.def.category == ThingCategory.Item && k < K.Item) { top = th; k = K.Item; }
            }
            return k;
        }

        static void WriteAscii(Map map)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# {map.Size.x}x{map.Size.z}, top row = max z, one char per cell. Legend: . floor/ground ~ water # rock $ ore W wall D door B building p plant i item @ pawn E portal/exit");
            for (int z = map.Size.z - 1; z >= 0; z--)
            {
                for (int x = 0; x < map.Size.x; x++) sb.Append(Ch[(int)Classify(map, new IntVec3(x, 0, z), out _)]);
                sb.Append(" z=").Append(z).AppendLine();
            }
            File.WriteAllText(Path.Combine(outDir, "sl-lair.ascii.txt"), sb.ToString());
            L("(d) ASCII dump written");
        }

        // ---- (c) schematic PNG (pure managed) -------------------------------------------------------

        static byte B(float v) => (byte)Math.Max(0, Math.Min(255, (int)(v * 255f + 0.5f)));

        static void WriteSchematic(Map map)
        {
            int target = PI("maxpx", 2000);
            int w = map.Size.x, h = map.Size.z;
            int k = Math.Max(2, target / Math.Max(w, h));
            int W = w * k, H = h * k;
            byte[] rgb = new byte[W * H * 3];
            for (int cz = 0; cz < h; cz++)
                for (int cx = 0; cx < w; cx++)
                {
                    var c = new IntVec3(cx, 0, cz);
                    K kind = Classify(map, c, out Thing top);
                    TerrainDef t = c.GetTerrain(map);
                    float tr = 0.45f, tg = 0.40f, tb = 0.35f;
                    if (t != null) { tr = t.color.r; tg = t.color.g; tb = t.color.b; }
                    float r = tr * 0.8f + 0.1f, g = tg * 0.8f + 0.1f, b = tb * 0.8f + 0.1f; // terrain base
                    switch (kind)
                    {
                        case K.Water: r = 0.15f; g = 0.35f; b = 0.75f; break;
                        case K.Rock: r = 0.18f; g = 0.19f; b = 0.21f; break;
                        case K.Ore: r = 0.85f; g = 0.75f; b = 0.20f; break;
                        case K.Wall: r = 0.55f; g = 0.30f; b = 0.18f; break;
                        case K.Door: r = 0.95f; g = 0.85f; b = 0.30f; break;
                        case K.Building: r = 0.90f; g = 0.50f; b = 0.15f; break;
                        case K.Plant: r = 0.20f; g = 0.65f; b = 0.25f; break;
                        case K.Item: r = 0.95f; g = 0.95f; b = 0.95f; break;
                        case K.Pawn: r = 0.95f; g = 0.15f; b = 0.15f; break;
                        case K.Portal: r = 0.85f; g = 0.15f; b = 0.85f; break;
                    }
                    int py0 = (h - 1 - cz) * k; // image row 0 = max z
                    for (int dy = 0; dy < k; dy++)
                        for (int dx = 0; dx < k; dx++)
                        {
                            float m = 1f;
                            if (k >= 6 && (dx == 0 || dy == 0)) m = 0.80f;                 // cell grid
                            if (cx % 10 == 0 && dx == 0) m = 0.45f;                        // 10-cell column lines
                            if (cz % 10 == 9 && dy == 0) m = 0.45f;                        // 10-cell row lines
                            int o = ((py0 + dy) * W + cx * k + dx) * 3;
                            rgb[o] = B(r * m); rgb[o + 1] = B(g * m); rgb[o + 2] = B(b * m);
                        }
                }
            string path = Path.Combine(outDir, "sl-lair.schematic.png");
            File.WriteAllBytes(path, EncodePng(rgb, W, H));
            L($"(c) schematic PNG {W}x{H} ({k}px/cell) -> {path}");
        }

        static void LogStats(Map map)
        {
            var counts = new int[Ch.Length];
            for (int z = 0; z < map.Size.z; z++)
                for (int x = 0; x < map.Size.x; x++) counts[(int)Classify(map, new IntVec3(x, 0, z), out _)]++;
            L("stats: " + string.Join(" ", Enum.GetValues(typeof(K)).Cast<K>().Select(e => $"{e}={counts[(int)e]}")));
            // Flood fill of walkable cells from the player start (the exit) = is the lair connected?
            Thing exitThing = map.listerThings.AllThings.FirstOrDefault(t => t is PocketMapExit);
            IntVec3 start = exitThing != null ? exitThing.Position : map.Center;
            int walk = 0;
            foreach (IntVec3 c in map.AllCells) if (c.Walkable(map)) walk++;
            var seen = new HashSet<int>();
            var q = new Queue<IntVec3>();
            if (start.InBounds(map) && start.Walkable(map)) { q.Enqueue(start); seen.Add(map.cellIndices.CellToIndex(start)); }
            while (q.Count > 0)
            {
                IntVec3 c = q.Dequeue();
                for (int i = 0; i < 4; i++)
                {
                    IntVec3 n = c + GenAdj.CardinalDirections[i];
                    if (!n.InBounds(map) || !n.Walkable(map)) continue;
                    if (seen.Add(map.cellIndices.CellToIndex(n))) q.Enqueue(n);
                }
            }
            L($"connectivity: start={start} walkable={walk} reachableFromStart={seen.Count}");
        }

        // ---- minimal PNG writer ----------------------------------------------------------------------

        static uint[] crcTable;

        static uint Crc(byte[] type, byte[] data)
        {
            if (crcTable == null)
            {
                crcTable = new uint[256];
                for (uint n = 0; n < 256; n++)
                {
                    uint c = n;
                    for (int j = 0; j < 8; j++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                    crcTable[n] = c;
                }
            }
            uint crc = 0xFFFFFFFFu;
            foreach (byte b in type) crc = crcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
            foreach (byte b in data) crc = crcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
            return crc ^ 0xFFFFFFFFu;
        }

        static void Chunk(Stream s, string type, byte[] data)
        {
            byte[] t = Encoding.ASCII.GetBytes(type);
            WriteBE(s, (uint)data.Length);
            s.Write(t, 0, 4);
            s.Write(data, 0, data.Length);
            WriteBE(s, Crc(t, data));
        }

        static void WriteBE(Stream s, uint v)
        {
            s.WriteByte((byte)(v >> 24)); s.WriteByte((byte)(v >> 16)); s.WriteByte((byte)(v >> 8)); s.WriteByte((byte)v);
        }

        static byte[] EncodePng(byte[] rgb, int w, int h)
        {
            var ms = new MemoryStream();
            ms.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);
            var ihdr = new MemoryStream();
            WriteBE(ihdr, (uint)w); WriteBE(ihdr, (uint)h);
            ihdr.Write(new byte[] { 8, 2, 0, 0, 0 }, 0, 5); // 8-bit RGB
            Chunk(ms, "IHDR", ihdr.ToArray());
            var raw = new MemoryStream();
            using (var z = new ZLibStream(raw, CompressionLevel.Fastest, true))
            {
                for (int y = 0; y < h; y++)
                {
                    z.WriteByte(0); // filter: none
                    z.Write(rgb, y * w * 3, w * 3);
                }
            }
            Chunk(ms, "IDAT", raw.ToArray());
            Chunk(ms, "IEND", new byte[0]);
            return ms.ToArray();
        }
    }
}
