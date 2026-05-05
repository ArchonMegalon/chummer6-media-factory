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

PACKAGE_ID = "next90-m109-media-factory-build-explain-bundles"
FRONTIER_ID = "4037265286"


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
    marker = "id: 109.3"
    start = text.find(marker)
    if start == -1:
        raise AssertionError("missing registry task 109.3")

    next_row = text.find("\n      - id: 109.4", start + len(marker))
    if next_row == -1:
        return text[start:]
    return text[start:next_row]


def queue_block_lines(block: str) -> list[str]:
    return [line.rstrip() for line in block.splitlines()]


def queue_value(block: str, key: str) -> str:
    prefixes = [f"  {key}: ", f"- {key}: "]
    for line in queue_block_lines(block):
        for prefix in prefixes:
            if line.startswith(prefix):
                return line[len(prefix):]
    raise AssertionError(f"missing queue field {key}")


def queue_list(block: str, key: str) -> list[str]:
    prefix = f"  {key}:"
    lines = queue_block_lines(block)
    for index, line in enumerate(lines):
        if line == prefix:
            values: list[str] = []
            for candidate in lines[index + 1 :]:
                if not candidate.startswith("  - "):
                    break
                values.append(candidate[4:])
            return values
    raise AssertionError(f"missing queue list {key}")


def queue_wrapped_value(block: str, key: str) -> str:
    prefixes = [f"  {key}: ", f"- {key}: "]
    lines = queue_block_lines(block)
    for index, line in enumerate(lines):
        for prefix in prefixes:
            if line.startswith(prefix):
                values = [line[len(prefix):].strip()]
                for candidate in lines[index + 1 :]:
                    if not candidate.startswith("    "):
                        break
                    values.append(candidate.strip())
                return " ".join(values)
    raise AssertionError(f"missing wrapped queue field {key}")


def registry_value(block: str, key: str) -> str:
    prefix = f"        {key}: "
    for line in block.splitlines():
        if line.startswith(prefix):
            return line[len(prefix):].rstrip()
    raise AssertionError(f"missing registry field {key}")


class M109SuccessorPackageAuthorityTests(unittest.TestCase):
    def assert_single_generated_package_entry(self, packages: list[dict]) -> dict:
        matches = [candidate for candidate in packages if candidate["package_id"] == PACKAGE_ID]
        self.assertEqual(1, len(matches), "generated proof should contain exactly one M109 successor package entry")
        return matches[0]

    def test_queue_mirrors_keep_exact_m109_media_factory_package_block(self):
        fleet_block = package_block(read(FLEET_QUEUE))
        design_block = package_block(read(DESIGN_QUEUE))
        repo_block = package_block(read(REPO_LOCAL_QUEUE))

        self.assertEqual(fleet_block, design_block, "design queue mirror should match the canonical fleet M109 package row exactly")
        self.assertEqual(fleet_block, repo_block, "repo-local queue mirror should match the canonical fleet M109 package row exactly")

    def test_fleet_queue_assigns_exact_m109_media_factory_package(self):
        queue = read(FLEET_QUEUE)
        self.assertEqual(
            1,
            count_exact_pattern(queue, rf"^  package_id: {re.escape(PACKAGE_ID)}$"),
            "fleet queue should contain exactly one canonical M109 package row",
        )
        self.assert_queue_block_tracks_exact_package(package_block(queue))

    def test_design_queue_mirror_assigns_exact_m109_media_factory_package(self):
        queue = read(DESIGN_QUEUE)
        self.assertEqual(
            1,
            count_exact_pattern(queue, rf"^  package_id: {re.escape(PACKAGE_ID)}$"),
            "design queue should contain exactly one canonical M109 package row",
        )
        self.assert_queue_block_tracks_exact_package(package_block(queue))

    def test_repo_local_queue_mirror_assigns_exact_m109_media_factory_package(self):
        queue = read(REPO_LOCAL_QUEUE)
        self.assertEqual(
            1,
            count_exact_pattern(queue, rf"^  package_id: {re.escape(PACKAGE_ID)}$"),
            "repo-local queue mirror should contain exactly one canonical M109 package row",
        )
        self.assert_queue_block_tracks_exact_package(package_block(queue))

    def assert_queue_block_tracks_exact_package(self, block: str):
        normalized_block = block.replace("\n    ", " ")

        for token in (
            "title: Render build explainer video, audio, preview-card, and packet siblings from approved explain packets",
            "task: Render approved Build Lab explain packets into video, audio, preview-card, and packet companions without mutating engine truth.",
            "work_task_id: 109.3",
            f"frontier_id: {FRONTIER_ID}",
            "milestone_id: 109",
            "status: complete",
            "landed_commit: 7d5a0167",
            "completion_action: verify_closed_package_only",
            "do_not_reopen_reason: M109 chummer6-media-factory build explain companion bundles are complete;",
            "wave: W9",
            "repo: chummer6-media-factory",
            "- src",
            "- tests",
            "- docs",
            "- scripts",
            "build_explain_companion_rendering",
            "explain_artifact_receipts",
        ):
            self.assertIn(token, normalized_block, token)

    def test_registry_keeps_m109_media_factory_task_scoped_to_render_only_work(self):
        registry = read(REGISTRY)
        self.assertEqual(
            1,
            count_exact_pattern(registry, r"^      - id: 109\.3$"),
            "registry should contain exactly one canonical M109 task block",
        )
        block = registry_task_block(registry)

        for token in (
            "id: 109.3",
            "owner: chummer6-media-factory",
            "title: Render build explainer video, audio, preview-card, and packet siblings from approved explain packets.",
            "status: complete",
            "landed_commit: 7d5a0167",
            "/docker/fleet/repos/chummer-media-factory/src/Chummer.Media.Factory.Runtime/Assets/BuildExplainCompanionRenderingService.cs",
            "/docker/fleet/repos/chummer-media-factory/tests/test_m109_successor_package_authority.py",
            "/docker/fleet/repos/chummer-media-factory/scripts/ai/verify_m109_build_explain_companion.sh",
            "python3 -m unittest tests.test_m109_successor_package_authority tests.test_m109_build_explain_proof tests.test_build_explain_companion_rendering exits 0.",
            "dotnet run --project tests/BuildExplainCompanionSmoke/Chummer.Media.Factory.BuildExplainCompanionSmoke.csproj --configuration Release --nologo --verbosity quiet exits 0.",
            "bash /docker/fleet/repos/chummer-media-factory/scripts/ai/verify_m109_build_explain_companion.sh exits 0.",
        ):
            self.assertIn(token, block, token)

    def test_registry_mirrors_keep_exact_m109_media_factory_task_block(self):
        design_block = registry_task_block(read(REGISTRY))
        repo_block = registry_task_block(read(REPO_LOCAL_REGISTRY))

        self.assertEqual(
            design_block,
            repo_block,
            "repo-local registry mirror should match the canonical M109 task block exactly",
        )

    def test_repo_local_registry_mirror_keeps_m109_media_factory_task_scoped_to_render_only_work(self):
        registry = read(REPO_LOCAL_REGISTRY)
        self.assertEqual(
            1,
            count_exact_pattern(registry, r"^      - id: 109\.3$"),
            "repo-local registry mirror should contain exactly one canonical M109 task block",
        )
        block = registry_task_block(registry)

        for token in (
            "id: 109.3",
            "owner: chummer6-media-factory",
            "title: Render build explainer video, audio, preview-card, and packet siblings from approved explain packets.",
            "status: complete",
            "landed_commit: 7d5a0167",
            "/docker/fleet/repos/chummer-media-factory/src/Chummer.Media.Factory.Runtime/Assets/BuildExplainCompanionRenderingService.cs",
            "/docker/fleet/repos/chummer-media-factory/tests/test_m109_successor_package_authority.py",
            "/docker/fleet/repos/chummer-media-factory/scripts/ai/verify_m109_build_explain_companion.sh",
            "python3 -m unittest tests.test_m109_successor_package_authority tests.test_m109_build_explain_proof tests.test_build_explain_companion_rendering exits 0.",
            "dotnet run --project tests/BuildExplainCompanionSmoke/Chummer.Media.Factory.BuildExplainCompanionSmoke.csproj --configuration Release --nologo --verbosity quiet exits 0.",
            "bash /docker/fleet/repos/chummer-media-factory/scripts/ai/verify_m109_build_explain_companion.sh exits 0.",
        ):
            self.assertIn(token, block, token)

    def test_generated_release_proof_keeps_m109_unlanded_and_worker_safe(self):
        proof_floor = read(ROOT / "docs/NEXT90_M109_BUILD_EXPLAIN_COMPANION_PROOF_FLOOR.md")
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

        package = self.assert_single_generated_package_entry(generated_proof["successor_packages"])

        self.assertEqual(int(FRONTIER_ID), package["frontier_id"])
        self.assertEqual("complete", package["status"])
        self.assertEqual("verify_closed_package_only", package["completion_action"])
        self.assertEqual("7d5a0167", package["proof_floor_commit"])
        self.assertIn("tests/test_m109_successor_package_authority.py", package["proof"])
        self.assertIn("scripts/ai/materialize_media_release_proof.py", package["proof"])
        self.assertIn("scripts/ai/verify_m109_build_explain_companion.sh", package["proof"])

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

    def test_generated_release_proof_matches_canonical_m109_queue_and_registry_scope(self):
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

        package = self.assert_single_generated_package_entry(generated_proof["successor_packages"])
        queue_block = package_block(read(FLEET_QUEUE))
        registry_block = registry_task_block(read(REGISTRY))

        self.assertEqual(queue_value(queue_block, "title"), package["title"])
        self.assertEqual(queue_wrapped_value(queue_block, "task"), package["task"])
        self.assertEqual(queue_value(queue_block, "work_task_id"), package["work_task_id"])
        self.assertEqual(int(queue_value(queue_block, "frontier_id")), package["frontier_id"])
        self.assertEqual(int(queue_value(queue_block, "milestone_id")), package["milestone_id"])
        self.assertEqual(queue_value(queue_block, "wave"), package["wave"])
        self.assertEqual(queue_value(queue_block, "repo"), package["repo"])
        self.assertEqual(queue_value(queue_block, "status"), package["status"])
        self.assertEqual(queue_list(queue_block, "allowed_paths"), package["allowed_paths"])
        self.assertEqual(queue_list(queue_block, "owned_surfaces"), package["owned_surfaces"])

        self.assertEqual(registry_value(registry_block, "owner"), package["repo"])
        self.assertEqual(registry_value(registry_block, "title").rstrip("."), package["title"])


if __name__ == "__main__":
    unittest.main()
