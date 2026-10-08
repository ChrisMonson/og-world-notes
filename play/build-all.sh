#!/usr/bin/env bash
# Builds the browser engine for every run in engines/, into play/<run>/engine.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
for dir in "$ROOT"/engines/*/; do
    name="$(basename "$dir")"
    echo "== $name"
    "$ROOT/play/build.sh" "$dir/Og.Core" "$ROOT/play/$name"
done
