#!/usr/bin/env bash
set -euo pipefail
python3 - <<'PY'
from pathlib import Path
root = Path('/docker/fleet/repos/chummer-media-factory')
bad = []
for path in root.rglob('*'):
    if path.is_dir() or path.suffix.lower() not in {'.json','.md','.yaml','.yml'}:
        continue
    text = path.read_text(encoding='utf-8', errors='ignore').lower()
    for token in ('release ready', 'all platforms', 'replacement for chummer5a'):
        if token in text:
            bad.append(f"{path}: contains '{token}'")
if bad:
    print("\n".join(bad[:200]))
    raise SystemExit(1)
print('media claims ok')
PY
