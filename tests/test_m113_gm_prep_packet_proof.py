from pathlib import Path
import json
import os
import subprocess
import sys
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[1]
PACKAGE_ID = "next90-m113-media-factory-gm-prep-packets"
PUBLISHED_RELEASE_PROOF = ROOT / ".codex-studio/published/MEDIA_LOCAL_RELEASE_PROOF.generated.json"
PUBLISHED_CERTIFICATION = ROOT / ".codex-studio/published/ARTIFACT_PUBLICATION_CERTIFICATION.generated.json"
PROOF_FLOOR_COMMIT = "7d5a0167"
PROOF_FLOOR_SUMMARY = (
    "Pin M113 governed GM prep packet closure with opposition-required entries, optional briefing siblings, "
    "and first-class subject receipt groups"
)
ALLOWED_PROOF_PREFIXES = ("src/", "tests/", "docs/", "scripts/")
WORKER_SAFE_PROOF_SOURCES = (
    "src/Chummer.Media.Factory.Runtime/Assets/GmPrepPacketBundleService.cs",
    "src/Chummer.Media.Contracts/Compatibility/RunServices/MediaFactoryContracts.cs",
    "tests/GmPrepPacketSmoke/Program.cs",
    "tests/test_gm_prep_packet_rendering.py",
    "tests/test_m113_gm_prep_packet_proof.py",
    "tests/test_m113_successor_package_authority.py",
    "docs/NEXT90_M113_GM_PREP_PACKET_PROOF_FLOOR.md",
    "scripts/ai/materialize_media_release_proof.py",
    "scripts/ai/verify_m113_gm_prep_packets.sh",
    "scripts/ai/verify.sh",
)
EXPECTED_PROOF = (
    "src/Chummer.Media.Factory.Runtime/Assets/GmPrepPacketBundleService.cs",
    "src/Chummer.Media.Contracts/Compatibility/RunServices/MediaFactoryContracts.cs",
    "tests/GmPrepPacketSmoke/Program.cs",
    "tests/test_gm_prep_packet_rendering.py",
    "tests/test_m113_gm_prep_packet_proof.py",
    "tests/test_m113_successor_package_authority.py",
    "docs/NEXT90_M113_GM_PREP_PACKET_PROOF_FLOOR.md",
    "scripts/ai/materialize_media_release_proof.py",
    "scripts/ai/verify_m113_gm_prep_packets.sh",
    "scripts/ai/verify.sh",
)
EXPECTED_ARTIFACT_ROLES = (
    "GmPrepOppositionPacket",
    "GmPrepOppositionPreview",
    "GmPrepOppositionBriefing",
    "GmPrepScenePacket",
    "GmPrepScenePreview",
    "GmPrepSceneBriefing",
    "GmPrepLibraryPacket",
    "GmPrepLibraryPreview",
    "GmPrepLibraryBriefing",
)
EXPECTED_RECEIPT_ROWS = (
    "EntryReceipts",
    "GmPrepPacketEntryReceipt",
    "SubjectReceiptGroups",
    "GmPrepPacketSubjectReceiptGroup",
    "PacketReceiptIds",
    "PreviewReceiptIds",
    "BriefingReceiptIds",
    "OppositionPacketReceiptIds",
    "ScenePacketReceiptIds",
    "PrepLibraryPacketReceiptIds",
    "PacketRefs",
    "JobIds",
    "ArtifactReceipts",
    "AssetUrl",
    "ApprovalState",
    "RetentionState",
    "StorageClass",
)


def worker_unsafe_tokens() -> tuple[str, ...]:
    return (
        "".join(("task", "_local", "_telemetry", ".generated", ".json")),
        "".join(("active", "_run", "_handoff", ".generated", ".md")),
        " ".join(("operator", "telemetry")),
        " ".join(("supervisor", "status")),
        " ".join(("status", "query")),
        " ".join(("eta", "query")),
        "-".join(("active", "run")) + " helper",
    )


class M113GmPrepPacketProofTests(unittest.TestCase):
    def test_worker_safe_source_set_matches_cited_m113_proof_files(self):
        self.assertEqual(EXPECTED_PROOF, WORKER_SAFE_PROOF_SOURCES)

    def assert_single_package(self, payload: dict) -> dict:
        matches = [candidate for candidate in payload["successor_packages"] if candidate["package_id"] == PACKAGE_ID]
        self.assertEqual(1, len(matches), f"expected exactly one published {PACKAGE_ID} successor package entry")
        return matches[0]

    def assert_allowed_proof_paths(self, package: dict) -> None:
        for proof_path in package["proof"]:
            self.assertTrue(
                proof_path.startswith(ALLOWED_PROOF_PREFIXES),
                f"M113 proof citation drifted outside the allowed package paths: {proof_path}",
            )
            self.assertTrue(
                (ROOT / proof_path).is_file(),
                f"M113 proof citation does not resolve to a repo-local file: {proof_path}",
            )

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
            certification = json.loads(
                (Path(tmp) / "ARTIFACT_PUBLICATION_CERTIFICATION.generated.json").read_text(encoding="utf-8")
            )

        package = self.assert_single_package(release)
        self.assert_single_package(certification)

        self.assertEqual(3813748639, package["frontier_id"])
        self.assertEqual(113, package["milestone_id"])
        self.assertEqual("Render opposition and GM prep packets from governed source packs", package["title"])
        self.assertEqual(
            "Produce packet, preview, and optional briefing artifacts for opposition, scenes, and prep-library entries.",
            package["task"],
        )
        self.assertEqual("113.4", package["work_task_id"])
        self.assertEqual("W11", package["wave"])
        self.assertEqual("chummer6-media-factory", package["repo"])
        self.assertEqual("complete", package["status"])
        self.assertEqual("7d5a0167", package["landed_commit"])
        self.assertEqual("verify_closed_package_only", package["completion_action"])
        self.assertEqual(PROOF_FLOOR_COMMIT, package["proof_floor_commit"])
        self.assertEqual(PROOF_FLOOR_SUMMARY, package["proof_floor_summary"])
        self.assertEqual(["src", "tests", "docs", "scripts"], package["allowed_paths"])
        self.assertEqual(
            ["gm_prep_packets", "opposition_packet_artifacts"],
            package["owned_surfaces"],
        )
        self.assertEqual(list(EXPECTED_PROOF), package["proof"])
        self.assertEqual(list(EXPECTED_ARTIFACT_ROLES), package["artifact_roles"])
        self.assertEqual(list(EXPECTED_RECEIPT_ROWS), package["receipt_rows"])

        for token in (
            "GM prep packet rendering stays render-verified by requiring a governed source pack id, source pack revision id, packet ref, and source entry id plus sibling-only payloads before media jobs enqueue, and JSON payloads must match those scope fields exactly instead of passing on substring mentions alone",
            "parseable JSON GM prep payloads fail closed when required scope fields are missing or mismatched, so JSON strings, arrays, or wrong-sibling objects cannot bypass exact scope matching through text fallback",
            "non-JSON GM prep payloads require exact keyed values or delimiter-safe scope tokens, so near-match source pack ids, packet refs, and source entry ids cannot pass by raw substring collision",
            "JSON and keyed text GM prep scope values trim surrounding whitespace before exact scope matching so padded governed payloads stay valid without reopening substring spoof paths",
            "GM prep packet rendering fails closed when the request contains null entries or a governed entry drops its required packet or preview artifact before normalization continues",
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
            "generated and published proof artifacts require exactly one M113 successor package entry before drift comparison",
            "generated proof requires unique M113 proof citations and unique GM prep guard rows before drift comparison so closed-package evidence cannot silently duplicate itself while still matching canonical mirrors",
            "generated proof requires unique M113 artifact roles and unique receipt rows before drift comparison so closed-package evidence cannot silently duplicate rendered sibling claims or receipt surfaces while still matching canonical mirrors",
            "generated proof requires the exact pinned M113 proof citations, artifact roles, and receipt rows before drift comparison so closed-package evidence cannot quietly add sibling surfaces while still matching canonical mirrors",
            "generated and published proof artifacts now pin the exact M113 GM prep guard rows directly on the successor package entry, so repo-local closure proof cannot silently rewrite the closed-package scope rules while still matching on identity alone",
            "queue and registry mirrors must match the canonical M113 package and task blocks exactly so repo-local proof cannot drift on status or scoped fields",
            "proof citations stay anchored to repo-local src, tests, docs, and scripts paths so closed-package evidence cannot drift into sibling surfaces",
            "every pinned M113 proof citation must resolve to a repo-local file before generated or published closure proof can stay green, so closed-package evidence cannot cite deleted surfaces while still matching on strings alone",
            "generated and published proof artifacts now pin `landed_commit: 7d5a0167` directly on the M113 successor package entry, so repo-local closure proof cannot drift behind the canonical queue receipt while still reporting `status: complete`.",
            "canonical successor queue rows are now complete with `landed_commit: 7d5a0167`, so future slices can close this package only from the landed proof floor.",
        ):
            self.assertIn(token, package["gm_prep_packet_guards"])

        self.assertEqual(len(package["proof"]), len(set(package["proof"])))
        self.assertEqual(
            len(package["gm_prep_packet_guards"]),
            len(set(package["gm_prep_packet_guards"])),
        )
        self.assertEqual(len(package["artifact_roles"]), len(set(package["artifact_roles"])))
        self.assertEqual(len(package["receipt_rows"]), len(set(package["receipt_rows"])))
        self.assert_allowed_proof_paths(package)

        published_release = json.loads(PUBLISHED_RELEASE_PROOF.read_text(encoding="utf-8"))
        published_certification = json.loads(PUBLISHED_CERTIFICATION.read_text(encoding="utf-8"))
        published_release_package = self.assert_single_package(published_release)
        published_certification_package = self.assert_single_package(published_certification)
        self.assertEqual(package, published_release_package)
        self.assertEqual(package, published_certification_package)
        self.assertEqual(len(published_release_package["proof"]), len(set(published_release_package["proof"])))
        self.assertEqual(
            len(published_release_package["gm_prep_packet_guards"]),
            len(set(published_release_package["gm_prep_packet_guards"])),
        )
        self.assertEqual(
            len(published_release_package["artifact_roles"]),
            len(set(published_release_package["artifact_roles"])),
        )
        self.assertEqual(
            len(published_release_package["receipt_rows"]),
            len(set(published_release_package["receipt_rows"])),
        )
        self.assertEqual(
            len(published_certification_package["proof"]),
            len(set(published_certification_package["proof"])),
        )
        self.assertEqual(
            len(published_certification_package["gm_prep_packet_guards"]),
            len(set(published_certification_package["gm_prep_packet_guards"])),
        )
        self.assertEqual(
            len(published_certification_package["artifact_roles"]),
            len(set(published_certification_package["artifact_roles"])),
        )
        self.assertEqual(
            len(published_certification_package["receipt_rows"]),
            len(set(published_certification_package["receipt_rows"])),
        )
        self.assert_allowed_proof_paths(published_release_package)
        self.assert_allowed_proof_paths(published_certification_package)

    def test_m113_proof_sources_stay_worker_safe(self):
        combined = "\n".join(
            (ROOT / proof_source).read_text(encoding="utf-8")
            for proof_source in WORKER_SAFE_PROOF_SOURCES
        ).lower()

        for token in worker_unsafe_tokens():
            self.assertNotIn(token, combined, token)


if __name__ == "__main__":
    unittest.main()
