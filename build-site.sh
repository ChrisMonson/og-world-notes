#!/usr/bin/env bash
# Assembles the published site in _site/: the explorer, its content, the logs and
# the playable worlds, without the sources that only build them. The site is
# plain static files; nothing here is processed.
#
#     ./build-site.sh            (after play/build-all.sh, for playable worlds)
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUT="$ROOT/_site"
rm -rf "${OUT:?}"
mkdir -p "$OUT"
# rsync takes the first rule that matches, so what to leave out comes first.
rsync -a \
  --exclude='/log/*.py' --exclude='/play/engine/' --exclude='/play/dev/' --exclude='/play/*.sh' \
  --exclude='bin/' --exclude='obj/' --exclude='.DS_Store' \
  --include='/index.html' --include='/assets/***' --include='/content/***' \
  --include='/runs/***' --include='/log/***' --include='/play/***' \
  --exclude='*' \
  "$ROOT/" "$OUT/"
touch "$OUT/.nojekyll"
echo "site: $(find "$OUT" -type f | wc -l | tr -d ' ') files, $(du -sh "$OUT" | cut -f1)"
