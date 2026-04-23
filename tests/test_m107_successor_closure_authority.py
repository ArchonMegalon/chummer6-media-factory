from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[1]
FLEET_QUEUE = Path("/docker/fleet/.codex-studio/published/NEXT_90_DAY_QUEUE_STAGING.generated.yaml")
DESIGN_QUEUE = Path("/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml")
REGISTRY = Path("/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_PRODUCT_ADVANCE_REGISTRY.yaml")

PACKAGE_ID = "next90-m107-media-factory-recipe-execution"
FRONTIER_ID = "1746209281"
LANDED_COMMIT = "47df6ab"
PROOF_FLOOR_COMMITS = ("a2a3702", "15fb6ef", "c13b80f", "3dc59e0", "e93f8f4", "6adf9a8")


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
    marker = "id: 107.2"
    start = text.find(marker)
    if start == -1:
        raise AssertionError("missing registry task 107.2")

    next_row = text.find("\n      - id: 107.3", start + len(marker))
    if next_row == -1:
        return text[start:]
    return text[start:next_row]


class M107SuccessorClosureAuthorityTests(unittest.TestCase):
    def test_fleet_queue_closes_exact_m107_media_recipe_package(self):
        self.assert_queue_block_closes_exact_package(package_block(read(FLEET_QUEUE)))

    def test_design_queue_mirror_closes_exact_m107_media_recipe_package(self):
        self.assert_queue_block_closes_exact_package(package_block(read(DESIGN_QUEUE)))

    def assert_queue_block_closes_exact_package(self, block: str):
        for token in (
            "title: Execute structured media recipes with receipts, captions, and preview refs",
            "repo: chummer6-media-factory",
            f"frontier_id: {FRONTIER_ID}",
            "milestone_id: 107",
            "wave: W9",
            "status: complete",
            f"landed_commit: {LANDED_COMMIT}",
            "completion_action: verify_closed_package_only",
            "do_not_reopen_reason: M107 chummer6-media-factory structured recipe execution is complete",
            "structured_media_recipe_execution",
            "artifact_factory:receipts",
            "- src",
            "- tests",
            "- docs",
            "- scripts",
            "StructuredMediaRecipeExecutionService.cs",
            "MediaFactoryContracts.cs",
            "tests/StructuredMediaRecipeSmoke/Program.cs",
            "tests/test_structured_media_recipe_execution.py",
            "scripts/ai/verify.sh",
            "python3 -m unittest tests/test_structured_media_recipe_execution.py exits 0.",
            "dotnet run --project tests/StructuredMediaRecipeSmoke/Chummer.Media.Factory.StructuredMediaRecipeSmoke.csproj --no-restore exits 0.",
            "./scripts/ai/verify.sh exits 0.",
        ):
            self.assertIn(token, block, token)

        for commit in PROOF_FLOOR_COMMITS:
            self.assertIn(commit, block)

    def test_canonical_registry_records_m107_media_recipe_evidence(self):
        block = registry_task_block(read(REGISTRY))

        for token in (
            "id: 107.2",
            "owner: chummer6-media-factory",
            "Execute structured video, audio, preview-card, and packet recipes with receipts and retention truth.",
            f"commit {LANDED_COMMIT}",
            "StructuredMediaRecipeExecutionService.cs validates required sibling roles",
            "MediaFactoryContracts.cs defines StructuredMediaRecipe request",
            "tests/StructuredMediaRecipeSmoke/Program.cs proves video, audio, preview-card, and packet-bundle execution",
            "scripts/ai/verify.sh now gates the structured recipe contract scan",
            "python3 -m unittest tests/test_structured_media_recipe_execution.py exits 0.",
            "dotnet run --project tests/StructuredMediaRecipeSmoke/Chummer.Media.Factory.StructuredMediaRecipeSmoke.csproj --no-restore exits 0.",
            "./scripts/ai/verify.sh exits 0",
        ):
            self.assertIn(token, block, token)

        for commit in PROOF_FLOOR_COMMITS:
            self.assertIn(commit, block)

    def test_closure_authority_does_not_cite_active_run_helpers(self):
        queue_block = package_block(read(FLEET_QUEUE))
        design_queue_block = package_block(read(DESIGN_QUEUE))
        registry = registry_task_block(read(REGISTRY))
        proof_floor = read(ROOT / "docs/NEXT90_M107_MEDIA_RECIPE_PROOF_FLOOR.md")
        generated_proof = read(ROOT / ".codex-studio/published/MEDIA_LOCAL_RELEASE_PROOF.generated.json")
        self.assertIn('"proof_floor_commit": "e93f8f4"', generated_proof)

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
