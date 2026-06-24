from pathlib import Path
import json
import re
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

PACKAGE_ID = "next90-m145-media-factory-explain-presenter-siblings"
FRONTIER_ID = "2090633046"


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def count_exact_pattern(text: str, pattern: str) -> int:
    return len(re.findall(pattern, text, flags=re.MULTILINE))


def package_block(text: str) -> str:
    marker = f"package_id: {PACKAGE_ID}"
    package_start = text.find(marker)
    if package_start == -1:
        raise AssertionError(f"missing package row {PACKAGE_ID}")

    title_start = text.rfind("\n- title:", 0, package_start)
    start = title_start + 1 if title_start != -1 else package_start
    next_row = text.find("\n- title:", package_start + len(marker))
    if next_row == -1:
        return text[start:]
    return text[start:next_row]


def registry_task_block(text: str) -> str:
    marker = "id: '145.5'"
    start = text.find(marker)
    if start == -1:
        raise AssertionError("missing registry task 145.5")

    next_row = text.find("\n    - id: '145.6'", start + len(marker))
    if next_row == -1:
        return text[start:]
    return text[start:next_row]


class M145SuccessorPackageAuthorityTests(unittest.TestCase):
    def test_queue_mirrors_keep_exact_m145_media_factory_package_block(self):
        fleet_block = package_block(read(FLEET_QUEUE))
        design_block = package_block(read(DESIGN_QUEUE))
        repo_block = package_block(read(REPO_LOCAL_QUEUE))

        self.assertEqual(fleet_block, design_block)
        self.assertEqual(fleet_block, repo_block)

    def test_fleet_queue_assigns_exact_m145_media_factory_package(self):
        queue = read(FLEET_QUEUE)
        self.assertEqual(1, count_exact_pattern(queue, rf"^  package_id: {re.escape(PACKAGE_ID)}$"))
        self.assert_queue_block_tracks_exact_package(package_block(queue))

    def test_all_queue_mirrors_keep_exactly_one_m145_package_row(self):
        for queue_path in (FLEET_QUEUE, DESIGN_QUEUE, REPO_LOCAL_QUEUE):
            with self.subTest(queue_path=queue_path):
                queue = read(queue_path)
                self.assertEqual(1, count_exact_pattern(queue, rf"^  package_id: {re.escape(PACKAGE_ID)}$"))

    def test_registry_keeps_m145_media_factory_task_scoped_to_render_only_work(self):
        registry = read(REGISTRY)
        self.assertEqual(1, count_exact_pattern(registry, r"^    - id: '145\.5'$"))
        block = registry_task_block(registry)

        for token in (
            "id: '145.5'",
            "owner: chummer6-media-factory",
            "title: Render optional audio or presenter siblings from approved explanation packets without becoming calculation authority.",
        ):
            self.assertIn(token, block, token)

    def test_registry_mirrors_keep_exact_m145_media_factory_task_block(self):
        self.assertEqual(registry_task_block(read(REGISTRY)), registry_task_block(read(REPO_LOCAL_REGISTRY)))

    def test_all_registry_mirrors_keep_exactly_one_m145_task_block(self):
        for registry_path in (REGISTRY, REPO_LOCAL_REGISTRY):
            with self.subTest(registry_path=registry_path):
                registry = read(registry_path)
                self.assertEqual(1, count_exact_pattern(registry, r"^    - id: '145\.5'$"))

    def test_generated_release_proof_keeps_m145_closed_and_worker_safe(self):
        proof_floor = read(ROOT / "docs/NEXT90_M145_EXPLAIN_PRESENTER_SIBLINGS_PROOF_FLOOR.md")
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
            generated_proof = json.loads((Path(tmp) / "MEDIA_LOCAL_RELEASE_PROOF.generated.json").read_text(encoding="utf-8"))

        package = next(candidate for candidate in generated_proof["successor_packages"] if candidate["package_id"] == PACKAGE_ID)
        self.assertEqual(int(FRONTIER_ID), package["frontier_id"])
        self.assertEqual("complete", package["status"])
        self.assertEqual("verify_closed_package_only", package["completion_action"])
        self.assertEqual("7d5a0167", package["proof_floor_commit"])
        self.assertIn("tests/test_m145_successor_package_authority.py", package["proof"])
        self.assertIn("scripts/ai/verify_m145_explain_presenter_siblings.sh", package["proof"])
        self.assertIn(
            "canonical successor queue rows are now complete with `landed_commit: 7d5a0167`, so future slices can close this package only from the landed proof floor.",
            package["explain_presenter_guards"],
        )

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

    def assert_queue_block_tracks_exact_package(self, block: str):
        for token in (
            "title: Render optional audio or presenter siblings from approved explanation packets without becoming calculation authority.",
            "task: Render optional audio or presenter siblings from approved explanation packets while preserving packet identity,",
            "work_task_id: '145.5'",
            f"frontier_id: {FRONTIER_ID}",
            "milestone_id: 145",
            "status: complete",
            "completion_action: verify_closed_package_only",
            "landed_commit: 7d5a0167",
            "wave: W28",
            "repo: chummer6-media-factory",
            "- src",
            "- tests",
            "- docs",
            "- scripts",
            "explain_presenter_siblings:media_factory",
            "explain_audio_video:media_factory",
        ):
            self.assertIn(token, block, token)


if __name__ == "__main__":
    unittest.main()
