from __future__ import annotations

import json
import subprocess
import sys
import tempfile
from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[1]


class MaterializeMediaReleaseProofTests(unittest.TestCase):
    def test_generated_receipts_pin_successor_package_closure(self):
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

        expected_packages = {
            "next90-m107-media-factory-recipe-execution": {
                "frontier_id": 1746209281,
                "milestone_id": 107,
                "status": "complete",
                "completion_action": "verify_closed_package_only",
                "proof_floor_commit": "398f756",
                "proof_floor_summary": "Pin M107 structured recipe receipt hardening with asset urls, stable replay ordering, and length-prefixed ref hashing",
                "owned_surfaces": [
                    "structured_media_recipe_execution",
                    "artifact_factory:receipts",
                ],
                "artifact_roles": [
                    "StructuredRecipeVideo",
                    "StructuredRecipeAudio",
                    "StructuredRecipePreviewCard",
                    "StructuredRecipePacketBundle",
                ],
                "receipt_rows": [
                    "PublicationRefReceipts",
                    "PublicationReadyRefs",
                    "StructuredMediaRecipePublicationReadyRef",
                    "JobIds",
                    "AssetUrl",
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
                "proof": [
                    "src/Chummer.Media.Factory.Runtime/Assets/StructuredMediaRecipeExecutionService.cs",
                    "tests/test_m107_successor_closure_authority.py",
                    "docs/NEXT90_M107_MEDIA_RECIPE_PROOF_FLOOR.md",
                    "scripts/ai/materialize_media_release_proof.py",
                ],
                "extra_key": "publication_ready_ref_guards",
                "extra_value": [
                    "recipe execution waits for completed media jobs before emitting publication-ready receipt refs",
                    "publication-ready refs preserve per-artifact ref, receipt id, job id, job state, output format, caption refs, preview refs, asset id, cache ttl, approval state, retention state, and storage class",
                    "artifact, publication-ready, publication-ref, caption-ref, and preview-ref receipt rows preserve direct asset urls so publication shelves do not need a follow-up asset lookup",
                    "bundle receipts expose aggregate JobIds matching every video, audio, preview-card, and packet-bundle artifact job",
                    "publication ref receipt rows preserve receipt id, job id, job state, output format, asset id, cache ttl, approval state, retention state, and storage class",
                    "role receipt groups preserve each video, audio, preview-card, and packet-bundle sibling's receipt ids, job ids, publication refs, caption refs, preview refs, lifecycle truth, and artifact rows",
                    "caption ref receipt rows group shared refs while preserving per-artifact publication, job, and lifecycle detail",
                    "preview ref receipt rows group shared refs while preserving packet-bundle publication, job, and lifecycle detail",
                    "packet-bundle siblings require at least one preview ref before recipe execution",
                    "publication refs are unique per recipe bundle so publication-ready receipt rows remain unambiguous",
                    "role, caption, and preview receipt groups sort their receipt ids and refs explicitly so replayed bundles keep stable publication evidence ordering",
                    "job dedupe includes artifact category, output format, and publication ref so colliding caller dedupe keys cannot collapse different recipe outputs",
                    "job dedupe uses length-prefixed hashing so delimiter-heavy category, output format, publication ref, and caller dedupe values cannot collapse different recipe outputs",
                    "receipt hashes include caption and preview refs so publication-ready refs remain tied to their emitted caption and preview surfaces",
                    "receipt hashes use length-prefixed caption and preview ref segments so delimiter-heavy refs cannot collapse distinct publication-ready outputs onto one receipt id",
                ],
            },
            "next90-m110-media-factory-runsite-bundles": {
                "frontier_id": 5126560638,
                "milestone_id": 110,
                "status": "complete",
                "completion_action": "verify_closed_package_only",
                "proof_floor_commit": "worktree-local",
                "proof_floor_summary": "Pin M110 runsite orientation bundle proof with host clips, route-linked preview receipts, and preview-only posture",
                "owned_surfaces": [
                    "runsite_orientation_bundle",
                    "route_preview:artifact_receipts",
                ],
                "artifact_roles": [
                    "RunsiteHostClip",
                    "RunsiteRoutePreview",
                    "RunsiteAudioCompanion",
                    "RunsiteTourSibling",
                ],
                "receipt_rows": [
                    "HostClipReceiptIds",
                    "RoutePreviewReceiptIds",
                    "RoutePreviewArtifactReceipts",
                    "RunsiteRoutePreviewArtifactReceipt",
                    "AudioCompanionReceiptIds",
                    "TourSiblingReceiptIds",
                    "Artifacts",
                    "JobId",
                    "CacheTtl",
                ],
                "proof": [
                    "src/Chummer.Media.Factory.Runtime/Assets/RunsiteOrientationBundleService.cs",
                    "tests/test_m110_successor_closure_authority.py",
                    "docs/NEXT90_M110_RUNSITE_ORIENTATION_PROOF_FLOOR.md",
                    "scripts/ai/materialize_media_release_proof.py",
                ],
                "extra_key": "orientation_guards",
                "extra_value": [
                    "runsite orientation bundles require at least one host clip before rendering",
                    "runsite orientation bundles require at least one route preview before rendering",
                    "preview truth posture stays pre-session-orientation-only-not-tactical-truth",
                    "route preview receipt rows preserve route segment ids plus receipt and media job identity",
                    "bundle-scoped dedupe keys include approved runsite pack, route summary, bundle id, role, route segment, category, output format, and caller dedupe",
                    "artifact receipts expose media-factory job ids for every host clip, route preview, audio companion, and optional tour sibling",
                    "orientation job dedupe and receipt hashing use length-prefixed segments so delimiter-heavy variants cannot collapse onto one media job or receipt id",
                ],
            },
        }

        for payload in (release, certification):
            packages = payload["successor_packages"]
            self.assertEqual(set(expected_packages), {package["package_id"] for package in packages})

            for package in packages:
                expected = expected_packages[package["package_id"]]
                self.assertEqual(expected["frontier_id"], package["frontier_id"])
                self.assertEqual(expected["milestone_id"], package["milestone_id"])
                self.assertEqual(expected["status"], package["status"])
                self.assertEqual(expected["completion_action"], package["completion_action"])
                self.assertEqual(expected["proof_floor_commit"], package["proof_floor_commit"])
                self.assertEqual(expected["proof_floor_summary"], package["proof_floor_summary"])
                self.assertEqual(expected["owned_surfaces"], package["owned_surfaces"])
                self.assertEqual(expected["artifact_roles"], package["artifact_roles"])
                self.assertEqual(expected["receipt_rows"], package["receipt_rows"])
                self.assertEqual(expected["extra_value"], package[expected["extra_key"]])

                for proof_path in expected["proof"]:
                    self.assertIn(proof_path, package["proof"])

        self.assertEqual(
            "render jobs, manifests, previews, runsite orientation bundles, structured recipe bundles, and asset lifecycle",
            release["evidence"]["release_surface"],
        )


if __name__ == "__main__":
    unittest.main()
