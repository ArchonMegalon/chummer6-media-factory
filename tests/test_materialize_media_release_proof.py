from __future__ import annotations

import json
import subprocess
import sys
import tempfile
from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[1]


class MaterializeMediaReleaseProofTests(unittest.TestCase):
    def test_generated_receipts_pin_m107_structured_recipe_closure(self):
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
            certification = json.loads(
                (Path(tmp) / "ARTIFACT_PUBLICATION_CERTIFICATION.generated.json").read_text(encoding="utf-8")
            )

        for payload in (release, certification):
            packages = payload["successor_packages"]
            self.assertEqual(1, len(packages))
            package = packages[0]
            self.assertEqual("next90-m107-media-factory-recipe-execution", package["package_id"])
            self.assertEqual(1746209281, package["frontier_id"])
            self.assertEqual(107, package["milestone_id"])
            self.assertEqual("complete", package["status"])
            self.assertEqual("verify_closed_package_only", package["completion_action"])
            self.assertEqual("a2a3702", package["proof_floor_commit"])
            self.assertEqual("Tighten M107 media recipe proof receipts", package["proof_floor_summary"])
            self.assertEqual(
                ["structured_media_recipe_execution", "artifact_factory:receipts"],
                package["owned_surfaces"],
            )
            self.assertEqual(
                [
                    "StructuredRecipeVideo",
                    "StructuredRecipeAudio",
                    "StructuredRecipePreviewCard",
                    "StructuredRecipePacketBundle",
                ],
                package["artifact_roles"],
            )
            self.assertEqual(
                [
                    "PublicationRefReceipts",
                    "PublicationReadyRefs",
                    "StructuredMediaRecipePublicationReadyRef",
                    "JobIds",
                    "CaptionRefReceipts",
                    "PreviewRefReceipts",
                    "RoleReceiptGroups",
                    "StructuredMediaRecipeRoleReceiptGroup",
                    "StructuredMediaRecipeRefArtifactReceipt",
                    "ArtifactReceipts",
                ],
                package["receipt_rows"],
            )
            self.assertEqual(
                [
                    "publication-ready refs preserve per-artifact ref, receipt id, job id, job state, output format, caption refs, preview refs, asset id, and cache ttl",
                    "bundle receipts expose aggregate JobIds matching every video, audio, preview-card, and packet-bundle artifact job",
                    "publication ref receipt rows preserve receipt id, job id, job state, output format, asset id, and cache ttl",
                    "role receipt groups preserve each video, audio, preview-card, and packet-bundle sibling's receipt ids, job ids, publication refs, caption refs, preview refs, and artifact rows",
                    "caption ref receipt rows group shared refs while preserving per-artifact publication and job detail",
                    "preview ref receipt rows group shared refs while preserving packet-bundle publication and job detail",
                    "packet-bundle siblings require at least one preview ref before recipe execution",
                ],
                package["publication_ready_ref_guards"],
            )
            self.assertIn(
                "src/Chummer.Media.Factory.Runtime/Assets/StructuredMediaRecipeExecutionService.cs",
                package["proof"],
            )
            self.assertIn("docs/NEXT90_M107_MEDIA_RECIPE_PROOF_FLOOR.md", package["proof"])


if __name__ == "__main__":
    unittest.main()
