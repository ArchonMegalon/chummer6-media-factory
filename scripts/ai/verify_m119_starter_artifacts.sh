#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"

tmp_dir="$(mktemp -d)"
trap 'rm -rf "$tmp_dir"' EXIT

python3 -m unittest \
  tests.test_m119_successor_package_authority \
  tests.test_m119_starter_artifact_proof \
  tests.test_starter_artifact_rendering

dotnet run \
  --project tests/StarterArtifactBundleSmoke/Chummer.Media.Factory.StarterArtifactBundleSmoke.csproj \
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

package_id = "next90-m119-media-factory-starter-artifacts"
repo_root = Path.cwd()
tmp_dir = Path(sys.argv[1])

def require_unique_package_ids(path: Path, payload: dict) -> None:
    packages = payload["successor_packages"]
    seen: set[str] = set()
    duplicates: set[str] = set()
    for candidate in packages:
        candidate_id = candidate["package_id"]
        if candidate_id in seen:
            duplicates.add(candidate_id)
        seen.add(candidate_id)

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

def require_unique_strings(package_name: str, field_name: str, values: list[str]) -> None:
    seen: set[str] = set()
    duplicates: set[str] = set()
    for value in values:
        if value in seen:
            duplicates.add(value)
        seen.add(value)

    if duplicates:
        raise SystemExit(
            f"verify failed: {package_name} repeated {field_name}: {', '.join(sorted(duplicates))}"
        )

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
    require_unique_strings(package_name, "proof citations", package["proof"])
    require_unique_strings(package_name, "starter artifact guard rows", package["starter_artifact_guards"])
    for proof_path in package["proof"]:
        if not proof_path.startswith(("src/", "tests/", "docs/", "scripts/")):
            raise SystemExit(
                f"verify failed: {package_name} cited proof outside the M119 allowed paths: {proof_path}"
            )

if published_release != expected_release:
    raise SystemExit("verify failed: published MEDIA_LOCAL_RELEASE_PROOF.generated.json drifted from the current M119 package entry")

if published_certification != expected_certification:
    raise SystemExit("verify failed: published ARTIFACT_PUBLICATION_CERTIFICATION.generated.json drifted from the current M119 package entry")
PY

rg -n \
  'next90-m119-media-factory-starter-artifacts|1413666751|Render starter primer and first-session companion artifacts|Produce localized starter primers, first-session briefings, and support-safe onboarding artifacts from approved source packs.|119\.4|W14|chummer6-media-factory|verify_closed_package_only|TO_BE_FILLED_M119_COMMIT|"status":[[:space:]]+"complete"|generated and published proof artifacts require exactly one M119 successor package entry before drift comparison|generated proof requires unique M119 proof citations and unique starter artifact guard rows before drift comparison so closed-package evidence cannot silently duplicate itself while still matching canonical mirrors|queue and registry mirrors must match the canonical M119 package and task blocks exactly so repo-local proof cannot drift on status or scoped fields|proof citations stay anchored to repo-local src, tests, docs, and scripts paths so closed-package evidence cannot drift into sibling surfaces|canonical successor queue rows are now complete with `landed_commit: TO_BE_FILLED_M119_COMMIT`, so future slices can close this package only from the landed proof floor\.' \
  "$tmp_dir/MEDIA_LOCAL_RELEASE_PROOF.generated.json" \
  "$tmp_dir/ARTIFACT_PUBLICATION_CERTIFICATION.generated.json" \
  docs/NEXT90_M119_STARTER_ARTIFACT_PROOF_FLOOR.md >/dev/null

if rg -ni \
  'TASK_LOCAL_TELEMETRY\.generated\.json|ACTIVE_RUN_HANDOFF\.generated\.md|operator[[:space:]]+telemetry|supervisor[[:space:]]+status|status[[:space:]]+query|eta[[:space:]]+query|active-run[[:space:]]+helper' \
  docs/NEXT90_M119_STARTER_ARTIFACT_PROOF_FLOOR.md \
  scripts/ai/verify_m119_starter_artifacts.sh \
  scripts/ai/materialize_media_release_proof.py \
  src/Chummer.Media.Factory.Runtime/Assets/StarterArtifactBundleService.cs >/dev/null; then
  echo "verify failed: M119 starter artifact proof sources must stay worker-safe and must not cite blocked run-helper context" >&2
  exit 1
fi
