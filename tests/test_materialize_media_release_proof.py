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
            self.assertEqual("9614cca", package["proof_floor_commit"])
            self.assertEqual(
                "Pin M107 queue-mirror closure guard with structured recipe receipt refs, lifecycle truth, and output-ref dedupe safety",
                package["proof_floor_summary"],
            )
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
                    "ApprovalState",
                    "RetentionState",
                    "StorageClass",
                ],
                package["receipt_rows"],
            )
            self.assertEqual(
                [
                    "recipe execution waits for completed media jobs before emitting publication-ready receipt refs",
                    "publication-ready refs preserve per-artifact ref, receipt id, job id, job state, output format, caption refs, preview refs, asset id, cache ttl, approval state, retention state, and storage class",
                    "bundle receipts expose aggregate JobIds matching every video, audio, preview-card, and packet-bundle artifact job",
                    "publication ref receipt rows preserve receipt id, job id, job state, output format, asset id, cache ttl, approval state, retention state, and storage class",
                    "role receipt groups preserve each video, audio, preview-card, and packet-bundle sibling's receipt ids, job ids, publication refs, caption refs, preview refs, lifecycle truth, and artifact rows",
                    "caption ref receipt rows group shared refs while preserving per-artifact publication, job, and lifecycle detail",
                    "preview ref receipt rows group shared refs while preserving packet-bundle publication, job, and lifecycle detail",
                    "packet-bundle siblings require at least one preview ref before recipe execution",
                    "publication refs are unique per recipe bundle so publication-ready receipt rows remain unambiguous",
                    "job dedupe includes artifact category, output format, and publication ref so colliding caller dedupe keys cannot collapse different recipe outputs",
                    "job dedupe uses length-prefixed hashing so delimiter-heavy category, output format, publication ref, and caller dedupe values cannot collapse different recipe outputs",
                    "receipt hashes include caption and preview refs so publication-ready refs remain tied to their emitted caption and preview surfaces",
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
