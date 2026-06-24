#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"

tmp_dir="$(mktemp -d)"
trap 'rm -rf "$tmp_dir"' EXIT

if ! rg -n 'bash scripts/ai/verify_m116_creator_promo_kits.sh' scripts/ai/verify.sh >/dev/null; then
  echo "verify failed: scripts/ai/verify.sh must call bash scripts/ai/verify_m116_creator_promo_kits.sh" >&2
  exit 1
fi

python3 -m unittest \
  tests.test_m116_successor_package_authority \
  tests.test_m116_creator_promo_proof \
  tests.test_creator_promo_kit_rendering

dotnet run \
  --project tests/CreatorPromoKitSmoke/Chummer.Media.Factory.CreatorPromoKitSmoke.csproj \
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

package_id = "next90-m116-media-factory-creator-promo-kits"
repo_root = Path.cwd()
tmp_dir = Path(sys.argv[1])

def require_unique_package_ids(path: Path, payload: dict) -> None:
    seen: set[str] = set()
    duplicates: set[str] = set()
    for candidate in payload["successor_packages"]:
        current = candidate["package_id"]
        if current in seen:
            duplicates.add(current)
        seen.add(current)
    if duplicates:
        raise SystemExit(
            f"verify failed: {path.name} repeated successor package ids: {', '.join(sorted(duplicates))}"
        )

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
    require_unique_strings(package_name, "proof citations", package["proof"])
    require_unique_strings(package_name, "creator promo guard rows", package["creator_promo_guards"])
    for proof_path in package["proof"]:
        if not proof_path.startswith(("src/", "tests/", "docs/", "scripts/", ".codex-studio/published/")):
            raise SystemExit(
                f"verify failed: {package_name} cited proof outside the M116 allowed paths: {proof_path}"
            )

if published_release != expected_release:
    raise SystemExit("verify failed: published MEDIA_LOCAL_RELEASE_PROOF.generated.json drifted from the current M116 package entry")

if published_certification != expected_certification:
    raise SystemExit("verify failed: published ARTIFACT_PUBLICATION_CERTIFICATION.generated.json drifted from the current M116 package entry")
PY

rg -n \
  'next90-m116-media-factory-creator-promo-kits|4956678153|CreatorPromoKitReadyRef|CreatorPromoKitRoleReceiptGroup|creator promo package authority requires exactly one canonical queue row per mirror and exactly one registry task block|proof_floor_commit|rendered timestamps resolve from completed media jobs so later deduped retries cannot rewrite bundle render time with a newer request timestamp' \
  "$tmp_dir/MEDIA_LOCAL_RELEASE_PROOF.generated.json" \
  "$tmp_dir/ARTIFACT_PUBLICATION_CERTIFICATION.generated.json" \
  docs/NEXT90_M116_CREATOR_PROMO_KIT_PROOF_FLOOR.md >/dev/null

rg -n \
  'creator promo receipts stay render-verified|creator promo payloads fail closed unless JSON scope fields match exactly|creator promo kit rendering requires promo video, promo poster, and preview-card siblings|creator promo caption and preview refs dedupe case-insensitively|creator promo rendering rejects duplicate artifact refs inside one approved manifest request|creator promo ready refs, artifact-ref receipts, and grouped role, caption, and preview receipt rows preserve aggregate job ids|creator promo dedupe and receipt identity stay scoped to approved manifest sibling truth' \
  docs/MEDIA_CAPABILITY_SIGNOFF.md \
  src/Chummer.Media.Contracts/README.md >/dev/null

if rg -ni \
  'TASK_LOCAL_TELEMETRY\.generated\.json|ACTIVE_RUN_HANDOFF\.generated\.md|operator[[:space:]]+telemetry|supervisor[[:space:]]+status|status[[:space:]]+query|eta[[:space:]]+query|active-run[[:space:]]+helper' \
  docs/NEXT90_M116_CREATOR_PROMO_KIT_PROOF_FLOOR.md \
  scripts/ai/verify_m116_creator_promo_kits.sh \
  scripts/ai/materialize_media_release_proof.py \
  src/Chummer.Media.Factory.Runtime/Assets/CreatorPromoKitRenderingService.cs >/dev/null; then
  echo "verify failed: M116 creator promo proof sources must stay worker-safe and must not cite blocked run-helper context" >&2
  exit 1
fi
