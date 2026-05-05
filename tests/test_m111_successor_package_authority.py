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

PACKAGE_ID = "next90-m111-media-factory-concierge-bundles"
FRONTIER_ID = "4132724850"
LANDED_COMMIT = "7d5a0167"


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def package_block(text: str) -> str:
    marker = f"package_id: {PACKAGE_ID}"
    package_start = text.find(marker)
    if package_start == -1:
        raise AssertionError(f"missing package row {PACKAGE_ID}")

    title_start = max(
        text.rfind("\n- title:", 0, package_start),
        text.rfind("\n- title:", 0, package_start),
    )
    start = title_start + 1 if title_start != -1 else package_start
    next_row_candidates = [
        index
        for index in (
            text.find("\n- title:", package_start + len(marker)),
            text.find("\n- title:", package_start + len(marker)),
        )
        if index != -1
    ]
    next_row = min(next_row_candidates) if next_row_candidates else -1
    if next_row == -1:
        return text[start:]
    return text[start:next_row]


def package_row_count(text: str) -> int:
    return text.count(f"package_id: {PACKAGE_ID}")


def registry_task_block(text: str) -> str:
    marker = "id: 111.3"
    start = text.find(marker)
    if start == -1:
        raise AssertionError("missing registry task 111.3")

    next_row = text.find("\n      - id: 111.4", start + len(marker))
    if next_row == -1:
        return text[start:]
    return text[start:next_row]


def registry_task_count(text: str) -> int:
    return text.count("id: 111.3")


class M111SuccessorPackageAuthorityTests(unittest.TestCase):
    def test_fleet_queue_closes_exact_m111_media_factory_package(self):
        text = read(FLEET_QUEUE)
        self.assertEqual(1, package_row_count(text))
        self.assert_queue_block_closes_exact_package(package_block(text))

    def test_design_queue_mirror_closes_exact_m111_media_factory_package(self):
        text = read(DESIGN_QUEUE)
        self.assertEqual(1, package_row_count(text))
        self.assert_queue_block_closes_exact_package(package_block(text))

    def assert_queue_block_closes_exact_package(self, block: str):
        for token in (
            "title: Render release explainer, support closure, and public concierge companion bundles with captions, previews, and sibling notes",
            "task: Render install-aware release, support, and public concierge companions with captions, previews, and bounded sibling notes.",
            "work_task_id: 111.3",
            f"frontier_id: {FRONTIER_ID}",
            "milestone_id: 111",
            "wave: W9",
            "repo: chummer6-media-factory",
            "status: complete",
            f"landed_commit: {LANDED_COMMIT}",
            "completion_action: verify_closed_package_only",
            "do_not_reopen_reason: M111 chummer6-media-factory install-aware concierge bundles are complete",
            "- src",
            "- tests",
            "- docs",
            "- scripts",
            "release_explainer_artifacts",
            "support_closure_artifacts",
            "public_concierge_companions",
            "InstallAwareConciergeBundleService.cs",
            "MediaFactoryContracts.cs",
            "tests/InstallAwareConciergeSmoke/Program.cs",
            "tests/test_install_aware_concierge_rendering.py",
            "tests/test_m111_install_aware_concierge_proof.py",
            "tests/test_m111_successor_package_authority.py",
            "docs/NEXT90_M111_INSTALL_AWARE_CONCIERGE_PROOF_FLOOR.md",
            ".codex-studio/published/MEDIA_LOCAL_RELEASE_PROOF.generated.json",
            ".codex-studio/published/ARTIFACT_PUBLICATION_CERTIFICATION.generated.json",
            "scripts/ai/materialize_media_release_proof.py",
            "scripts/ai/verify_m111_install_aware_concierge.sh",
            "scripts/ai/verify.sh",
            f"commit {LANDED_COMMIT}",
            "python3 -m unittest tests.test_m111_successor_package_authority tests.test_m111_install_aware_concierge_proof tests.test_install_aware_concierge_rendering exits 0.",
            "dotnet run --project tests/InstallAwareConciergeSmoke/Chummer.Media.Factory.InstallAwareConciergeSmoke.csproj --configuration Release --nologo --verbosity quiet exits 0.",
            "bash scripts/ai/verify_m111_install_aware_concierge.sh exits 0.",
            "bash scripts/ai/verify.sh exits 0.",
        ):
            self.assertIn(token, block, token)

    def test_registry_records_m111_media_factory_closure_evidence(self):
        text = read(REGISTRY)
        self.assertEqual(1, registry_task_count(text))
        block = registry_task_block(text)

        for token in (
            "id: 111.3",
            "owner: chummer6-media-factory",
            "title: Render release explainer, support closure, and public concierge companion bundles with captions, previews, and sibling notes.",
            "status: complete",
            f"commit {LANDED_COMMIT}",
            "InstallAwareConciergeBundleService.cs renders release explainer, support closure, and public concierge companions through media-factory job execution only",
            "MediaFactoryContracts.cs defines InstallAwareConciergeRenderRequest, InstallAwareConciergeBundleReceipt, InstallAwareConciergeArtifactReceipt",
            "tests/InstallAwareConciergeSmoke/Program.cs proves every install-aware bundle kind requires video, audio, and preview-card siblings",
            "tests/test_install_aware_concierge_rendering.py, /docker/fleet/repos/chummer-media-factory/tests/test_m111_install_aware_concierge_proof.py, and /docker/fleet/repos/chummer-media-factory/tests/test_m111_successor_package_authority.py fail-close contract drift, repo-local proof drift, and canonical package-assignment drift",
            "NEXT90_M111_INSTALL_AWARE_CONCIERGE_PROOF_FLOOR.md records the repo-local M111 proof floor",
            "materialize_media_release_proof.py emits the M111 package into /docker/fleet/repos/chummer-media-factory/.codex-studio/published/MEDIA_LOCAL_RELEASE_PROOF.generated.json and /docker/fleet/repos/chummer-media-factory/.codex-studio/published/ARTIFACT_PUBLICATION_CERTIFICATION.generated.json with the current complete status",
            "verify_m111_install_aware_concierge.sh gives the package one repo-local verifier entrypoint",
            "scripts/ai/verify.sh calls the dedicated M111 verifier as part of the standard media-factory verify lane",
            "python3 -m unittest tests.test_m111_successor_package_authority tests.test_m111_install_aware_concierge_proof tests.test_install_aware_concierge_rendering exits 0.",
            "dotnet run --project tests/InstallAwareConciergeSmoke/Chummer.Media.Factory.InstallAwareConciergeSmoke.csproj --configuration Release --nologo --verbosity quiet exits 0.",
            "bash /docker/fleet/repos/chummer-media-factory/scripts/ai/verify_m111_install_aware_concierge.sh exits 0.",
            "bash /docker/fleet/repos/chummer-media-factory/scripts/ai/verify.sh exits 0.",
        ):
            self.assertIn(token, block, token)

    def test_generated_release_proof_keeps_m111_closed_and_worker_safe(self):
        proof_floor = read(ROOT / "docs/NEXT90_M111_INSTALL_AWARE_CONCIERGE_PROOF_FLOOR.md")
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
        self.assertEqual(LANDED_COMMIT, package["proof_floor_commit"])
        self.assertIn("tests/test_m111_successor_package_authority.py", package["proof"])
        self.assertIn("scripts/ai/materialize_media_release_proof.py", package["proof"])
        self.assertIn("scripts/ai/verify_m111_install_aware_concierge.sh", package["proof"])

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
