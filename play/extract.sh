#!/usr/bin/env bash
# Copies one run's engine and world out of the private archive, as they were
# when the run ended: the engine from the last commit that was live, the world
# from the files the live world was actually reading.
#
#   play/extract.sh <run> <name>      e.g. play/extract.sh genesis-v2-opus-5-5 genesis-v2
#
# Writes engines/<name>/Og.Core and play/<name>/world.json. ARCHIVE_DIR points at
# og-world-archive (default: next to this repository).
set -euo pipefail
RUN="$1"; NAME="$2"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ARCHIVE="${ARCHIVE_DIR:-$ROOT/../og-world-archive}/runs/$RUN"
BUNDLE="$ARCHIVE/world-repo.bundle"
[ -f "$BUNDLE" ] || { echo "no repository bundle for $RUN" >&2; exit 1; }

# The last deploy or reload is what players were on when the run ended.
SHA="$(grep -E ' (deploy|reload|rollback) ' "$ARCHIVE/deploys.log" | tail -1 | awk '{print $3}')"
TMP="$(mktemp -d)"; trap 'rm -rf "$TMP"' EXIT
git clone -q --no-checkout "$BUNDLE" "$TMP/repo"
FULL="$(git -C "$TMP/repo" rev-parse --verify "$SHA^{commit}")" || { echo "$SHA is not in the bundle" >&2; exit 1; }

rm -rf "$ROOT/engines/$NAME/Og.Core"
mkdir -p "$ROOT/engines/$NAME" "$ROOT/play/$NAME"
git -C "$TMP/repo" archive "$FULL" src/Og.Core | tar -x -C "$TMP"
mv "$TMP/src/Og.Core" "$ROOT/engines/$NAME/Og.Core"
# The world the live server was reading, where the archive kept it; otherwise the
# world as committed at the same point.
if [ -f "$ARCHIVE/world/maps/starter.json" ]; then
    cp "$ARCHIVE/world/maps/starter.json" "$ROOT/play/$NAME/world.json"
    FRAGMENTS="$ARCHIVE/world/maps/starter.d"
else
    git -C "$TMP/repo" show "$FULL:world/maps/starter.json" > "$ROOT/play/$NAME/world.json"
    git -C "$TMP/repo" archive "$FULL" world/maps | tar -x -C "$TMP"
    FRAGMENTS="$TMP/world/maps/starter.d"
fi
rm -rf "$ROOT/play/$NAME/starter.d"
if compgen -G "$FRAGMENTS/*.json" >/dev/null; then
    mkdir -p "$ROOT/play/$NAME/starter.d"
    cp "$FRAGMENTS"/*.json "$ROOT/play/$NAME/starter.d/"
    echo "$NAME: fragments $(cd "$ROOT/play/$NAME/starter.d" && ls | tr '\n' ' ')"
fi
printf '%s %s\n' "$RUN" "$FULL" > "$ROOT/engines/$NAME/SOURCE"
echo "$NAME: engine at ${FULL:0:12}, world $(wc -c < "$ROOT/play/$NAME/world.json" | xargs) bytes"
