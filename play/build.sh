#!/usr/bin/env bash
# Builds the browser engine against one run's Og.Core.
#
#   play/build.sh <dir holding Og.Core.csproj> <output dir>
#
# The output dir receives engine/: the runtime files the player page loads. They
# go in engine/ rather than _framework/, which some hosts treat as private.
set -euo pipefail
ENGINE_DIR="$(cd "$1" && pwd)"
OUT="$2"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

# Build from a copy, so the engine's own directory is left without bin/ and obj/.
cp -R "$ENGINE_DIR" "$TMP/Og.Core"
dotnet publish "$HERE/engine" -c Release -o "$TMP/out" -p:EngineDir="$TMP/Og.Core" -nologo -clp:ErrorsOnly
rm -rf "$OUT/engine"
mkdir -p "$OUT/engine"
find "$TMP/out/wwwroot/_framework" -type f ! -name '*.br' ! -name '*.gz' -exec cp {} "$OUT/engine/" \;
echo "built $(ls "$OUT/engine" | wc -l | xargs) files into $OUT/engine"
