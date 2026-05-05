from pathlib import Path
import json
import os
import subprocess
import sys
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[1]
PACKAGE_ID = "next90-m119-media-factory-starter-artifacts"
PUBLISHED_RELEASE_PROOF = ROOT / ".codex-studio/published/MEDIA_LOCAL_RELEASE_PROOF.generated.json"
PUBLISHED_CERTIFICATION = ROOT / ".codex-studio/published/ARTIFACT_PUBLICATION_CERTIFICATION.generated.json"
PROOF_FLOOR_COMMIT = "TO_BE_FILLED_M119_COMMIT"
ALLOWED_PROOF_PREFIXES = ("src/", "tests/", "docs/", "scripts/")


class M119StarterArtifactProofTests(unittest.TestCase):
    def assert_single_package(self, payload: dict) -> dict:
        matches = [candidate for candidate in payload["successor_packages"] if candidate["package_id"] == PACKAGE_ID]
        self.assertEqual(1, len(matches), f"expected exactly one published {PACKAGE_ID} successor package entry")
        return matches[0]

    def assert_allowed_proof_paths(self, package: dict) -> None:
        for proof_path in package["proof"]:
            self.assertTrue(
                proof_path.startswith(ALLOWED_PROOF_PREFIXES),
                f"M119 proof citation drifted outside the allowed package paths: {proof_path}",
            )

    def test_proof_floor_records_direct_verify_entrypoint(self):
        proof_floor = (ROOT / "docs/NEXT90_M119_STARTER_ARTIFACT_PROOF_FLOOR.md").read_text(encoding="utf-8")

        self.assertIn(
            "`scripts/ai/verify_m119_starter_artifacts.sh` stays directly executable so workers can invoke the package verifier without wrapping it in an ad hoc shell fallback.",
            proof_floor,
        )
        self.assertTrue(
            os.access(ROOT / "scripts/ai/verify_m119_starter_artifacts.sh", os.X_OK),
            "scripts/ai/verify_m119_starter_artifacts.sh should stay executable for worker-safe direct invocation.",
        )

    def test_generated_release_proof_tracks_m119_starter_artifact_package(self):
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

        self.assertEqual(1413666751, package["frontier_id"])
        self.assertEqual(119, package["milestone_id"])
        self.assertEqual("Render starter primer and first-session companion artifacts", package["title"])
        self.assertEqual(
            "Produce localized starter primers, first-session briefings, and support-safe onboarding artifacts from approved source packs.",
            package["task"],
        )
        self.assertEqual("119.4", package["work_task_id"])
        self.assertEqual("W14", package["wave"])
        self.assertEqual("chummer6-media-factory", package["repo"])
        self.assertEqual("complete", package["status"])
        self.assertEqual("verify_closed_package_only", package["completion_action"])
        self.assertEqual(PROOF_FLOOR_COMMIT, package["proof_floor_commit"])
        self.assertEqual(["src", "tests", "docs", "scripts"], package["allowed_paths"])
        self.assertEqual(
            ["starter_primer_artifacts", "first_session_briefing_artifacts"],
            package["owned_surfaces"],
        )

        for token in (
            "starter artifact rendering stays render-verified by requiring an approved starter source pack id, source pack revision id, starter lane id, and per-artifact locale plus sibling-only payloads before media jobs enqueue, and JSON payloads must match those scope fields exactly instead of passing on substring mentions alone",
            "parseable JSON starter artifact payloads fail closed when required scope fields are missing or mismatched, so JSON strings, arrays, or wrong-sibling objects cannot bypass exact scope matching through text fallback",
            "non-JSON starter artifact payloads require exact keyed values or delimiter-safe scope tokens, so near-match source pack ids, starter lane ids, and locales cannot pass by raw substring collision",
            "starter artifact bundles require requested-locale and fallback locale triads for starter primer, first-session briefing, and support-safe onboarding siblings, while fallback locales stay bounded to at most two locales",
            "starter primer and first-session video/audio siblings require caption refs, video and preview-card siblings require preview refs, and support-safe onboarding siblings require bounded support-note refs",
            "starter artifact rendering rejects duplicate artifact refs inside one starter-lane render request",
            "bundle-scoped dedupe keys include approved starter source pack id, source pack revision id, starter lane id, bundle kind, role, locale, category, output format, artifact ref, and caller dedupe key",
            "receipt hashes include caption, preview, and support-note refs so starter receipts stay tied to emitted companion evidence",
            "receipt hashes use length-prefixed locale, output-format, caption, preview, and support-note segments so delimiter-heavy starter variants cannot collapse distinct outputs onto one receipt id",
            "normalized sibling ordering keeps receipt ids, ready refs, locale receipt groups, bundle-locale receipt groups, and aggregate ref receipts stable when callers reorder the same starter siblings",
            "case-insensitive caption, preview, and support-note dedupe selects one canonical ref spelling before receipt hashing and aggregate ref emission so mixed-case duplicate refs stay stable when callers reorder them",
            "rendered timestamps resolve from completed media jobs so later deduped retries cannot rewrite bundle render time with a newer request timestamp",
            "starter artifact package authority requires exactly one canonical queue row per mirror and exactly one registry task block",
            "generated and published proof artifacts require exactly one M119 successor package entry before drift comparison",
            "generated proof requires unique M119 proof citations and unique starter artifact guard rows before drift comparison so closed-package evidence cannot silently duplicate itself while still matching canonical mirrors",
            "queue and registry mirrors must match the canonical M119 package and task blocks exactly so repo-local proof cannot drift on status or scoped fields",
            "proof citations stay anchored to repo-local src, tests, docs, and scripts paths so closed-package evidence cannot drift into sibling surfaces",
            "canonical successor queue rows are now complete with `landed_commit: TO_BE_FILLED_M119_COMMIT`, so future slices can close this package only from the landed proof floor.",
        ):
            self.assertIn(token, package["starter_artifact_guards"])

        self.assertEqual(len(package["proof"]), len(set(package["proof"])))
        self.assertEqual(
            len(package["starter_artifact_guards"]),
            len(set(package["starter_artifact_guards"])),
        )
        self.assert_allowed_proof_paths(package)

        published_release = json.loads(PUBLISHED_RELEASE_PROOF.read_text(encoding="utf-8"))
        published_certification = json.loads(PUBLISHED_CERTIFICATION.read_text(encoding="utf-8"))
        published_release_package = self.assert_single_package(published_release)
        published_certification_package = self.assert_single_package(published_certification)
        self.assertEqual(package, published_release_package)
        self.assertEqual(package, published_certification_package)
        self.assertEqual(len(published_release_package["proof"]), len(set(published_release_package["proof"])))
        self.assertEqual(
            len(published_release_package["starter_artifact_guards"]),
            len(set(published_release_package["starter_artifact_guards"])),
        )
        self.assertEqual(
            len(published_certification_package["proof"]),
            len(set(published_certification_package["proof"])),
        )
        self.assertEqual(
            len(published_certification_package["starter_artifact_guards"]),
            len(set(published_certification_package["starter_artifact_guards"])),
        )
        self.assert_allowed_proof_paths(published_release_package)
        self.assert_allowed_proof_paths(published_certification_package)

    def test_m119_proof_sources_stay_worker_safe(self):
        combined = "\n".join(
            (
                (ROOT / "docs/NEXT90_M119_STARTER_ARTIFACT_PROOF_FLOOR.md").read_text(encoding="utf-8"),
                (ROOT / "scripts/ai/verify_m119_starter_artifacts.sh").read_text(encoding="utf-8"),
                (ROOT / "scripts/ai/materialize_media_release_proof.py").read_text(encoding="utf-8"),
                (ROOT / "src/Chummer.Media.Factory.Runtime/Assets/StarterArtifactBundleService.cs").read_text(
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
