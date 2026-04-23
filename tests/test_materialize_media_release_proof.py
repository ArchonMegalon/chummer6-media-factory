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
                ["PublicationRefReceipts", "CaptionRefReceipts", "PreviewRefReceipts"],
                package["receipt_rows"],
            )
            self.assertIn(
                "src/Chummer.Media.Factory.Runtime/Assets/StructuredMediaRecipeExecutionService.cs",
                package["proof"],
            )


if __name__ == "__main__":
    unittest.main()
