#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"

tmp_dir="$(mktemp -d)"
trap 'rm -rf "$tmp_dir"' EXIT

python3 -m unittest \
  tests.test_m109_successor_package_authority \
  tests.test_m109_build_explain_proof \
  tests.test_build_explain_companion_rendering

dotnet run \
  --project tests/BuildExplainCompanionSmoke/Chummer.Media.Factory.BuildExplainCompanionSmoke.csproj \
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

package_id = "next90-m109-media-factory-build-explain-bundles"
repo_root = Path.cwd()
tmp_dir = Path(sys.argv[1])

def require_unique_package_ids(path: Path, payload: dict) -> None:
    packages = payload["successor_packages"]
    seen: set[str] = set()
    duplicates: set[str] = set()
    for candidate in packages:
        package_id = candidate["package_id"]
        if package_id in seen:
            duplicates.add(package_id)
        seen.add(package_id)

    if duplicates:
        raise SystemExit(
            f"verify failed: {path.name} repeated successor package ids: {', '.join(sorted(duplicates))}"
        )

def load_package(path: Path) -> dict:
    payload = json.loads(path.read_text(encoding="utf-8"))
    require_unique_package_ids(path, payload)
    matches = [candidate for candidate in payload["successor_packages"] if candidate["package_id"] == package_id]
    if len(matches) != 1:
        raise SystemExit(
            f"verify failed: expected exactly one {package_id} successor package entry in {path.name}, found {len(matches)}"
        )
    return matches[0]

expected_release = load_package(tmp_dir / "MEDIA_LOCAL_RELEASE_PROOF.generated.json")
expected_certification = load_package(tmp_dir / "ARTIFACT_PUBLICATION_CERTIFICATION.generated.json")
published_release = load_package(repo_root / ".codex-studio/published/MEDIA_LOCAL_RELEASE_PROOF.generated.json")
published_certification = load_package(repo_root / ".codex-studio/published/ARTIFACT_PUBLICATION_CERTIFICATION.generated.json")

for package_name, package in (
    ("generated release proof", expected_release),
    ("generated publication certification", expected_certification),
    ("published release proof", published_release),
    ("published publication certification", published_certification),
):
    for proof_path in package["proof"]:
        if not proof_path.startswith(("src/", "tests/", "docs/", "scripts/")):
            raise SystemExit(
                f"verify failed: {package_name} cited proof outside the M109 allowed paths: {proof_path}"
            )

if published_release != expected_release:
    raise SystemExit("verify failed: published MEDIA_LOCAL_RELEASE_PROOF.generated.json drifted from the current M109 package entry")

if published_certification != expected_certification:
    raise SystemExit("verify failed: published ARTIFACT_PUBLICATION_CERTIFICATION.generated.json drifted from the current M109 package entry")
PY

rg -n \
  'next90-m109-media-factory-build-explain-bundles|4037265286|BuildExplainCompanionReadyRef|BuildExplainCompanionRoleReceiptGroup|verify_closed_package_only|7d5a0167|"status":[[:space:]]+"complete"|build explain package authority requires exactly one canonical queue row per mirror and exactly one registry task block|canonical successor queue rows are now complete with `landed_commit: 7d5a0167`, so future slices can close this package only from the landed proof floor.|queue and registry mirrors must match the canonical M109 package and task blocks exactly|proof citations stay anchored to repo-local src, tests, docs, and scripts paths so closed-package evidence cannot drift into sibling surfaces' \
  "$tmp_dir/MEDIA_LOCAL_RELEASE_PROOF.generated.json" \
  "$tmp_dir/ARTIFACT_PUBLICATION_CERTIFICATION.generated.json" \
  docs/NEXT90_M109_BUILD_EXPLAIN_COMPANION_PROOF_FLOOR.md >/dev/null

rg -n \
  'build explain companion receipts stay render-verified|null artifact lists and null sibling entries|approved explain packet id or explain packet revision id|JSON and keyed text build explain scope values trim surrounding whitespace|build explain caption and preview refs dedupe case-insensitively|trims surrounding whitespace and rejects case-insensitive duplicate companion refs|duplicate companion refs inside one approved explain packet|source or requested timestamp drift cannot fork stable job ids, receipt ids, ready refs, or grouped role receipts' \
  docs/MEDIA_CAPABILITY_SIGNOFF.md >/dev/null

if rg -ni \
  'TASK_LOCAL_TELEMETRY\.generated\.json|ACTIVE_RUN_HANDOFF\.generated\.md|operator[[:space:]]+telemetry|supervisor[[:space:]]+status|status[[:space:]]+query|eta[[:space:]]+query|active-run[[:space:]]+helper' \
  docs/NEXT90_M109_BUILD_EXPLAIN_COMPANION_PROOF_FLOOR.md \
  scripts/ai/verify_m109_build_explain_companion.sh \
  scripts/ai/materialize_media_release_proof.py \
  src/Chummer.Media.Factory.Runtime/Assets/BuildExplainCompanionRenderingService.cs >/dev/null; then
  echo "verify failed: M109 build explain proof sources must stay worker-safe and must not cite blocked run-helper context" >&2
  exit 1
fi
