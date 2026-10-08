#!/usr/bin/env bash
# Copies what had happened to one run's world by the time it ended -- the dead,
# words left in places, what lay on the floor -- out of its database, as the
# server last saved it.
#
#   play/extract-state.sh <run> <name>     writes play/<name>/state.json
#
# A database can hold several worlds' states (an earlier world the run renamed
# or replaced); the one wanted is the one named in play/<name>/world.json.
set -euo pipefail
RUN="$1"; NAME="$2"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DB="${ARCHIVE_DIR:-$ROOT/../og-world-archive}/runs/$RUN/world.db"
WORLD="$(python3 -c "import json,sys; print(json.load(open(sys.argv[1]))['name'])" "$ROOT/play/$NAME/world.json")"
sqlite3 -readonly "$DB" "select snapshot from world_state where world = '$WORLD'" > "$ROOT/play/$NAME/state.json"
[ -s "$ROOT/play/$NAME/state.json" ] || { rm -f "$ROOT/play/$NAME/state.json"; echo "$NAME: no saved state for world '$WORLD'" >&2; exit 1; }
python3 - "$ROOT/play/$NAME/state.json" "$NAME" <<'PY'
import json, sys
s = json.load(open(sys.argv[1]))
count = lambda key: sum(len(v) for v in (s.get(key) or {}).values())
print(f"{sys.argv[2]}: dead {count('Dead')}, words {count('Spoken')}, survivors {count('Survivors')}, "
      f"ground items {count('GroundItems')}, changed descriptions {len(s.get('LocationDescriptions') or {})}")
PY
