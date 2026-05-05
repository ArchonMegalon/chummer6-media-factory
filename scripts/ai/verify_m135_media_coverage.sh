#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"

tmp_dir="$(mktemp -d)"
trap 'rm -rf "$tmp_dir"' EXIT

python3 -m unittest   tests.test_m135_media_coverage_proof

dotnet run   --project Chummer.Media.Factory.Runtime.Verify/Chummer.Media.Factory.Runtime.Verify.csproj   --configuration Release   --nologo   --verbosity quiet

python3 scripts/ai/materialize_media_release_proof.py   --out-dir "$tmp_dir"   --status passed >/dev/null

python3 - "$tmp_dir" <<'PY'
from pathlib import Path
import json
import sys

package_id = 'next90-m135-media-factory-close-media-contracts-render-jobs-previews-manifests-arc'
repo_root = Path.cwd()
tmp_dir = Path(sys.argv[1])

def load_package(path: Path) -> dict:
    payload = json.loads(path.read_text(encoding='utf-8'))
    return next(candidate for candidate in payload['successor_packages'] if candidate['package_id'] == package_id)

expected_release = load_package(tmp_dir / 'MEDIA_LOCAL_RELEASE_PROOF.generated.json')
expected_certification = load_package(tmp_dir / 'ARTIFACT_PUBLICATION_CERTIFICATION.generated.json')
published_release = load_package(repo_root / '.codex-studio/published/MEDIA_LOCAL_RELEASE_PROOF.generated.json')
published_certification = load_package(repo_root / '.codex-studio/published/ARTIFACT_PUBLICATION_CERTIFICATION.generated.json')

if published_release != expected_release:
    raise SystemExit('verify failed: published MEDIA_LOCAL_RELEASE_PROOF.generated.json drifted from the current M135 package entry')

if published_certification != expected_certification:
    raise SystemExit('verify failed: published ARTIFACT_PUBLICATION_CERTIFICATION.generated.json drifted from the current M135 package entry')
PY

rg -n   'next90-m135-media-factory-close-media-contracts-render-jobs-previews-manifests-arc|4720040715|verify_closed_package_only|unlanded|close_media_contracts_render_jobs:media_factory|published release proof and artifact publication certification must match the freshly materialized M135 package entry exactly|media coverage package authority requires exactly one canonical queue row per mirror and exactly one registry task block|proof citations stay anchored to repo-local `src`, `tests`, `docs`, and `scripts` paths'   "$tmp_dir/MEDIA_LOCAL_RELEASE_PROOF.generated.json"   "$tmp_dir/ARTIFACT_PUBLICATION_CERTIFICATION.generated.json"   docs/NEXT90_M135_MEDIA_COVERAGE_PROOF_FLOOR.md >/dev/null

if rg -ni   'TASK_LOCAL_TELEMETRY\.generated\.json|ACTIVE_RUN_HANDOFF\.generated\.md|operator[[:space:]]+telemetry|supervisor[[:space:]]+status|status[[:space:]]+query|eta[[:space:]]+query|active-run[[:space:]]+helper'   docs/NEXT90_M135_MEDIA_COVERAGE_PROOF_FLOOR.md   scripts/ai/verify_m135_media_coverage.sh   scripts/ai/materialize_media_release_proof.py >/dev/null; then
  echo 'verify failed: M135 media coverage proof sources must stay worker-safe and must not cite blocked run-helper context' >&2
  exit 1
fi
