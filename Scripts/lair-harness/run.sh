#!/usr/bin/env bash
# One-command visual check loop for the SL_Lair pocket map.
#   Scripts/lair-harness/run.sh [seed] [size]     e.g.  run.sh 4242 150
# Env switches:
#   SL_DLC=royalty,ideology,anomaly,odyssey   extra DLC to activate (Biotech is always on)
#   SL_PR=1                                   also activate Harmony + Progress Renderer, mode (a)
#   SL_MODS=id1,id2                           extra mod packageIds, loaded in this order before the lair
#   SL_PORTAL=0                               do not spawn SL_AncientStairwell on the colony map
#   SL_MAXPX=2000                             long side of outputs      SL_TIMEOUT=300  seconds
#   SL_DEF=SL_Lair                            MapGeneratorDef to generate
#   SL_ROOFS=1 / SL_BRIGHT=0 / SL_CLEARFOG=0  render toggles (see Harness.cs)
# Outputs: $SL_HARNESS_WORK/out/, by default /tmp/sl-harness/out/ (view.png is the one to look at)
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WORK="${SL_HARNESS_WORK:-/tmp/sl-harness}"
STATE=$WORK/state
OUT=$WORK/out
RW="${RIMWORLD_PATH:-/mnt/c/Program Files (x86)/Steam/steamapps/common/RimWorld}"
CFGDIR="$(ls -d /mnt/c/Users/*/AppData/LocalLow/Ludeon\ Studios/RimWorld\ by\ Ludeon\ Studios | head -1)"
CFG="$CFGDIR/Config/ModsConfig.xml"
LOG="$CFGDIR/Logs/Player.log"
EXE="RimWorld.exe"
HMOD="$RW/Mods/SLHarness"
SEED="${1:-${SL_SEED:-12345}}"
SIZE="${2:-${SL_SIZE:-150}}"
MAXPX="${SL_MAXPX:-2000}"
TIMEOUT="${SL_TIMEOUT:-300}"
mkdir -p "$STATE" "$OUT"

running() { tasklist.exe 2>/dev/null | grep -i "RimWorld" >/dev/null; }
restore() {
  # Idempotent: only acts while a backup is pending.
  if [ -f "$STATE/pending" ]; then
    taskkill.exe /IM "$EXE" /F >/dev/null 2>&1 || true
    cp "$STATE/ModsConfig.xml.orig" "$CFG"
    rm -rf "$HMOD" "$CFGDIR/SLHarness"
    rm -f "$STATE/pending"
    echo "[run.sh] ModsConfig.xml restored"
  fi
}
# A previous run that died hard (kill -9, WSL restart) leaves "pending": undo it first.
if [ -f "$STATE/pending" ] && ! running; then echo "[run.sh] recovering a crashed previous run"; restore; fi
if running; then echo "RimWorld is already running; close it first" >&2; exit 2; fi
[ -d "$RW/Mods/SanguophageLair" ] || { echo "Sanguophage Lair is not deployed to $RW/Mods (build the mod first)" >&2; exit 1; }

dotnet build "$HERE/SLHarness.csproj" -c Release -p:RimWorldPath="$RW" 2>&1 | grep -E "error|Build succeeded" || true
[ -f "$HERE/bin/SLHarness.dll" ] || { echo "harness build failed" >&2; exit 1; }

# Pin the mod list. Order: core, DLC, (Harmony + PR), SL, harness. Keeps the game's own version stamp.
cp "$CFG" "$STATE/ModsConfig.xml.orig"
touch "$STATE/pending"
trap restore EXIT INT TERM HUP
PR_IDS=""; [ "${SL_PR:-0}" = "1" ] && PR_IDS="brrainz.harmony,neptimus7.progressrenderer"
[ -n "${SL_MODS:-}" ] && PR_IDS="${PR_IDS:+$PR_IDS,}$SL_MODS"
python3 -I - "$STATE/ModsConfig.xml.orig" "$CFG" "${SL_DLC:-}" "$PR_IDS" <<'PY'
import re, sys
src, dst, dlc, pr = sys.argv[1:5]
text = open(src, encoding="utf-8-sig").read()
version = re.search(r"<version>(.*?)</version>", text).group(1)
known = re.search(r"<knownExpansions>.*?</knownExpansions>", text, re.S)
ids = ["ludeon.rimworld", "ludeon.rimworld.biotech"]
for d in filter(None, dlc.split(",")):
    ids.append("ludeon.rimworld." + d.strip().lower())
# Biotech first-class in the DLC order the game expects: royalty, ideology, biotech, anomaly, odyssey.
order = ["ludeon.rimworld", "ludeon.rimworld.royalty", "ludeon.rimworld.ideology", "ludeon.rimworld.biotech",
         "ludeon.rimworld.anomaly", "ludeon.rimworld.odyssey"]
ids = sorted(set(ids), key=order.index)
ids += [i for i in pr.split(",") if i]
ids += ["shunter.sanguophagelair", "shunter.slharness"]
body = "\n".join(f"    <li>{i}</li>" for i in ids)
open(dst, "w", encoding="utf-8").write(
    f'<?xml version="1.0" encoding="utf-8"?>\n<ModsConfigData>\n  <version>{version}</version>\n'
    f'  <activeMods>\n{body}\n  </activeMods>\n  {known.group(0) if known else "<knownExpansions />"}\n</ModsConfigData>\n')
PY

# Install the harness mod + params (the game reads them from the Windows side).
mkdir -p "$HMOD/About" "$HMOD/Assemblies" "$CFGDIR/SLHarness"
cp "$HERE/About.xml" "$HMOD/About/About.xml"
cp "$HERE/bin/SLHarness.dll" "$HMOD/Assemblies/"
cat > "$CFGDIR/SLHarness/params.txt" <<P
seed=$SEED
size=$SIZE
maxpx=$MAXPX
def=${SL_DEF:-SL_Lair}
portal=${SL_PORTAL:-1}
pr=${SL_PR:-0}
roofs=${SL_ROOFS:-0}
bright=${SL_BRIGHT:-1}
clearfog=${SL_CLEARFOG:-1}
P
rm -rf "$CFGDIR/SLHarness/out"
[ -f "$LOG" ] && mv -f "$LOG" "$LOG.prev"

START=$(date +%s)
( cd "$RW" && "./$EXE" -quicktest >/dev/null 2>&1 & )
echo "[run.sh] launched (seed=$SEED size=$SIZE dlc=${SL_DLC:-none} pr=${SL_PR:-0}); waiting up to ${TIMEOUT}s"
elapsed=0
while [ "$elapsed" -lt "$TIMEOUT" ]; do
  sleep 3; elapsed=$((elapsed + 3))
  if grep -q "\[SL-Harness\] DONE" "$LOG" 2>/dev/null; then break; fi
  if [ "$elapsed" -gt 30 ] && ! running; then echo "[run.sh] game exited before DONE"; break; fi
done
# Let the game finish Application.Quit on its own, then collect, then restore config.
for _ in $(seq 1 15); do running || break; sleep 2; done
rm -rf "$OUT"; mkdir -p "$OUT"
grep "SL-Harness" "$LOG" > "$OUT/harness.log" 2>/dev/null || true
cp "$LOG" "$OUT/Player-full.log" 2>/dev/null || true
if [ -d "$CFGDIR/SLHarness/out" ]; then cp -r "$CFGDIR/SLHarness/out/." "$OUT/"; fi
restore

# Post-process: crop the screenshot to the map rectangle and shrink to MAXPX on the long side.
python3 -I - "$OUT" "$MAXPX" <<'PY2' || echo "[run.sh] post-process failed (PIL missing?)"
import os, sys
from PIL import Image
out, maxpx = sys.argv[1], int(sys.argv[2])
def shrink(im):
    s = maxpx / max(im.size)
    return im.resize((round(im.width * s), round(im.height * s)), Image.LANCZOS) if s < 1 else im
shot, crop = f"{out}/sl-lair-screen.png", f"{out}/crop.txt"
if os.path.exists(shot) and os.path.exists(crop):
    x, y, w, h = map(int, open(crop).read().split())
    im = Image.open(shot).convert("RGB")
    im = im.crop((max(0, x), max(0, y), min(im.width, x + w), min(im.height, y + h)))
    shrink(im).save(f"{out}/view.png")
    print("[run.sh] mode (b) rendered view ->", f"{out}/view.png")
for root, _, files in os.walk(f"{out}/pr"):
    for f in files:
        if f.lower().endswith(".png"):
            shrink(Image.open(os.path.join(root, f)).convert("RGB")).save(f"{out}/view-pr.png")
            print("[run.sh] mode (a) Progress Renderer view ->", f"{out}/view-pr.png")
            break
PY2
[ -f "$OUT/sl-lair.schematic.png" ] && echo "[run.sh] mode (c) schematic -> $OUT/sl-lair.schematic.png"
[ -f "$OUT/sl-lair.ascii.txt" ] && echo "[run.sh] mode (d) ascii -> $OUT/sl-lair.ascii.txt"
echo "[run.sh] log -> $OUT/harness.log"
grep -E "EXCEPTION|generated in|connectivity|stats:" "$OUT/harness.log" || true
echo "[run.sh] finished after $(( $(date +%s) - START ))s"
