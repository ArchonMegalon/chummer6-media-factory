from pathlib import Path
import json
import os
import subprocess
import sys
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[1]
PACKAGE_ID = "next90-m116-media-factory-creator-promo-kits"
PROOF_FLOOR_COMMIT = "29ea571"


class M116CreatorPromoProofTests(unittest.TestCase):
    def test_proof_floor_records_direct_verify_entrypoint(self):
        proof_floor = (ROOT / "docs/NEXT90_M116_CREATOR_PROMO_KIT_PROOF_FLOOR.md").read_text(encoding="utf-8")

        self.assertIn(
            "`scripts/ai/verify_m116_creator_promo_kits.sh` stays directly executable so workers can invoke the package verifier without wrapping it in an ad hoc shell fallback.",
            proof_floor,
        )
        self.assertTrue(
            os.access(ROOT / "scripts/ai/verify_m116_creator_promo_kits.sh", os.X_OK),
            "scripts/ai/verify_m116_creator_promo_kits.sh should stay executable for worker-safe direct invocation.",
        )

    def test_generated_release_proof_tracks_m116_creator_promo_package(self):
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

        package = next(candidate for candidate in release["successor_packages"] if candidate["package_id"] == PACKAGE_ID)

        self.assertEqual(4956678153, package["frontier_id"])
        self.assertEqual(116, package["milestone_id"])
        self.assertEqual("complete", package["status"])
        self.assertEqual("verify_closed_package_only", package["completion_action"])
        self.assertEqual(PROOF_FLOOR_COMMIT, package["proof_floor_commit"])
        self.assertEqual(
            ["creator_promo_kits", "publication_preview_artifacts"],
            package["owned_surfaces"],
        )
        self.assertIn(".codex-studio/published/MEDIA_LOCAL_RELEASE_PROOF.generated.json", package["proof"])
        self.assertIn(".codex-studio/published/ARTIFACT_PUBLICATION_CERTIFICATION.generated.json", package["proof"])
        self.assertIn("scripts/ai/verify.sh", package["proof"])

        for token in (
            "creator promo kit rendering stays render-verified by requiring an approved manifest id and manifest revision id plus sibling-only payloads before media jobs enqueue, and JSON payloads must match those scope fields exactly instead of passing on substring mentions alone",
            "parseable JSON creator promo payloads fail closed when required scope fields are missing or mismatched, so JSON strings, arrays, or wrong-sibling objects cannot bypass exact scope matching through text fallback",
            "non-JSON creator promo payloads require exact keyed values or delimiter-safe scope tokens, so near-match approved manifest ids and manifest revision ids cannot pass by raw substring collision",
            "creator promo kit rendering requires one promo video, one promo poster, and one preview-card sibling before the bundle can render",
            "creator promo video siblings require caption refs while every creator promo sibling requires at least one preview ref",
            "creator promo rendering rejects duplicate artifact refs inside one approved manifest render request",
            "bundle-scoped dedupe keys include approved manifest id, manifest revision id, rendering id, sibling role, category, output format, artifact ref, and caller dedupe key",
            "receipt hashes include caption and preview refs so creator promo receipts stay tied to emitted preview and caption siblings",
            "receipt hashes use length-prefixed caption and preview ref segments so delimiter-heavy creator promo refs cannot collapse distinct outputs onto one receipt id",
            "normalized sibling ordering keeps receipt ids, artifact refs, ready refs, and grouped role, caption, and preview receipt rows stable when callers reorder the same approved manifest siblings",
            "case-insensitive caption and preview dedupe selects one canonical ref spelling before receipt hashing and aggregate ref emission so mixed-case duplicate refs stay stable when callers reorder them",
            "source and requested timestamp metadata stay outside bundle-scoped dedupe and receipt identity so replayed approved manifest renders cannot fork stable job ids, receipt ids, ready refs, or grouped role receipts",
            "creator promo ready refs preserve per-artifact ref, receipt id, job id, job state, output format, caption refs, preview refs, asset id, asset url, cache ttl, approval state, retention state, and storage class",
            "role, caption, and preview receipt groups preserve aggregate job ids, grouped artifact refs, and grouped artifact rows so downstream shelves do not need to reconstruct creator promo evidence from raw artifact receipts",
            "rendered timestamps resolve from completed media jobs so later deduped retries cannot rewrite bundle render time with a newer request timestamp",
            "creator promo package authority requires exactly one canonical queue row per mirror and exactly one registry task block",
        ):
            self.assertIn(token, package["creator_promo_guards"])

    def test_m116_proof_sources_stay_worker_safe(self):
        combined = "\n".join(
            (
                (ROOT / "docs/NEXT90_M116_CREATOR_PROMO_KIT_PROOF_FLOOR.md").read_text(encoding="utf-8"),
                (ROOT / "scripts/ai/materialize_media_release_proof.py").read_text(encoding="utf-8"),
                (ROOT / "src/Chummer.Media.Factory.Runtime/Assets/CreatorPromoKitRenderingService.cs").read_text(
                    encoding="utf-8"
                ),
            )
        ).lower()

        forbidden = (
            "task_local_telemetry.generated.json",
            "active_run_handoff.generated.md",
            "operator telemetry",
            "supervisor status",
            "status query",
            "eta query",
            "active-run helper",
        )

        for token in forbidden:
            self.assertNotIn(token, combined, token)


if __name__ == "__main__":
    unittest.main()
