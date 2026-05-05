from pathlib import Path
import json
import os
import subprocess
import sys
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[1]
PACKAGE_ID = "next90-m113-media-factory-gm-prep-packets"
PROOF_FLOOR_COMMIT = "TBD_COMMIT"


class M113GmPrepPacketProofTests(unittest.TestCase):
    def test_proof_floor_records_direct_verify_entrypoint(self):
        proof_floor = (ROOT / "docs/NEXT90_M113_GM_PREP_PACKET_PROOF_FLOOR.md").read_text(encoding="utf-8")

        self.assertIn(
            "`scripts/ai/verify_m113_gm_prep_packets.sh` stays directly executable so workers can invoke the package verifier without wrapping it in an ad hoc shell fallback.",
            proof_floor,
        )
        self.assertTrue(
            os.access(ROOT / "scripts/ai/verify_m113_gm_prep_packets.sh", os.X_OK),
            "scripts/ai/verify_m113_gm_prep_packets.sh should stay executable for worker-safe direct invocation.",
        )

    def test_generated_release_proof_tracks_m113_gm_prep_package(self):
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

        self.assertEqual(3813748639, package["frontier_id"])
        self.assertEqual(113, package["milestone_id"])
        self.assertEqual("complete", package["status"])
        self.assertEqual("verify_closed_package_only", package["completion_action"])
        self.assertEqual(PROOF_FLOOR_COMMIT, package["proof_floor_commit"])
        self.assertEqual(
            ["gm_prep_packets", "opposition_packet_artifacts"],
            package["owned_surfaces"],
        )

        for token in (
            "GM prep packet rendering stays render-verified by requiring a governed source pack id, source pack revision id, packet ref, and source entry id plus sibling-only payloads before media jobs enqueue, and JSON payloads must match those scope fields exactly instead of passing on substring mentions alone",
            "parseable JSON GM prep payloads fail closed when required scope fields are missing or mismatched, so JSON strings, arrays, or wrong-sibling objects cannot bypass exact scope matching through text fallback",
            "non-JSON GM prep payloads require exact keyed values or delimiter-safe scope tokens, so near-match source pack ids, packet refs, and source entry ids cannot pass by raw substring collision",
            "GM prep packet rendering requires at least one opposition entry and keeps scene and prep-library entries optional within the same governed render request",
            "GM prep packet entries require packet and preview artifacts while briefing artifacts stay optional per governed entry",
            "GM prep packet rendering rejects duplicate source entries and duplicate packet refs inside one governed render request",
            "bundle-scoped dedupe keys include governed source pack id, source pack revision id, rendering id, subject kind, source entry id, packet ref, artifact role, category, output format, and caller dedupe key",
            "receipt hashes use length-prefixed subject-kind, artifact-role, and output-format segments so delimiter-heavy GM prep variants cannot collapse distinct outputs onto one receipt id",
            "entry receipt ids and subject receipt group ids stay scoped to governed source pack id, source pack revision id, and rendering id so reused packet refs cannot alias grouped evidence across governed packs",
            "subject receipt groups preserve grouped entry ids, packet refs, packet receipt ids, preview receipt ids, optional briefing receipt ids, aggregate job ids, and grouped artifact rows so downstream shelves do not need to reconstruct governed prep evidence from raw artifact receipts",
            "GM prep packet artifact receipts preserve asset urls, approval state, retention state, and storage class alongside packet, preview, and optional briefing outputs",
            "rendered timestamps resolve from completed media jobs so later deduped retries cannot rewrite bundle render time with a newer request timestamp",
            "GM prep packet package authority requires exactly one canonical queue row per mirror and exactly one registry task block",
        ):
            self.assertIn(token, package["gm_prep_packet_guards"])

    def test_m113_proof_sources_stay_worker_safe(self):
        combined = "\n".join(
            (
                (ROOT / "docs/NEXT90_M113_GM_PREP_PACKET_PROOF_FLOOR.md").read_text(encoding="utf-8"),
                (ROOT / "scripts/ai/materialize_media_release_proof.py").read_text(encoding="utf-8"),
                (ROOT / "src/Chummer.Media.Factory.Runtime/Assets/GmPrepPacketBundleService.cs").read_text(
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
