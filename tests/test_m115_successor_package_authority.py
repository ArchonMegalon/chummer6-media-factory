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

PACKAGE_ID = "next90-m115-media-factory-exchange-previews"
FRONTIER_ID = "1547375325"


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
    marker = "id: 115.5"
    start = text.find(marker)
    if start == -1:
        raise AssertionError("missing registry task 115.5")

    next_row = text.find("\n      - id: 115.6", start + len(marker))
    if next_row == -1:
        return text[start:]
    return text[start:next_row]


class M115SuccessorPackageAuthorityTests(unittest.TestCase):
    def test_queue_mirrors_keep_exact_m115_media_factory_package_block(self):
        fleet_block = package_block(read(FLEET_QUEUE))
        design_block = package_block(read(DESIGN_QUEUE))
        repo_block = package_block(read(REPO_LOCAL_QUEUE))

        self.assertEqual(fleet_block, design_block)
        self.assertEqual(fleet_block, repo_block)

    def test_fleet_queue_assigns_exact_m115_media_factory_package(self):
        queue = read(FLEET_QUEUE)
        self.assertEqual(1, count_exact_pattern(queue, rf"^  package_id: {re.escape(PACKAGE_ID)}$"))
        block = package_block(queue)

        for token in (
            "title: Render recap, replay, and exchange preview artifacts with receipts and siblings",
            "task: Render recap, replay, and exchange preview artifacts with receipts and siblings.",
            "work_task_id: 115.5",
            "milestone_id: 115",
            "status: in_progress",
            "wave: W12",
            "repo: chummer6-media-factory",
            "- src",
            "- tests",
            "- docs",
            "- scripts",
            "recap_preview_artifacts",
            "replay_exchange_preview_artifacts",
        ):
            self.assertIn(token, block, token)

    def test_repo_local_registry_mirror_keeps_one_m115_task_anchor(self):
        registry = read(REPO_LOCAL_REGISTRY)
        self.assertEqual(1, count_exact_pattern(registry, r"^      - id: 115\.5$"))
        self.assertIn(
            "title: Render recap, replay, and exchange preview artifacts with receipts and inspectable siblings.",
            registry_task_block(registry),
        )

    def test_registry_keeps_m115_media_factory_task_scoped_to_render_only_work(self):
        registry = read(REGISTRY)
        self.assertEqual(1, count_exact_pattern(registry, r"^      - id: 115\.5$"))
        block = registry_task_block(registry)

        for token in (
            "id: 115.5",
            "owner: chummer6-media-factory",
            "title: Render recap, replay, and exchange preview artifacts with receipts and inspectable siblings.",
        ):
            self.assertIn(token, block, token)

    def test_generated_release_proof_keeps_m115_unlanded_and_worker_safe(self):
        proof_floor = read(ROOT / "docs/NEXT90_M115_REPLAY_EXCHANGE_PREVIEW_PROOF_FLOOR.md")
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
        self.assertEqual("in_progress", package["status"])
        self.assertEqual("implementation_only", package["completion_action"])
        self.assertEqual("unlanded", package["proof_floor_commit"])
        self.assertIn("tests/test_m115_successor_package_authority.py", package["proof"])
        self.assertIn("tests/test_m115_replay_exchange_preview_proof.py", package["proof"])
        self.assertIn("scripts/ai/verify_m115_replay_exchange_previews.sh", package["proof"])
        self.assertIn(
            "replay, recap, and exchange bundles must each stay first-class so portable artifact shelves can inspect every preview lane directly",
            package["exchange_preview_guards"],
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


if __name__ == "__main__":
    unittest.main()
