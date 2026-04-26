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

PACKAGE_ID = "next90-m110-media-factory-runsite-bundles"


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def package_block(text: str) -> str:
    marker = f"package_id: {PACKAGE_ID}"
    package_start = text.find(marker)
    if package_start == -1:
        raise AssertionError(f"missing package row {PACKAGE_ID}")

    title_start = text.rfind("\n  - title:", 0, package_start)
    start = title_start + 1 if title_start != -1 else package_start
    next_row = text.find("\n  - title:", package_start + len(marker))
    if next_row == -1:
        return text[start:]
    return text[start:next_row]


def registry_task_block(text: str) -> str:
    marker = "id: 110.2"
    start = text.find(marker)
    if start == -1:
        raise AssertionError("missing registry task 110.2")

    next_row = text.find("\n      - id: 110.3", start + len(marker))
    if next_row == -1:
        return text[start:]
    return text[start:next_row]


class M110RunsiteOrientationProofTests(unittest.TestCase):
    def test_queue_rows_keep_m110_scoped_to_the_owned_surface(self):
        expected_tokens = (
            "title: Render runsite host clips, route previews, and orientation bundle receipts",
            "task: Make runsite orientation a first-class artifact bundle with host clips, previews, and route-linked receipts.",
            "frontier_id: 5126560638",
            "milestone_id: 110",
            "wave: W10",
            "repo: chummer6-media-factory",
            "status: complete",
            "landed_commit: 3accc50",
            "completion_action: verify_closed_package_only",
            "do_not_reopen_reason: M110 chummer6-media-factory runsite orientation bundles are complete",
            "- src",
            "- tests",
            "- docs",
            "- scripts",
            "runsite_orientation_bundle",
            "route_preview:artifact_receipts",
            "tests/test_m110_successor_closure_authority.py",
        )

        for block in (package_block(read(FLEET_QUEUE)), package_block(read(DESIGN_QUEUE))):
            for token in expected_tokens:
                self.assertIn(token, block, token)

            for forbidden in (
                "artifact_factory:receipts",
                "structured_media_recipe_execution",
            ):
                self.assertNotIn(forbidden, block, forbidden)

    def test_registry_task_stays_exactly_on_media_factory_orientation_rendering(self):
        block = registry_task_block(read(REGISTRY))

        for token in (
            "id: 110.2",
            "owner: chummer6-media-factory",
            "Render host clips, audio companions, previews, and route-linked bundle receipts.",
        ):
            self.assertIn(token, block, token)

    def test_generated_release_proof_pins_m110_package(self):
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
            release = json.loads((Path(tmp) / "MEDIA_LOCAL_RELEASE_PROOF.generated.json").read_text(encoding="utf-8"))

        package = next(
            candidate
            for candidate in release["successor_packages"]
            if candidate["package_id"] == PACKAGE_ID
        )

        self.assertEqual(5126560638, package["frontier_id"])
        self.assertEqual(110, package["milestone_id"])
        self.assertEqual("complete", package["status"])
        self.assertEqual("verify_closed_package_only", package["completion_action"])
        self.assertEqual("worktree-local", package["proof_floor_commit"])
        self.assertEqual(
            ["runsite_orientation_bundle", "route_preview:artifact_receipts"],
            package["owned_surfaces"],
        )
        self.assertIn(
            "bundle-scoped dedupe keys include approved runsite pack, route summary, bundle id, role, route segment, category, output format, and caller dedupe",
            package["orientation_guards"],
        )
        self.assertIn(
            "orientation job dedupe and receipt hashing use length-prefixed segments so delimiter-heavy variants cannot collapse onto one media job or receipt id",
            package["orientation_guards"],
        )
        self.assertIn(
            "src/Chummer.Media.Factory.Runtime/Assets/RunsiteOrientationBundleService.cs",
            package["proof"],
        )
        self.assertIn(
            "tests/RunsiteOrientationBundleSmoke/Program.cs",
            package["proof"],
        )
        self.assertIn(
            "docs/NEXT90_M110_RUNSITE_ORIENTATION_PROOF_FLOOR.md",
            package["proof"],
        )
        self.assertIn(
            "tests/test_runsite_orientation_bundle_contracts.py",
            package["proof"],
        )
        self.assertIn(
            "tests/test_m110_successor_closure_authority.py",
            package["proof"],
        )
        self.assertIn(
            "scripts/ai/materialize_media_release_proof.py",
            package["proof"],
        )

    def test_m110_proof_sources_stay_worker_safe(self):
        queue_block = package_block(read(FLEET_QUEUE))
        design_queue_block = package_block(read(DESIGN_QUEUE))
        registry_block = registry_task_block(read(REGISTRY))
        combined = "\n".join(
            (
                queue_block,
                design_queue_block,
                registry_block,
                (ROOT / "docs/NEXT90_M110_RUNSITE_ORIENTATION_PROOF_FLOOR.md").read_text(encoding="utf-8"),
                (ROOT / "scripts/ai/materialize_media_release_proof.py").read_text(encoding="utf-8"),
                (ROOT / "src/Chummer.Media.Factory.Runtime/Assets/RunsiteOrientationBundleService.cs").read_text(
                    encoding="utf-8"
                ),
            )
        ).lower()

        forbidden = (
            "task_local_telemetry.generated.json",
            "active_run_handoff.generated.md",
            "operator telemetry",
            "supervisor status",
            "status query",
            "eta query",
            "active-run helper",
        )

        for token in forbidden:
            self.assertNotIn(token, combined, token)


if __name__ == "__main__":
    unittest.main()
