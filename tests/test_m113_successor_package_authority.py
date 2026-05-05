from pathlib import Path
import json
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
LANDED_COMMIT = "TBD_COMMIT"
PROOF_FLOOR_COMMIT = "TBD_COMMIT"


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
            text.rfind("\n  - title:", 0, package_start),
        )
        if position != -1
    ]
    start = (max(title_starts) + 1) if title_starts else package_start
    next_rows = [
        position
        for position in (
            text.find("\n- title:", package_start + len(marker)),
            text.find("\n  - title:", package_start + len(marker)),
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
            "scripts/ai/materialize_media_release_proof.py",
            "scripts/ai/verify_m113_gm_prep_packets.sh",
            "scripts/ai/verify.sh",
            "python3 -m unittest tests.test_m113_successor_package_authority tests.test_m113_gm_prep_packet_proof tests.test_gm_prep_packet_rendering exits 0.",
            "dotnet run --project tests/GmPrepPacketSmoke/Chummer.Media.Factory.GmPrepPacketSmoke.csproj --configuration Release --nologo --verbosity quiet exits 0.",
            "./scripts/ai/verify_m113_gm_prep_packets.sh exits 0.",
            "./scripts/ai/verify.sh exits 0.",
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
            "materialize_media_release_proof.py emits the M113 package into MEDIA_LOCAL_RELEASE_PROOF.generated.json and ARTIFACT_PUBLICATION_CERTIFICATION.generated.json with the current complete status",
            "python3 -m unittest tests.test_m113_successor_package_authority tests.test_m113_gm_prep_packet_proof tests.test_gm_prep_packet_rendering exits 0.",
            "dotnet run --project tests/GmPrepPacketSmoke/Chummer.Media.Factory.GmPrepPacketSmoke.csproj --configuration Release --nologo --verbosity quiet exits 0.",
            "./scripts/ai/verify_m113_gm_prep_packets.sh exits 0.",
            "./scripts/ai/verify.sh exits 0.",
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
        self.assertEqual("verify_closed_package_only", package["completion_action"])
        self.assertEqual(PROOF_FLOOR_COMMIT, package["proof_floor_commit"])
        self.assertIn("tests/test_m113_successor_package_authority.py", package["proof"])
        self.assertIn("scripts/ai/materialize_media_release_proof.py", package["proof"])
        self.assertIn("scripts/ai/verify_m113_gm_prep_packets.sh", package["proof"])

        forbidden = (
            "TASK_LOCAL_TELEMETRY.generated.json",
            "ACTIVE_RUN_HANDOFF.generated.md",
            "operator telemetry",
            "supervisor status",
            "status query",
            "eta query",
            "active-run helper",
        )

        combined = "\n".join(
            (
                package_block(read(FLEET_QUEUE)),
                package_block(read(DESIGN_QUEUE)),
                registry_task_block(read(REGISTRY)),
                proof_floor,
                json.dumps(generated_proof, indent=2, sort_keys=True),
            )
        ).lower()
        for token in forbidden:
            self.assertNotIn(token.lower(), combined, token)


if __name__ == "__main__":
    unittest.main()
