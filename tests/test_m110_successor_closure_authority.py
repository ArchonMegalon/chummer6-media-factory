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
FRONTIER_ID = "5126560638"
LANDED_COMMIT = "3accc50"


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


class M110SuccessorClosureAuthorityTests(unittest.TestCase):
    def test_fleet_queue_closes_exact_m110_runsite_bundle_package(self):
        self.assert_queue_block_closes_exact_package(package_block(read(FLEET_QUEUE)))

    def test_design_queue_mirror_closes_exact_m110_runsite_bundle_package(self):
        self.assert_queue_block_closes_exact_package(package_block(read(DESIGN_QUEUE)))

    def assert_queue_block_closes_exact_package(self, block: str):
        for token in (
            "title: Render runsite host clips, route previews, and orientation bundle receipts",
            f"frontier_id: {FRONTIER_ID}",
            "milestone_id: 110",
            "wave: W10",
            "repo: chummer6-media-factory",
            "status: complete",
            f"landed_commit: {LANDED_COMMIT}",
            "completion_action: verify_closed_package_only",
            "do_not_reopen_reason: M110 chummer6-media-factory runsite orientation bundles are complete",
            "runsite_orientation_bundle",
            "route_preview:artifact_receipts",
            "- src",
            "- tests",
            "- docs",
            "- scripts",
            "RunsiteOrientationBundleService.cs",
            "MediaFactoryContracts.cs",
            "tests/RunsiteOrientationBundleSmoke/Program.cs",
            "tests/test_runsite_orientation_bundle_contracts.py",
            "tests/test_m110_successor_closure_authority.py",
            "scripts/ai/materialize_media_release_proof.py",
            "scripts/ai/verify.sh",
            "python3 -m unittest tests.test_m110_successor_closure_authority tests.test_m110_runsite_orientation_proof exits 0.",
            "dotnet run --project tests/RunsiteOrientationBundleSmoke/Chummer.Media.Factory.RunsiteOrientationBundleSmoke.csproj --configuration Release --nologo --verbosity quiet exits 0.",
            "./scripts/ai/verify.sh exits 0.",
        ):
            self.assertIn(token, block, token)

    def test_canonical_registry_records_m110_media_factory_evidence(self):
        block = registry_task_block(read(REGISTRY))

        for token in (
            "id: 110.2",
            "owner: chummer6-media-factory",
            "Render host clips, audio companions, previews, and route-linked bundle receipts.",
            f"commit {LANDED_COMMIT}",
            "RunsiteOrientationBundleService.cs renders host clips, route previews, audio companions, and optional tour siblings through media-factory job execution only.",
            "MediaFactoryContracts.cs defines RunsiteOrientationBundleRequest, RunsiteOrientationBundleReceipt, and RunsiteRoutePreviewArtifactReceipt.",
            "tests/RunsiteOrientationBundleSmoke/Program.cs proves host clips and route previews are mandatory, route preview receipts stay route-linked, and delimiter-heavy variants cannot collapse dedupe or receipt ids.",
            "tests/test_m110_successor_closure_authority.py fail-closes the exact package, queue, registry, and generated-proof closure authority.",
            "python3 -m unittest tests.test_m110_successor_closure_authority tests.test_m110_runsite_orientation_proof exits 0.",
            "dotnet run --project tests/RunsiteOrientationBundleSmoke/Chummer.Media.Factory.RunsiteOrientationBundleSmoke.csproj --configuration Release --nologo --verbosity quiet exits 0.",
            "./scripts/ai/verify.sh exits 0.",
        ):
            self.assertIn(token, block, token)

    def test_closure_authority_does_not_cite_active_run_helpers(self):
        queue_block = package_block(read(FLEET_QUEUE))
        design_queue_block = package_block(read(DESIGN_QUEUE))
        registry = registry_task_block(read(REGISTRY))
        proof_floor = read(ROOT / "docs/NEXT90_M110_RUNSITE_ORIENTATION_PROOF_FLOOR.md")
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
            generated_proof = (Path(tmp) / "MEDIA_LOCAL_RELEASE_PROOF.generated.json").read_text(encoding="utf-8")

        package = next(
            candidate
            for candidate in json.loads(generated_proof)["successor_packages"]
            if candidate["package_id"] == PACKAGE_ID
        )
        self.assertEqual("worktree-local", package["proof_floor_commit"])
        self.assertIn("tests/test_m110_successor_closure_authority.py", package["proof"])
        self.assertIn("scripts/ai/materialize_media_release_proof.py", package["proof"])

        forbidden = (
            "TASK_LOCAL_TELEMETRY.generated.json",
            "ACTIVE_RUN_HANDOFF.generated.md",
            "operator telemetry",
            "supervisor status",
            "status query",
            "eta query",
            "active-run helper",
        )

        combined = "\n".join((queue_block, design_queue_block, registry, proof_floor, generated_proof)).lower()
        for token in forbidden:
            self.assertNotIn(token.lower(), combined, token)


if __name__ == "__main__":
    unittest.main()
