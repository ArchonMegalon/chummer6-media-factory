from pathlib import Path
import json
import subprocess
import sys
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[1]
PACKAGE_ID = "next90-m111-media-factory-concierge-bundles"
PUBLISHED_RELEASE_PROOF = ROOT / ".codex-studio/published/MEDIA_LOCAL_RELEASE_PROOF.generated.json"
PUBLISHED_CERTIFICATION = ROOT / ".codex-studio/published/ARTIFACT_PUBLICATION_CERTIFICATION.generated.json"


def load_package(path: Path) -> dict:
    payload = json.loads(path.read_text(encoding="utf-8"))
    return next(candidate for candidate in payload["successor_packages"] if candidate["package_id"] == PACKAGE_ID)


class M111InstallAwareConciergeProofTests(unittest.TestCase):
    def test_proof_floor_records_timestamp_stability_and_verify_lane(self):
        proof_floor = (ROOT / "docs/NEXT90_M111_INSTALL_AWARE_CONCIERGE_PROOF_FLOOR.md").read_text(encoding="utf-8")

        for token in (
            "rendered timestamps must resolve from completed media jobs so later deduped retries cannot rewrite bundle render time with a newer request timestamp",
            "source and requested timestamp metadata must stay outside bundle-scoped dedupe, receipt identity, and companion-ready identity so replayed install-aware packets cannot fork stable jobs or receipt refs",
            "normalized artifact ordering plus normalized caption, preview, and sibling-note ref ordering must keep receipt ids, companion refs, ready refs, and grouped bundle/role/caption/preview/sibling-note receipt rows stable when callers reorder the same install-aware packet artifacts",
            "top-level caption, preview, and sibling-note aggregate refs must stay first-class receipt rows so downstream shelves can surface bundle-wide concierge evidence without reconstructing it from grouped rows",
            "bundle receipt groups must preserve aggregate receipt ids, job ids, companion refs, caption refs, preview refs, sibling note refs, roles, and grouped artifact rows for each release, support, and public concierge sibling bundle",
            "parseable JSON payloads fail closed when required scope fields are missing or the root payload is not an object, so JSON strings, arrays, or note-only objects cannot bypass exact install-aware scope matching through text fallback",
            "JSON and keyed text scope values trim surrounding whitespace before exact scope matching so padded install-aware payloads stay valid without reopening substring spoof paths",
            "install-aware concierge package authority requires exactly one canonical queue row per mirror and exactly one registry task block while the package remains unlanded",
            "`python3 -m unittest tests.test_m111_successor_package_authority tests.test_m111_install_aware_concierge_proof tests.test_install_aware_concierge_rendering` exits 0",
            "`dotnet run --project tests/InstallAwareConciergeSmoke/Chummer.Media.Factory.InstallAwareConciergeSmoke.csproj --configuration Release --nologo --verbosity quiet` exits 0",
            "`bash scripts/ai/verify_m111_install_aware_concierge.sh` exits 0",
            "`scripts/ai/verify_m111_install_aware_concierge.sh` gives the package one repo-local verifier entrypoint",
        ):
            self.assertIn(token, proof_floor)

    def test_generated_release_proof_tracks_m111_install_aware_package(self):
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

        self.assertEqual(
            "Render release explainer, support closure, and public concierge companion bundles with captions, previews, and sibling notes",
            package["title"],
        )
        self.assertEqual(
            "Render install-aware release, support, and public concierge companions with captions, previews, and bounded sibling notes.",
            package["task"],
        )
        self.assertEqual("111.3", package["work_task_id"])
        self.assertEqual(4132724850, package["frontier_id"])
        self.assertEqual(111, package["milestone_id"])
        self.assertEqual("W9", package["wave"])
        self.assertEqual("chummer6-media-factory", package["repo"])
        self.assertEqual("in_progress", package["status"])
        self.assertEqual("implementation_only", package["completion_action"])
        self.assertEqual("unlanded", package["proof_floor_commit"])
        self.assertEqual(["src", "tests", "docs", "scripts"], package["allowed_paths"])
        self.assertEqual(
            ["release_explainer_artifacts", "support_closure_artifacts", "public_concierge_companions"],
            package["owned_surfaces"],
        )

        for token in (
            "install-aware concierge payloads must stay scoped to the install-aware packet id, installed build receipt id, and artifact identity id before media jobs enqueue, JSON payloads must match those scope fields exactly, and non-JSON payloads must satisfy keyed or delimiter-safe scope matching instead of raw substring mentions alone",
            "request-level rendering id, install-aware packet id, installed build receipt id, artifact identity id, and source values normalize surrounding whitespace before scope enforcement so valid concierge requests do not fail on padded caller input",
            "parseable JSON payloads fail closed when required scope fields are missing or the root payload is not an object, so JSON strings, arrays, or note-only objects cannot bypass exact install-aware scope matching through text fallback",
            "JSON and keyed text scope values trim surrounding whitespace before exact scope matching so padded install-aware payloads stay valid without reopening substring spoof paths",
            "install-aware concierge bundles require release explainer, support closure, and public concierge siblings to each emit video, audio, and preview-card artifacts before the bundle can render",
            "install-aware concierge video and audio companions require caption refs while video and preview-card companions require preview refs",
            "install-aware concierge artifacts require at least one sibling note ref and keep sibling notes bounded to at most two refs per artifact",
            "companion refs are unique per install-aware packet so downstream shelves cannot confuse release, support, and public concierge outputs",
            "bundle-scoped dedupe keys include install-aware packet id, installed build receipt id, artifact identity id, rendering id, bundle kind, sibling role, category, output format, companion ref, and caller dedupe key",
            "source and requested timestamp metadata stay outside bundle-scoped dedupe, receipt identity, and companion-ready identity so replayed install-aware packets cannot fork stable jobs, receipt ids, or ready refs",
            "normalized artifact ordering plus normalized caption, preview, and sibling-note ref ordering keep receipt ids, companion refs, ready refs, and grouped bundle, role, caption, preview, and sibling-note receipt rows stable when callers reorder the same install-aware packet artifacts",
            "case-insensitive caption, preview, and sibling-note dedupe selects one canonical ref spelling before receipt hashing and aggregate ref emission so mixed-case duplicate refs stay stable when callers reorder them",
            "receipt hashes include caption, preview, and sibling-note refs so concierge receipts stay tied to their emitted release, support, and public siblings",
            "receipt hashes use length-prefixed caption, preview, and sibling-note ref segments so delimiter-heavy refs cannot collapse distinct concierge outputs onto one receipt id",
            "top-level caption, preview, and sibling-note aggregate refs stay first-class receipt rows so downstream shelves can surface bundle-wide concierge evidence without reconstructing it from grouped rows",
            "companion ready refs preserve per-artifact ref, receipt id, job id, job state, output format, caption refs, preview refs, sibling note refs, asset id, asset url, cache ttl, approval state, retention state, and storage class",
            "caption, preview, and sibling-note grouped receipt rows preserve bundle kinds, roles, and grouped asset urls so downstream shelves can publish first-class concierge evidence without reconstructing it from raw artifact receipts",
            "bundle, role, caption, preview, and sibling-note receipt groups preserve aggregate job ids, grouped companion refs, grouped bundle kinds or roles, and grouped artifact rows so downstream shelves do not need to reconstruct concierge evidence from raw artifact receipts",
            "install-aware concierge package authority requires exactly one canonical queue row per mirror and exactly one registry task block",
        ):
            self.assertIn(token, package["install_aware_concierge_guards"])

    def test_published_release_proof_and_certification_track_current_m111_entry(self):
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
            expected_release = load_package(Path(tmp) / "MEDIA_LOCAL_RELEASE_PROOF.generated.json")
            expected_certification = load_package(Path(tmp) / "ARTIFACT_PUBLICATION_CERTIFICATION.generated.json")

        published_release = load_package(PUBLISHED_RELEASE_PROOF)
        published_certification = load_package(PUBLISHED_CERTIFICATION)

        self.assertEqual(expected_release, published_release)
        self.assertEqual(expected_certification, published_certification)

    def test_m111_proof_sources_stay_worker_safe(self):
        combined = "\n".join(
            (
                (ROOT / "docs/NEXT90_M111_INSTALL_AWARE_CONCIERGE_PROOF_FLOOR.md").read_text(encoding="utf-8"),
                (ROOT / "scripts/ai/materialize_media_release_proof.py").read_text(encoding="utf-8"),
                (ROOT / "src/Chummer.Media.Factory.Runtime/Assets/InstallAwareConciergeBundleService.cs").read_text(
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
