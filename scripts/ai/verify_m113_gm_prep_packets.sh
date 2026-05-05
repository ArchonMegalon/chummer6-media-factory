#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"

tmp_dir="$(mktemp -d)"
trap 'rm -rf "$tmp_dir"' EXIT

python3 -m unittest \
  tests.test_m113_successor_package_authority \
  tests.test_m113_gm_prep_packet_proof \
  tests.test_gm_prep_packet_rendering

dotnet run \
  --project tests/GmPrepPacketSmoke/Chummer.Media.Factory.GmPrepPacketSmoke.csproj \
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

package_id = "next90-m113-media-factory-gm-prep-packets"
expected_scalars = {
    "title": "Render opposition and GM prep packets from governed source packs",
    "task": "Produce packet, preview, and optional briefing artifacts for opposition, scenes, and prep-library entries.",
    "work_task_id": "113.4",
    "frontier_id": 3813748639,
    "milestone_id": 113,
    "wave": "W11",
    "repo": "chummer6-media-factory",
    "status": "complete",
    "landed_commit": "7d5a0167",
    "completion_action": "verify_closed_package_only",
    "proof_floor_commit": "7d5a0167",
}
expected_allowed_paths = ["src", "tests", "docs", "scripts"]
expected_owned_surfaces = ["gm_prep_packets", "opposition_packet_artifacts"]
repo_root = Path.cwd()
tmp_dir = Path(sys.argv[1])
expected_proof = [
    "src/Chummer.Media.Factory.Runtime/Assets/GmPrepPacketBundleService.cs",
    "src/Chummer.Media.Contracts/Compatibility/RunServices/MediaFactoryContracts.cs",
    "tests/GmPrepPacketSmoke/Program.cs",
    "tests/test_gm_prep_packet_rendering.py",
    "tests/test_m113_gm_prep_packet_proof.py",
    "tests/test_m113_successor_package_authority.py",
    "docs/NEXT90_M113_GM_PREP_PACKET_PROOF_FLOOR.md",
    "scripts/ai/materialize_media_release_proof.py",
    "scripts/ai/verify_m113_gm_prep_packets.sh",
    "scripts/ai/verify.sh",
]
expected_artifact_roles = [
    "GmPrepOppositionPacket",
    "GmPrepOppositionPreview",
    "GmPrepOppositionBriefing",
    "GmPrepScenePacket",
    "GmPrepScenePreview",
    "GmPrepSceneBriefing",
    "GmPrepLibraryPacket",
    "GmPrepLibraryPreview",
    "GmPrepLibraryBriefing",
]
expected_receipt_rows = [
    "EntryReceipts",
    "GmPrepPacketEntryReceipt",
    "SubjectReceiptGroups",
    "GmPrepPacketSubjectReceiptGroup",
    "PacketReceiptIds",
    "PreviewReceiptIds",
    "BriefingReceiptIds",
    "OppositionPacketReceiptIds",
    "ScenePacketReceiptIds",
    "PrepLibraryPacketReceiptIds",
    "PacketRefs",
    "JobIds",
    "ArtifactReceipts",
    "AssetUrl",
    "ApprovalState",
    "RetentionState",
    "StorageClass",
]
expected_gm_prep_packet_guards = [
    "GM prep packet rendering stays render-verified by requiring a governed source pack id, source pack revision id, packet ref, and source entry id plus sibling-only payloads before media jobs enqueue, and JSON payloads must match those scope fields exactly instead of passing on substring mentions alone",
    "parseable JSON GM prep payloads fail closed when required scope fields are missing or mismatched, so JSON strings, arrays, or wrong-sibling objects cannot bypass exact scope matching through text fallback",
    "non-JSON GM prep payloads require exact keyed values or delimiter-safe scope tokens, so near-match source pack ids, packet refs, and source entry ids cannot pass by raw substring collision",
    "JSON and keyed text GM prep scope values trim surrounding whitespace before exact scope matching so padded governed payloads stay valid without reopening substring spoof paths",
    "GM prep packet rendering fails closed when the request contains null entries or a governed entry drops its required packet or preview artifact before normalization continues",
    "GM prep packet rendering requires at least one opposition entry and keeps scene and prep-library entries optional within the same governed render request",
    "GM prep packet entries require packet and preview artifacts while briefing artifacts stay optional per governed entry",
    "GM prep packet rendering rejects duplicate source entries and duplicate packet refs inside one governed render request",
    "bundle-scoped dedupe keys include governed source pack id, source pack revision id, rendering id, subject kind, source entry id, packet ref, artifact role, category, output format, and caller dedupe key",
    "receipt hashes use length-prefixed subject-kind, artifact-role, and output-format segments so delimiter-heavy GM prep variants cannot collapse distinct outputs onto one receipt id",
    "entry receipt ids and subject receipt group ids stay scoped to governed source pack id, source pack revision id, and rendering id so reused packet refs cannot alias grouped evidence across governed packs",
    "subject receipt groups preserve grouped entry ids, packet refs, packet receipt ids, preview receipt ids, optional briefing receipt ids, aggregate job ids, and grouped artifact rows so downstream shelves do not need to reconstruct governed prep evidence from raw artifact receipts",
    "GM prep packet artifact receipts preserve asset urls, approval state, retention state, and storage class alongside packet, preview, and optional briefing outputs",
    "rendered timestamps resolve from completed media jobs so later deduped retries cannot rewrite bundle render time with a newer request timestamp",
    "GM prep packet package authority requires exactly one canonical queue row per mirror and exactly one registry task block",
    "generated and published proof artifacts require exactly one M113 successor package entry before drift comparison",
    "generated proof requires unique M113 proof citations and unique GM prep guard rows before drift comparison so closed-package evidence cannot silently duplicate itself while still matching canonical mirrors",
    "generated proof requires unique M113 artifact roles and unique receipt rows before drift comparison so closed-package evidence cannot silently duplicate rendered sibling claims or receipt surfaces while still matching canonical mirrors",
    "generated proof requires the exact pinned M113 proof citations, artifact roles, and receipt rows before drift comparison so closed-package evidence cannot quietly add sibling surfaces while still matching canonical mirrors",
    "generated and published proof artifacts now pin the exact M113 GM prep guard rows directly on the successor package entry, so repo-local closure proof cannot silently rewrite the closed-package scope rules while still matching on identity alone",
    "queue and registry mirrors must match the canonical M113 package and task blocks exactly so repo-local proof cannot drift on status or scoped fields",
    "proof citations stay anchored to repo-local src, tests, docs, and scripts paths so closed-package evidence cannot drift into sibling surfaces",
    "every pinned M113 proof citation must resolve to a repo-local file before generated or published closure proof can stay green, so closed-package evidence cannot cite deleted surfaces while still matching on strings alone",
    "generated and published proof artifacts now pin `landed_commit: 7d5a0167` directly on the M113 successor package entry, so repo-local closure proof cannot drift behind the canonical queue receipt while still reporting `status: complete`.",
    "canonical successor queue rows are now complete with `landed_commit: 7d5a0167`, so future slices can close this package only from the landed proof floor.",
]

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

def require_exact_field(package_name: str, package: dict, field_name: str, expected_value: object) -> None:
    actual_value = package.get(field_name)
    if actual_value != expected_value:
        raise SystemExit(
            f"verify failed: {package_name} {field_name} drifted from the pinned M113 package identity"
        )

def require_existing_repo_file(package_name: str, proof_path: str) -> None:
    resolved = repo_root / proof_path
    if not resolved.is_file():
        raise SystemExit(
            f"verify failed: {package_name} cited proof path that does not resolve locally: {proof_path}"
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
    for field_name, expected_value in expected_scalars.items():
        require_exact_field(package_name, package, field_name, expected_value)
    require_exact_field(package_name, package, "allowed_paths", expected_allowed_paths)
    require_exact_field(package_name, package, "owned_surfaces", expected_owned_surfaces)
    require_unique_strings(package_name, "proof citations", package["proof"])
    require_unique_strings(package_name, "GM prep guard rows", package["gm_prep_packet_guards"])
    require_unique_strings(package_name, "artifact roles", package["artifact_roles"])
    require_unique_strings(package_name, "receipt rows", package["receipt_rows"])
    if package["gm_prep_packet_guards"] != expected_gm_prep_packet_guards:
        raise SystemExit(
            f"verify failed: {package_name} GM prep guard rows drifted from the pinned M113 closed-package guard set"
        )
    if package["proof"] != expected_proof:
        raise SystemExit(
            f"verify failed: {package_name} proof citations drifted from the pinned M113 closed-package proof set"
        )
    if package["artifact_roles"] != expected_artifact_roles:
        raise SystemExit(
            f"verify failed: {package_name} artifact roles drifted from the pinned M113 closed-package role set"
        )
    if package["receipt_rows"] != expected_receipt_rows:
        raise SystemExit(
            f"verify failed: {package_name} receipt rows drifted from the pinned M113 closed-package receipt set"
        )
    for proof_path in package["proof"]:
        if not proof_path.startswith(("src/", "tests/", "docs/", "scripts/")):
            raise SystemExit(
                f"verify failed: {package_name} cited proof outside the M113 allowed paths: {proof_path}"
            )
        require_existing_repo_file(package_name, proof_path)

if published_release != expected_release:
    raise SystemExit("verify failed: published MEDIA_LOCAL_RELEASE_PROOF.generated.json drifted from the current M113 package entry")

if published_certification != expected_certification:
    raise SystemExit("verify failed: published ARTIFACT_PUBLICATION_CERTIFICATION.generated.json drifted from the current M113 package entry")
PY

for commit_name in 7d5a0167 7d5a0167; do
  if ! git rev-parse --verify "${commit_name}^{commit}" >/dev/null 2>&1; then
    echo "verify failed: pinned M113 commit anchor ${commit_name} does not resolve locally" >&2
    exit 1
  fi
done

if ! rg -n 'bash scripts/ai/verify_m113_gm_prep_packets\.sh' scripts/ai/verify.sh >/dev/null; then
  echo "verify failed: scripts/ai/verify.sh must call bash scripts/ai/verify_m113_gm_prep_packets.sh" >&2
  exit 1
fi

rg -n \
  'next90-m113-media-factory-gm-prep-packets|3813748639|Render opposition and GM prep packets from governed source packs|Produce packet, preview, and optional briefing artifacts for opposition, scenes, and prep-library entries.|113\.4|W11|chummer6-media-factory|verify_closed_package_only|7d5a0167|"landed_commit":[[:space:]]+"7d5a0167"|"status":[[:space:]]+"complete"|generated and published proof artifacts require exactly one M113 successor package entry before drift comparison|generated proof requires unique M113 proof citations and unique GM prep guard rows before drift comparison so closed-package evidence cannot silently duplicate itself while still matching canonical mirrors|generated proof requires unique M113 artifact roles and unique receipt rows before drift comparison so closed-package evidence cannot silently duplicate rendered sibling claims or receipt surfaces while still matching canonical mirrors|generated proof requires the exact pinned M113 proof citations, artifact roles, and receipt rows before drift comparison so closed-package evidence cannot quietly add sibling surfaces while still matching canonical mirrors|generated and published proof artifacts now pin the exact M113 GM prep guard rows directly on the successor package entry, so repo-local closure proof cannot silently rewrite the closed-package scope rules while still matching on identity alone|queue and registry mirrors must match the canonical M113 package and task blocks exactly so repo-local proof cannot drift on status or scoped fields|proof citations stay anchored to repo-local src, tests, docs, and scripts paths so closed-package evidence cannot drift into sibling surfaces|every pinned M113 proof citation must resolve to a repo-local file before generated or published closure proof can stay green, so closed-package evidence cannot cite deleted surfaces while still matching on strings alone|generated and published proof artifacts now pin `landed_commit: 7d5a0167` directly on the M113 successor package entry, so repo-local closure proof cannot drift behind the canonical queue receipt while still reporting `status: complete`.|canonical successor queue rows are now complete with `landed_commit: 7d5a0167`, so future slices can close this package only from the landed proof floor\.' \
  "$tmp_dir/MEDIA_LOCAL_RELEASE_PROOF.generated.json" \
  "$tmp_dir/ARTIFACT_PUBLICATION_CERTIFICATION.generated.json" \
  docs/NEXT90_M113_GM_PREP_PACKET_PROOF_FLOOR.md >/dev/null

python3 - <<'PY'
from pathlib import Path

proof_sources = (
    "src/Chummer.Media.Contracts/Compatibility/RunServices/MediaFactoryContracts.cs",
    "src/Chummer.Media.Factory.Runtime/Assets/GmPrepPacketBundleService.cs",
    "tests/GmPrepPacketSmoke/Program.cs",
    "tests/test_gm_prep_packet_rendering.py",
    "tests/test_m113_gm_prep_packet_proof.py",
    "tests/test_m113_successor_package_authority.py",
    "docs/NEXT90_M113_GM_PREP_PACKET_PROOF_FLOOR.md",
    "scripts/ai/verify.sh",
    "scripts/ai/verify_m113_gm_prep_packets.sh",
    "scripts/ai/materialize_media_release_proof.py",
)
forbidden = (
    "".join(("task", "_local", "_telemetry", ".generated", ".json")),
    "".join(("active", "_run", "_handoff", ".generated", ".md")),
    " ".join(("operator", "telemetry")),
    " ".join(("supervisor", "status")),
    " ".join(("status", "query")),
    " ".join(("eta", "query")),
    "-".join(("active", "run")) + " helper",
)
combined = "\n".join(Path(path).read_text(encoding="utf-8").lower() for path in proof_sources)
for token in forbidden:
    if token in combined:
        raise SystemExit(
            "verify failed: M113 GM prep packet proof sources must stay worker-safe and must not cite blocked run-helper context"
        )
PY
