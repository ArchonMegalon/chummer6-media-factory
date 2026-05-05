from pathlib import Path
import json
import os
import subprocess
import sys
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[1]
FLEET_QUEUE = Path("/docker/fleet/.codex-studio/published/NEXT_90_DAY_QUEUE_STAGING.generated.yaml")
DESIGN_QUEUE = Path("/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml")
REGISTRY = Path("/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_PRODUCT_ADVANCE_REGISTRY.yaml")

PACKAGE_ID = "next90-m113-media-factory-gm-prep-packets"
FRONTIER_ID = "3813748639"
LANDED_COMMIT = "7d5a0167"
PROOF_FLOOR_COMMIT = "7d5a0167"
VERIFY_SCRIPT = Path("/docker/fleet/repos/chummer-media-factory/scripts/ai/verify_m113_gm_prep_packets.sh")
VERIFY_ALL_SCRIPT = Path("/docker/fleet/repos/chummer-media-factory/scripts/ai/verify.sh")
EXPECTED_PROOF = (
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
)
EXPECTED_ARTIFACT_ROLES = (
    "GmPrepOppositionPacket",
    "GmPrepOppositionPreview",
    "GmPrepOppositionBriefing",
    "GmPrepScenePacket",
    "GmPrepScenePreview",
    "GmPrepSceneBriefing",
    "GmPrepLibraryPacket",
    "GmPrepLibraryPreview",
    "GmPrepLibraryBriefing",
)
EXPECTED_RECEIPT_ROWS = (
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
)


def worker_unsafe_tokens() -> tuple[str, ...]:
    return (
        "".join(("TASK", "_LOCAL", "_TELEMETRY", ".generated", ".json")),
        "".join(("ACTIVE", "_RUN", "_HANDOFF", ".generated", ".md")),
        " ".join(("operator", "telemetry")),
        " ".join(("supervisor", "status")),
        " ".join(("status", "query")),
        " ".join(("eta", "query")),
        "-".join(("active", "run")) + " helper",
    )


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def package_block(text: str) -> str:
    marker = f"package_id: {PACKAGE_ID}"
    package_start = text.find(marker)
    if package_start == -1:
        raise AssertionError(f"missing package row {PACKAGE_ID}")

    title_starts = [
        position
        for position in (
            text.rfind("\n- title:", 0, package_start),
            text.rfind("\n- title:", 0, package_start),
        )
        if position != -1
    ]
    start = (max(title_starts) + 1) if title_starts else package_start
    next_rows = [
        position
        for position in (
            text.find("\n- title:", package_start + len(marker)),
            text.find("\n- title:", package_start + len(marker)),
        )
        if position != -1
    ]
    next_row = min(next_rows) if next_rows else -1
    if next_row == -1:
        return text[start:]
    return text[start:next_row]


def package_row_count(text: str) -> int:
    return text.count(f"package_id: {PACKAGE_ID}")


def registry_task_block(text: str) -> str:
    marker = "id: 113.4"
    start = text.find(marker)
    if start == -1:
        raise AssertionError("missing registry task 113.4")

    next_row = text.find("\n    - id:", start + len(marker))
    if next_row == -1:
        return text[start:]
    return text[start:next_row]


def registry_task_count(text: str) -> int:
    return text.count("id: 113.4")


class M113SuccessorPackageAuthorityTests(unittest.TestCase):
    def test_verify_script_fail_closes_duplicate_package_evidence(self):
        text = read(VERIFY_SCRIPT)

        self.assertTrue(os.access(VERIFY_SCRIPT, os.X_OK), "verify_m113_gm_prep_packets.sh should stay executable.")

        for token in (
            "expected_scalars = {",
            "\"title\": \"Render opposition and GM prep packets from governed source packs\"",
            "\"task\": \"Produce packet, preview, and optional briefing artifacts for opposition, scenes, and prep-library entries.\"",
            "\"work_task_id\": \"113.4\"",
            "\"frontier_id\": 3813748639",
            "\"milestone_id\": 113",
            "\"wave\": \"W11\"",
            "\"repo\": \"chummer6-media-factory\"",
            "\"status\": \"complete\"",
            "\"landed_commit\": \"7d5a0167\"",
            "\"completion_action\": \"verify_closed_package_only\"",
            "\"proof_floor_commit\": \"7d5a0167\"",
            "expected_allowed_paths = [\"src\", \"tests\", \"docs\", \"scripts\"]",
            "expected_owned_surfaces = [\"gm_prep_packets\", \"opposition_packet_artifacts\"]",
            "def require_exact_field(package_name: str, package: dict, field_name: str, expected_value: object) -> None:",
            "verify failed: {package_name} {field_name} drifted from the pinned M113 package identity",
            "for field_name, expected_value in expected_scalars.items():",
            "require_exact_field(package_name, package, \"allowed_paths\", expected_allowed_paths)",
            "require_exact_field(package_name, package, \"owned_surfaces\", expected_owned_surfaces)",
            "require_unique_strings(package_name, \"proof citations\", package[\"proof\"])",
            "require_unique_strings(package_name, \"GM prep guard rows\", package[\"gm_prep_packet_guards\"])",
            "require_unique_strings(package_name, \"artifact roles\", package[\"artifact_roles\"])",
            "require_unique_strings(package_name, \"receipt rows\", package[\"receipt_rows\"])",
            "if package[\"gm_prep_packet_guards\"] != expected_gm_prep_packet_guards:",
            "if package[\"proof\"] != expected_proof:",
            "if package[\"artifact_roles\"] != expected_artifact_roles:",
            "if package[\"receipt_rows\"] != expected_receipt_rows:",
            "GM prep guard rows drifted from the pinned M113 closed-package guard set",
            "proof citations drifted from the pinned M113 closed-package proof set",
            "artifact roles drifted from the pinned M113 closed-package role set",
            "receipt rows drifted from the pinned M113 closed-package receipt set",
            "verify failed: {package_name} repeated {field_name}:",
            "generated proof requires unique M113 proof citations and unique GM prep guard rows before drift comparison so closed-package evidence cannot silently duplicate itself while still matching canonical mirrors",
            "generated proof requires unique M113 artifact roles and unique receipt rows before drift comparison so closed-package evidence cannot silently duplicate rendered sibling claims or receipt surfaces while still matching canonical mirrors",
            "generated proof requires the exact pinned M113 proof citations, artifact roles, and receipt rows before drift comparison so closed-package evidence cannot quietly add sibling surfaces while still matching canonical mirrors",
            "GM prep packet rendering fails closed when the request contains null entries or a governed entry drops its required packet or preview artifact before normalization continues",
            "generated and published proof artifacts now pin the exact M113 GM prep guard rows directly on the successor package entry, so repo-local closure proof cannot silently rewrite the closed-package scope rules while still matching on identity alone",
            "every pinned M113 proof citation must resolve to a repo-local file before generated or published closure proof can stay green, so closed-package evidence cannot cite deleted surfaces while still matching on strings alone",
            'git rev-parse --verify "${commit_name}^{commit}"',
            "verify failed: pinned M113 commit anchor ${commit_name} does not resolve locally",
            "def require_existing_repo_file(package_name: str, proof_path: str) -> None:",
            "verify failed: {package_name} cited proof path that does not resolve locally: {proof_path}",
            "require_existing_repo_file(package_name, proof_path)",
        ):
            self.assertIn(token, text, token)

    def test_pinned_commit_anchors_resolve_locally(self):
        for commit_name in (LANDED_COMMIT, PROOF_FLOOR_COMMIT):
            subprocess.run(
                ["git", "rev-parse", "--verify", f"{commit_name}^{{commit}}"],
                cwd=ROOT,
                check=True,
                stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL,
            )

    def test_shared_verify_lane_calls_dedicated_m113_verifier(self):
        verify_all = read(VERIFY_ALL_SCRIPT)
        self.assertIn(
            "bash scripts/ai/verify_m113_gm_prep_packets.sh",
            verify_all,
            "scripts/ai/verify.sh should call the dedicated M113 verifier directly.",
        )

        verify_package = read(VERIFY_SCRIPT)
        self.assertIn(
            "verify failed: scripts/ai/verify.sh must call bash scripts/ai/verify_m113_gm_prep_packets.sh",
            verify_package,
            "verify_m113_gm_prep_packets.sh should fail closed if the shared verify lane stops calling it.",
        )

    def test_fleet_queue_closes_exact_m113_media_factory_package(self):
        self.assertEqual(1, package_row_count(read(FLEET_QUEUE)))
        self.assert_queue_block_closes_exact_package(package_block(read(FLEET_QUEUE)))

    def test_design_queue_mirror_closes_exact_m113_media_factory_package(self):
        self.assertEqual(1, package_row_count(read(DESIGN_QUEUE)))
        self.assert_queue_block_closes_exact_package(package_block(read(DESIGN_QUEUE)))

    def assert_queue_block_closes_exact_package(self, block: str):
        for token in (
            "title: Render opposition and GM prep packets from governed source packs",
            f"frontier_id: {FRONTIER_ID}",
            "task: Produce packet, preview, and optional briefing artifacts for opposition, scenes, and prep-library entries.",
            "work_task_id: 113.4",
            "milestone_id: 113",
            "wave: W11",
            "repo: chummer6-media-factory",
            "status: complete",
            f"landed_commit: {LANDED_COMMIT}",
            "completion_action: verify_closed_package_only",
            "do_not_reopen_reason: M113 chummer6-media-factory GM prep packet bundles are complete",
            "- src",
            "- tests",
            "- docs",
            "- scripts",
            "gm_prep_packets",
            "opposition_packet_artifacts",
            "GmPrepPacketBundleService.cs",
            "MediaFactoryContracts.cs",
            "tests/GmPrepPacketSmoke/Program.cs",
            "tests/test_gm_prep_packet_rendering.py",
            "tests/test_m113_gm_prep_packet_proof.py",
            "tests/test_m113_successor_package_authority.py",
            "docs/NEXT90_M113_GM_PREP_PACKET_PROOF_FLOOR.md",
            ".codex-studio/published/MEDIA_LOCAL_RELEASE_PROOF.generated.json",
            ".codex-studio/published/ARTIFACT_PUBLICATION_CERTIFICATION.generated.json",
            "scripts/ai/materialize_media_release_proof.py",
            "scripts/ai/verify_m113_gm_prep_packets.sh",
            "scripts/ai/verify.sh",
            f"landed_commit: {LANDED_COMMIT}",
            "python3 -m unittest tests.test_m113_successor_package_authority tests.test_m113_gm_prep_packet_proof tests.test_gm_prep_packet_rendering exits 0.",
            "dotnet run --project tests/GmPrepPacketSmoke/Chummer.Media.Factory.GmPrepPacketSmoke.csproj --configuration Release --nologo --verbosity quiet exits 0.",
            "./scripts/ai/verify_m113_gm_prep_packets.sh exits 0.",
            "bash scripts/ai/verify_m113_gm_prep_packets.sh exits 0.",
            "bash scripts/ai/verify.sh exits 0.",
        ):
            self.assertIn(token, block, token)

    def test_registry_records_m113_media_factory_closure_evidence(self):
        text = read(REGISTRY)
        self.assertEqual(1, registry_task_count(text))
        block = registry_task_block(text)

        for token in (
            "id: 113.4",
            "owner: chummer6-media-factory",
            "title: Render opposition and prep packets into governed packet, preview, and optional briefing artifacts.",
            "status: complete",
            f"landed_commit: {LANDED_COMMIT}",
            "GmPrepPacketBundleService.cs renders governed opposition, scene, and prep-library entries into packet, preview, and optional briefing artifacts through media-factory job execution only",
            "MediaFactoryContracts.cs defines GmPrepPacketRenderRequest, GmPrepPacketBundleReceipt, GmPrepPacketEntryReceipt, GmPrepPacketSubjectReceiptGroup, and GmPrepPacketArtifactReceipt",
            "tests/GmPrepPacketSmoke/Program.cs proves opposition entries stay mandatory, packet refs stay unique, governed source-pack plus sibling packet/source-entry scope stays enforced, optional briefings stay bounded per entry, and delimiter-heavy variants cannot collapse dedupe or receipt ids",
            "tests/test_gm_prep_packet_rendering.py, /docker/fleet/repos/chummer-media-factory/tests/test_m113_gm_prep_packet_proof.py, and /docker/fleet/repos/chummer-media-factory/tests/test_m113_successor_package_authority.py fail-close contract drift, repo-local proof drift, queue and registry closure drift, and do-not-reopen proof drift",
            "NEXT90_M113_GM_PREP_PACKET_PROOF_FLOOR.md records the repo-local M113 proof floor",
            "materialize_media_release_proof.py emits the M113 package into /docker/fleet/repos/chummer-media-factory/.codex-studio/published/MEDIA_LOCAL_RELEASE_PROOF.generated.json and /docker/fleet/repos/chummer-media-factory/.codex-studio/published/ARTIFACT_PUBLICATION_CERTIFICATION.generated.json with the current complete status",
            "verify_m113_gm_prep_packets.sh gives the package one repo-local verifier entrypoint",
            "scripts/ai/verify.sh calls the dedicated M113 verifier as part of the standard media-factory verify lane",
            "python3 -m unittest tests.test_m113_successor_package_authority tests.test_m113_gm_prep_packet_proof tests.test_gm_prep_packet_rendering exits 0.",
            "dotnet run --project tests/GmPrepPacketSmoke/Chummer.Media.Factory.GmPrepPacketSmoke.csproj --configuration Release --nologo --verbosity quiet exits 0.",
            "./scripts/ai/verify_m113_gm_prep_packets.sh exits 0.",
            "bash /docker/fleet/repos/chummer-media-factory/scripts/ai/verify_m113_gm_prep_packets.sh exits 0.",
            "bash /docker/fleet/repos/chummer-media-factory/scripts/ai/verify.sh exits 0.",
        ):
            self.assertIn(token, block, token)

    def test_registry_task_block_stops_at_next_task_even_without_113_5_marker(self):
        text = """milestones:
  - id: 113
    work_tasks:
    - id: 113.4
      owner: chummer6-media-factory
      title: Render opposition and prep packets into governed packet, preview, and optional briefing artifacts.
      notes: keep packet rendering scoped to governed media work
    - id: 114.1
      owner: chummer6-media-factory
      title: unrelated follow-on task
"""

        block = registry_task_block(text)

        self.assertIn("id: 113.4", block)
        self.assertIn("Render opposition and prep packets", block)
        self.assertNotIn("id: 114.1", block)
        self.assertNotIn("unrelated follow-on task", block)

    def test_generated_release_proof_keeps_m113_closed_and_worker_safe(self):
        proof_floor = read(ROOT / "docs/NEXT90_M113_GM_PREP_PACKET_PROOF_FLOOR.md")
        with tempfile.TemporaryDirectory() as tmp:
            subprocess.run(
                [
                    sys.executable,
                    "scripts/ai/materialize_media_release_proof.py",
                    "--out-dir",
                    tmp,
                    "--status",
                    "passed",
                ],
                cwd=ROOT,
                check=True,
                stdout=subprocess.DEVNULL,
            )
            generated_proof = json.loads(
                (Path(tmp) / "MEDIA_LOCAL_RELEASE_PROOF.generated.json").read_text(encoding="utf-8")
            )

        package = next(candidate for candidate in generated_proof["successor_packages"] if candidate["package_id"] == PACKAGE_ID)

        self.assertEqual(int(FRONTIER_ID), package["frontier_id"])
        self.assertEqual("complete", package["status"])
        self.assertEqual(LANDED_COMMIT, package["landed_commit"])
        self.assertEqual("verify_closed_package_only", package["completion_action"])
        self.assertEqual(PROOF_FLOOR_COMMIT, package["proof_floor_commit"])
        self.assertIn("tests/test_m113_successor_package_authority.py", package["proof"])
        self.assertEqual(list(EXPECTED_PROOF), package["proof"])
        self.assertEqual(list(EXPECTED_ARTIFACT_ROLES), package["artifact_roles"])
        self.assertEqual(list(EXPECTED_RECEIPT_ROWS), package["receipt_rows"])
        self.assertIn("scripts/ai/materialize_media_release_proof.py", package["proof"])
        self.assertIn("scripts/ai/verify_m113_gm_prep_packets.sh", package["proof"])

        combined = "\n".join(
            (
                package_block(read(FLEET_QUEUE)),
                package_block(read(DESIGN_QUEUE)),
                registry_task_block(read(REGISTRY)),
                proof_floor,
                json.dumps(generated_proof, indent=2, sort_keys=True),
            )
        ).lower()
        for token in worker_unsafe_tokens():
            self.assertNotIn(token.lower(), combined, token)


if __name__ == "__main__":
    unittest.main()
