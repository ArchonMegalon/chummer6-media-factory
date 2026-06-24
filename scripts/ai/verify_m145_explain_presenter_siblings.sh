#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"

tmp_dir="$(mktemp -d)"
trap 'rm -rf "$tmp_dir"' EXIT

python3 -m unittest \
  tests.test_m145_successor_package_authority \
  tests.test_m145_explain_presenter_proof \
  tests.test_explain_presenter_sibling_rendering

dotnet run \
  --project tests/ExplainPresenterSiblingSmoke/Chummer.Media.Factory.ExplainPresenterSiblingSmoke.csproj \
  --configuration Release \
  --nologo \
  --verbosity quiet

python3 scripts/ai/materialize_media_release_proof.py \
  --out-dir "$tmp_dir" \
  --status passed >/dev/null

python3 - "$tmp_dir" <<'PY'
from pathlib import Path
import json
import sys

package_id = "next90-m145-media-factory-explain-presenter-siblings"
repo_root = Path.cwd()
tmp_dir = Path(sys.argv[1])

def load_package(path: Path) -> dict:
    payload = json.loads(path.read_text(encoding="utf-8"))
    return next(candidate for candidate in payload["successor_packages"] if candidate["package_id"] == package_id)

expected_release = load_package(tmp_dir / "MEDIA_LOCAL_RELEASE_PROOF.generated.json")
expected_certification = load_package(tmp_dir / "ARTIFACT_PUBLICATION_CERTIFICATION.generated.json")
published_release = load_package(repo_root / ".codex-studio/published/MEDIA_LOCAL_RELEASE_PROOF.generated.json")
published_certification = load_package(repo_root / ".codex-studio/published/ARTIFACT_PUBLICATION_CERTIFICATION.generated.json")

if published_release != expected_release:
    raise SystemExit("verify failed: published MEDIA_LOCAL_RELEASE_PROOF.generated.json drifted from the current M145 package entry")

if published_certification != expected_certification:
    raise SystemExit("verify failed: published ARTIFACT_PUBLICATION_CERTIFICATION.generated.json drifted from the current M145 package entry")
PY

rg -n \
  'next90-m145-media-factory-explain-presenter-siblings|2090633046|ExplainPresenterTextFallbackReceipt|ExplainPresenterSiblingRoleReceiptGroup|verify_closed_package_only|7d5a0167|"status":[[:space:]]+"complete"|explain presenter package authority requires exactly one canonical queue row per mirror and exactly one registry task block|canonical successor queue rows are now complete with `landed_commit: 7d5a0167`, so future slices can close this package only from the landed proof floor.|queue and registry mirrors must match the canonical M145 package and task blocks exactly' \
  "$tmp_dir/MEDIA_LOCAL_RELEASE_PROOF.generated.json" \
  "$tmp_dir/ARTIFACT_PUBLICATION_CERTIFICATION.generated.json" \
  docs/NEXT90_M145_EXPLAIN_PRESENTER_SIBLINGS_PROOF_FLOOR.md >/dev/null

rg -n \
  'explain presenter sibling receipts stay render-verified|first-party text fallback|grounding-scope fields are missing|caption refs while presenter-video siblings require preview refs|duplicate companion refs inside one approved explanation packet|source or requested timestamp drift cannot fork stable job ids|rendered timestamps resolve from completed media jobs' \
  docs/MEDIA_CAPABILITY_SIGNOFF.md >/dev/null

if rg -ni \
  'TASK_LOCAL_TELEMETRY\.generated\.json|ACTIVE_RUN_HANDOFF\.generated\.md|operator[[:space:]]+telemetry|supervisor[[:space:]]+status|status[[:space:]]+query|eta[[:space:]]+query|active-run[[:space:]]+helper' \
  docs/NEXT90_M145_EXPLAIN_PRESENTER_SIBLINGS_PROOF_FLOOR.md \
  scripts/ai/verify_m145_explain_presenter_siblings.sh \
  scripts/ai/materialize_media_release_proof.py \
  src/Chummer.Media.Factory.Runtime/Assets/ExplainPresenterSiblingRenderingService.cs >/dev/null; then
  echo "verify failed: M145 explain presenter proof sources must stay worker-safe and must not cite blocked run-helper context" >&2
  exit 1
fi
