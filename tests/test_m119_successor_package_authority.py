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

PACKAGE_ID = "next90-m119-media-factory-starter-artifacts"
FRONTIER_ID = "1413666751"
LANDED_COMMIT = "TO_BE_FILLED_M119_COMMIT"
PROOF_FLOOR_COMMIT = "TO_BE_FILLED_M119_COMMIT"
VERIFY_SCRIPT = Path("/docker/fleet/repos/chummer-media-factory/scripts/ai/verify_m119_starter_artifacts.sh")


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
    marker = "id: 119.4"
    start = text.find(marker)
    if start == -1:
        raise AssertionError("missing registry task 119.4")

    next_row = text.find("\n      - id:", start + len(marker))
    if next_row == -1:
        return text[start:]
    return text[start:next_row]


def registry_task_count(text: str) -> int:
    return text.count("id: 119.4")


class M119SuccessorPackageAuthorityTests(unittest.TestCase):
    def test_verify_script_fail_closes_duplicate_package_evidence(self):
        text = read(VERIFY_SCRIPT)

        self.assertTrue(os.access(VERIFY_SCRIPT, os.X_OK), "verify_m119_starter_artifacts.sh should stay executable.")

        for token in (
            "require_unique_strings(package_name, \"proof citations\", package[\"proof\"])",
            "require_unique_strings(package_name, \"starter artifact guard rows\", package[\"starter_artifact_guards\"])",
            "verify failed: {package_name} repeated {field_name}:",
            "generated proof requires unique M119 proof citations and unique starter artifact guard rows before drift comparison so closed-package evidence cannot silently duplicate itself while still matching canonical mirrors",
        ):
            self.assertIn(token, text, token)

    def test_fleet_queue_closes_exact_m119_media_factory_package(self):
        self.assertEqual(1, package_row_count(read(FLEET_QUEUE)))
        self.assert_queue_block_closes_exact_package(package_block(read(FLEET_QUEUE)))

    def test_design_queue_mirror_closes_exact_m119_media_factory_package(self):
        self.assertEqual(1, package_row_count(read(DESIGN_QUEUE)))
        self.assert_queue_block_closes_exact_package(package_block(read(DESIGN_QUEUE)))

    def assert_queue_block_closes_exact_package(self, block: str):
        normalized_block = " ".join(block.split())
        for token in (
            "title: Render starter primer and first-session companion artifacts",
            f"frontier_id: {FRONTIER_ID}",
            "task: Produce localized starter primers, first-session briefings, and support-safe onboarding artifacts from approved source packs.",
            "work_task_id: 119.4",
            "milestone_id: 119",
            "wave: W14",
            "repo: chummer6-media-factory",
            "status: complete",
            f"landed_commit: {LANDED_COMMIT}",
            "completion_action: verify_closed_package_only",
            "do_not_reopen_reason: M119 chummer6-media-factory starter primer and first-session companion artifacts are complete",
            "- src",
            "- tests",
            "- docs",
            "- scripts",
            "starter_primer_artifacts",
            "first_session_briefing_artifacts",
            "StarterArtifactBundleService.cs",
            "MediaFactoryContracts.cs",
            "tests/StarterArtifactBundleSmoke/Program.cs",
            "tests/test_starter_artifact_rendering.py",
            "tests/test_m119_starter_artifact_proof.py",
            "tests/test_m119_successor_package_authority.py",
            "docs/NEXT90_M119_STARTER_ARTIFACT_PROOF_FLOOR.md",
            ".codex-studio/published/MEDIA_LOCAL_RELEASE_PROOF.generated.json",
            ".codex-studio/published/ARTIFACT_PUBLICATION_CERTIFICATION.generated.json",
            "scripts/ai/materialize_media_release_proof.py",
            "scripts/ai/verify_m119_starter_artifacts.sh",
            "scripts/ai/verify.sh",
            "python3 -m unittest tests.test_m119_successor_package_authority tests.test_m119_starter_artifact_proof tests.test_starter_artifact_rendering exits 0.",
            "dotnet run --project tests/StarterArtifactBundleSmoke/Chummer.Media.Factory.StarterArtifactBundleSmoke.csproj --configuration Release --nologo --verbosity quiet exits 0.",
            "bash scripts/ai/verify_m119_starter_artifacts.sh exits 0.",
            "bash scripts/ai/verify.sh exits 0.",
        ):
            self.assertIn(" ".join(token.split()), normalized_block, token)

    def test_registry_records_m119_media_factory_closure_evidence(self):
        text = read(REGISTRY)
        self.assertEqual(1, registry_task_count(text))
        block = registry_task_block(text)

        for token in (
            "id: 119.4",
            "owner: chummer6-media-factory",
            "title: Render starter primer, first-session briefing, and support-safe onboarding companion artifacts.",
            "status: complete",
            f"landed_commit: {LANDED_COMMIT}",
            "StarterArtifactBundleService.cs renders localized starter primer, first-session briefing, and support-safe onboarding siblings through media-factory job execution only",
            "MediaFactoryContracts.cs defines StarterArtifactBundleRenderRequest, StarterArtifactBundleReceipt, StarterArtifactReceipt, StarterArtifactReadyRef, StarterArtifactLocaleReceiptGroup, StarterArtifactBundleLocaleReceiptGroup, StarterArtifactArtifactRefReceipt, StarterArtifactCaptionRefReceipt, StarterArtifactPreviewRefReceipt, and StarterArtifactSupportNoteReceipt",
            "tests/StarterArtifactBundleSmoke/Program.cs proves requested-locale bundles stay mandatory, fallback locales stay bounded, mixed-case caption and preview refs normalize canonically, non-JSON scoped payloads still render, and delimiter-heavy variants cannot collapse dedupe or receipt ids",
            "tests/test_starter_artifact_rendering.py, /docker/fleet/repos/chummer-media-factory/tests/test_m119_starter_artifact_proof.py, and /docker/fleet/repos/chummer-media-factory/tests/test_m119_successor_package_authority.py fail-close contract drift, repo-local proof drift, queue and registry closure drift, and do-not-reopen proof drift",
            "NEXT90_M119_STARTER_ARTIFACT_PROOF_FLOOR.md records the repo-local M119 proof floor",
            "materialize_media_release_proof.py emits the M119 package into /docker/fleet/repos/chummer-media-factory/.codex-studio/published/MEDIA_LOCAL_RELEASE_PROOF.generated.json and /docker/fleet/repos/chummer-media-factory/.codex-studio/published/ARTIFACT_PUBLICATION_CERTIFICATION.generated.json with the current complete status",
            "verify_m119_starter_artifacts.sh gives the package one repo-local verifier entrypoint",
            "scripts/ai/verify.sh calls the dedicated M119 verifier as part of the standard media-factory verify lane",
            "python3 -m unittest tests.test_m119_successor_package_authority tests.test_m119_starter_artifact_proof tests.test_starter_artifact_rendering exits 0.",
            "dotnet run --project tests/StarterArtifactBundleSmoke/Chummer.Media.Factory.StarterArtifactBundleSmoke.csproj --configuration Release --nologo --verbosity quiet exits 0.",
            "bash /docker/fleet/repos/chummer-media-factory/scripts/ai/verify_m119_starter_artifacts.sh exits 0.",
            "bash /docker/fleet/repos/chummer-media-factory/scripts/ai/verify.sh exits 0.",
        ):
            self.assertIn(token, block, token)

    def test_generated_release_proof_keeps_m119_closed_and_worker_safe(self):
        proof_floor = read(ROOT / "docs/NEXT90_M119_STARTER_ARTIFACT_PROOF_FLOOR.md")
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
        self.assertIn("tests/test_m119_successor_package_authority.py", package["proof"])
        self.assertIn("scripts/ai/materialize_media_release_proof.py", package["proof"])
        self.assertIn("scripts/ai/verify_m119_starter_artifacts.sh", package["proof"])

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
