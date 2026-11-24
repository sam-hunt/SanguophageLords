#!/usr/bin/env bash
# Retry wrapper around run.sh: the 1.7 game sometimes freezes during startup (its log stops
# right after "[SL-Harness] mod loaded"), and a second launch then works.
#   Scripts/lair-harness/try.sh [seed] [size]     (env switches as for run.sh)
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WORK="${SL_HARNESS_WORK:-/tmp/sl-harness}"
mkdir -p "$WORK"
for attempt in 1 2 3 4; do
  SL_TIMEOUT="${SL_TIMEOUT:-100}" "$HERE/run.sh" "$@" > "$WORK/last-run.txt" 2>&1
  if grep -q "DONE" "$WORK/out/harness.log" 2>/dev/null; then
    grep -E "exitLinked|stats|connectivity|finished" "$WORK/last-run.txt" | cut -c1-260
    echo "[try.sh] ok on attempt $attempt"
    exit 0
  fi
  echo "[try.sh] attempt $attempt froze at startup, retrying"
done
exit 1
