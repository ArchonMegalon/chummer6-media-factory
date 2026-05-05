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
REPO_LOCAL_QUEUE = ROOT / ".codex-design/product/NEXT_90_DAY_QUEUE_STAGING.generated.yaml"
REPO_LOCAL_REGISTRY = ROOT / ".codex-design/product/NEXT_90_DAY_PRODUCT_ADVANCE_REGISTRY.yaml"

PACKAGE_ID = "next90-m116-media-factory-creator-promo-kits"
FRONTIER_ID = "4956678153"
LANDED_COMMIT = "29ea571"
PROOF_FLOOR_COMMIT = "29ea571"
VERIFY_SCRIPT = ROOT / "scripts/ai/verify_m116_creator_promo_kits.sh"
VERIFY_ALL_SCRIPT = ROOT / "scripts/ai/verify.sh"


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
    marker = "id: 116.4"
    start = text.find(marker)
    if start == -1:
        raise AssertionError("missing registry task 116.4")

    next_row = text.find("\n      - id: 116.5", start + len(marker))
    if next_row == -1:
        return text[start:]
    return text[start:next_row]


def registry_task_count(text: str) -> int:
    return text.count("id: 116.4")


class M116SuccessorPackageAuthorityTests(unittest.TestCase):
    def test_verify_script_fail_closes_duplicate_package_evidence(self):
        text = read(VERIFY_SCRIPT)

        self.assertTrue(os.access(VERIFY_SCRIPT, os.X_OK), "verify_m116_creator_promo_kits.sh should stay executable.")

        for token in (
            "require_unique_strings(package_name, \"proof citations\", package[\"proof\"])",
            "require_unique_strings(package_name, \"creator promo guard rows\", package[\"creator_promo_guards\"])",
            "verify failed: {package_name} repeated {field_name}:",
            "verify failed: scripts/ai/verify.sh must call bash scripts/ai/verify_m116_creator_promo_kits.sh",
        ):
            self.assertIn(token, text, token)

    def test_shared_verify_lane_calls_dedicated_m116_verifier(self):
        verify_all = read(VERIFY_ALL_SCRIPT)
        self.assertIn(
            "bash scripts/ai/verify_m116_creator_promo_kits.sh",
            verify_all,
            "scripts/ai/verify.sh should call the dedicated M116 verifier directly.",
        )

    def test_queue_mirrors_keep_exact_m116_media_factory_package_block(self):
        fleet_block = package_block(read(FLEET_QUEUE))
        design_block = package_block(read(DESIGN_QUEUE))
        repo_block = package_block(read(REPO_LOCAL_QUEUE))

        self.assertEqual(fleet_block, design_block, "design queue mirror should match the canonical fleet M116 package row exactly")
        self.assertEqual(fleet_block, repo_block, "repo-local queue mirror should match the canonical fleet M116 package row exactly")

    def test_fleet_queue_closes_exact_m116_media_factory_package(self):
        self.assertEqual(1, package_row_count(read(FLEET_QUEUE)))
        self.assert_queue_block_closes_exact_package(package_block(read(FLEET_QUEUE)))

    def test_design_queue_mirror_closes_exact_m116_media_factory_package(self):
        self.assertEqual(1, package_row_count(read(DESIGN_QUEUE)))
        self.assert_queue_block_closes_exact_package(package_block(read(DESIGN_QUEUE)))

    def test_repo_local_queue_mirror_closes_exact_m116_media_factory_package(self):
        self.assertEqual(1, package_row_count(read(REPO_LOCAL_QUEUE)))
        self.assert_queue_block_closes_exact_package(package_block(read(REPO_LOCAL_QUEUE)))

    def assert_queue_block_closes_exact_package(self, block: str):
        for token in (
            "title: Render creator promo kits from approved manifests",
            "task: Render creator promo kits from approved manifests.",
            "work_task_id: 116.4",
            f"frontier_id: {FRONTIER_ID}",
            "milestone_id: 116",
            "status: complete",
            "wave: W13",
            "repo: chummer6-media-factory",
            f"landed_commit: {LANDED_COMMIT}",
            "completion_action: verify_closed_package_only",
            "do_not_reopen_reason: M116 chummer6-media-factory creator promo kit rendering is complete",
            "- src",
            "- tests",
            "- docs",
            "- scripts",
            "creator_promo_kits",
            "publication_preview_artifacts",
            "CreatorPromoKitRenderingService.cs",
            "MediaFactoryContracts.cs",
            "tests/CreatorPromoKitSmoke/Program.cs",
            "tests/test_creator_promo_kit_rendering.py",
            "tests/test_m116_creator_promo_proof.py",
            "tests/test_m116_successor_package_authority.py",
            "docs/NEXT90_M116_CREATOR_PROMO_KIT_PROOF_FLOOR.md",
            ".codex-studio/published/MEDIA_LOCAL_RELEASE_PROOF.generated.json",
            ".codex-studio/published/ARTIFACT_PUBLICATION_CERTIFICATION.generated.json",
            "scripts/ai/materialize_media_release_proof.py",
            "scripts/ai/verify_m116_creator_promo_kits.sh",
            "scripts/ai/verify.sh",
            f"commit {LANDED_COMMIT} pins the M116 creator promo kit closure floor.",
            "python3 -m unittest tests.test_m116_successor_package_authority tests.test_m116_creator_promo_proof tests.test_creator_promo_kit_rendering exits 0.",
            "dotnet run --project tests/CreatorPromoKitSmoke/Chummer.Media.Factory.CreatorPromoKitSmoke.csproj --configuration Release --nologo --verbosity quiet exits 0.",
            "bash scripts/ai/verify_m116_creator_promo_kits.sh exits 0.",
        ):
            self.assertIn(token, block, token)

    def test_registry_keeps_m116_media_factory_task_scoped_to_render_only_work(self):
        text = read(REGISTRY)
        self.assertEqual(1, registry_task_count(text))
        block = registry_task_block(text)

        for token in (
            "id: 116.4",
            "owner: chummer6-media-factory",
            "title: Render creator promo kits from approved manifests with preview, captions, and receipts.",
            "status: complete",
            f"landed_commit: {LANDED_COMMIT}",
            "CreatorPromoKitRenderingService.cs renders creator promo video, poster, and preview-card siblings from approved manifests through media-factory job execution only.",
            "MediaFactoryContracts.cs defines CreatorPromoKitRenderRequest, CreatorPromoKitRenderReceipt, CreatorPromoKitArtifactReceipt, CreatorPromoKitReadyRef, CreatorPromoKitRoleReceiptGroup, CreatorPromoKitArtifactRefReceipt, CreatorPromoCaptionRefReceipt, and CreatorPromoPreviewRefReceipt.",
            "tests/CreatorPromoKitSmoke/Program.cs proves approved-manifest and revision scope enforcement, required video/poster/preview-card sibling coverage, preview and caption receipts, replay-safe dedupe, stable rendered timestamps, mixed-case ref dedupe, duplicate artifact-ref rejection, and delimiter-heavy receipt collision safety.",
            "tests/test_creator_promo_kit_rendering.py, /docker/fleet/repos/chummer-media-factory/tests/test_m116_creator_promo_proof.py, and /docker/fleet/repos/chummer-media-factory/tests/test_m116_successor_package_authority.py fail-close contract drift, repo-local proof drift, queue and registry closure drift, duplicate proof evidence drift, and shared verify-lane drift.",
            "NEXT90_M116_CREATOR_PROMO_KIT_PROOF_FLOOR.md records the repo-local M116 proof floor.",
            "materialize_media_release_proof.py emits the M116 package into /docker/fleet/repos/chummer-media-factory/.codex-studio/published/MEDIA_LOCAL_RELEASE_PROOF.generated.json and /docker/fleet/repos/chummer-media-factory/.codex-studio/published/ARTIFACT_PUBLICATION_CERTIFICATION.generated.json with the current complete status.",
            "verify_m116_creator_promo_kits.sh gives the package one repo-local verifier entrypoint and rejects duplicate proof or guard rows plus shared-verify drift.",
            "scripts/ai/verify.sh calls the dedicated M116 verifier as part of the standard media-factory verify lane.",
            "python3 -m unittest tests.test_m116_successor_package_authority tests.test_m116_creator_promo_proof tests.test_creator_promo_kit_rendering exits 0.",
            "dotnet run --project tests/CreatorPromoKitSmoke/Chummer.Media.Factory.CreatorPromoKitSmoke.csproj --configuration Release --nologo --verbosity quiet exits 0.",
            "bash /docker/fleet/repos/chummer-media-factory/scripts/ai/verify_m116_creator_promo_kits.sh exits 0.",
        ):
            self.assertIn(token, block, token)

    def test_registry_mirrors_keep_exact_m116_media_factory_task_block(self):
        design_block = registry_task_block(read(REGISTRY))
        repo_block = registry_task_block(read(REPO_LOCAL_REGISTRY))

        self.assertEqual(
            design_block,
            repo_block,
            "repo-local registry mirror should match the canonical M116 task block exactly",
        )

    def test_generated_release_proof_keeps_m116_closed_and_worker_safe(self):
        proof_floor = read(ROOT / "docs/NEXT90_M116_CREATOR_PROMO_KIT_PROOF_FLOOR.md")
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
        self.assertIn("tests/test_m116_successor_package_authority.py", package["proof"])
        self.assertIn("scripts/ai/materialize_media_release_proof.py", package["proof"])
        self.assertIn("scripts/ai/verify_m116_creator_promo_kits.sh", package["proof"])
        self.assertIn("scripts/ai/verify.sh", package["proof"])

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
                package_block(read(REPO_LOCAL_QUEUE)),
                registry_task_block(read(REGISTRY)),
                registry_task_block(read(REPO_LOCAL_REGISTRY)),
                proof_floor,
                json.dumps(generated_proof, indent=2, sort_keys=True),
            )
        ).lower()
        for token in forbidden:
            self.assertNotIn(token.lower(), combined, token)


if __name__ == "__main__":
    unittest.main()
