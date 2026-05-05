from pathlib import Path
import json
import subprocess
import sys
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[1]
PACKAGE_ID = "next90-m145-media-factory-explain-presenter-siblings"
PUBLISHED_RELEASE_PROOF = ROOT / ".codex-studio/published/MEDIA_LOCAL_RELEASE_PROOF.generated.json"
PUBLISHED_CERTIFICATION = ROOT / ".codex-studio/published/ARTIFACT_PUBLICATION_CERTIFICATION.generated.json"


def load_package(path: Path) -> dict:
    payload = json.loads(path.read_text(encoding="utf-8"))
    return next(candidate for candidate in payload["successor_packages"] if candidate["package_id"] == PACKAGE_ID)


class M145ExplainPresenterProofTests(unittest.TestCase):
    def test_proof_floor_tracks_required_presenter_receipt_language(self):
        proof_floor = (ROOT / "docs/NEXT90_M145_EXPLAIN_PRESENTER_SIBLINGS_PROOF_FLOOR.md").read_text(encoding="utf-8")

        for token in (
            PACKAGE_ID,
            "2090633046",
            "ExplainPresenterTextFallbackReceipt",
            "ExplainPresenterSiblingRoleReceiptGroup",
            "verify_closed_package_only",
            "7d5a0167",
            "explain presenter package authority requires exactly one canonical queue row per mirror and exactly one registry task block",
            "canonical successor queue rows are now complete with `landed_commit: 7d5a0167`, so future slices can close this package only from the landed proof floor.",
            "queue and registry mirrors must match the canonical M145 package and task blocks exactly",
            "`scripts/ai/verify_m145_explain_presenter_siblings.sh` gives the package one repo-local verifier entrypoint",
            "published release proof and publication certification snapshots track the current M145 package entry exactly",
            "package-scoped proof is green for the current M145 worktree",
            "case-insensitive duplicate caption and preview refs canonicalize to one stable spelling before grouped receipt rows emit",
            "role, caption, and preview receipt groups preserve aggregate job ids, grouped companion refs, and grouped artifact rows so downstream shelves do not need to reconstruct explain presenter evidence from raw artifact receipts",
            "companion ready refs preserve per-artifact ref, receipt id, job id, job state, output format, caption refs, preview refs, asset id, asset url, cache ttl, approval state, retention state, and storage class",
        ):
            self.assertIn(token, proof_floor, token)

    def test_package_verify_entrypoint_materializes_release_proof_and_worker_safe_checks(self):
        verify_script = (ROOT / "scripts/ai/verify_m145_explain_presenter_siblings.sh").read_text(encoding="utf-8")

        for token in (
            "mktemp -d",
            "trap 'rm -rf \"$tmp_dir\"' EXIT",
            "python3 scripts/ai/materialize_media_release_proof.py",
            "--out-dir \"$tmp_dir\"",
            "$tmp_dir/MEDIA_LOCAL_RELEASE_PROOF.generated.json",
            "$tmp_dir/ARTIFACT_PUBLICATION_CERTIFICATION.generated.json",
            "published MEDIA_LOCAL_RELEASE_PROOF.generated.json drifted from the current M145 package entry",
            "published ARTIFACT_PUBLICATION_CERTIFICATION.generated.json drifted from the current M145 package entry",
            ".codex-studio/published/MEDIA_LOCAL_RELEASE_PROOF.generated.json",
            ".codex-studio/published/ARTIFACT_PUBLICATION_CERTIFICATION.generated.json",
            "docs/NEXT90_M145_EXPLAIN_PRESENTER_SIBLINGS_PROOF_FLOOR.md",
            "docs/MEDIA_CAPABILITY_SIGNOFF.md",
            "next90-m145-media-factory-explain-presenter-siblings",
            "tests.test_m145_successor_package_authority",
            "tests.test_m145_explain_presenter_proof",
            "tests.test_explain_presenter_sibling_rendering",
            "tests/ExplainPresenterSiblingSmoke/Chummer.Media.Factory.ExplainPresenterSiblingSmoke.csproj",
            "2090633046",
            "ExplainPresenterTextFallbackReceipt",
            "ExplainPresenterSiblingRoleReceiptGroup",
            "verify_closed_package_only",
            "7d5a0167",
            "\"status\":[[:space:]]+\"complete\"",
            "explain presenter sibling receipts stay render-verified",
            "canonical successor queue rows are now complete with `landed_commit: 7d5a0167`, so future slices can close this package only from the landed proof floor.",
            "rendered timestamps resolve from completed media jobs",
            "queue and registry mirrors must match the canonical M145 package and task blocks exactly",
            "TASK_LOCAL_TELEMETRY\\.generated\\.json",
            "ACTIVE_RUN_HANDOFF\\.generated\\.md",
            "operator[[:space:]]+telemetry",
            "supervisor[[:space:]]+status",
            "status[[:space:]]+query",
            "eta[[:space:]]+query",
            "active-run[[:space:]]+helper",
            "verify failed: M145 explain presenter proof sources must stay worker-safe and must not cite blocked run-helper context",
        ):
            self.assertIn(token, verify_script, token)

    def test_media_capability_signoff_mentions_m145_presenter_rendering_guards(self):
        signoff = (ROOT / "docs/MEDIA_CAPABILITY_SIGNOFF.md").read_text(encoding="utf-8")

        for token in (
            "explain presenter sibling receipts stay render-verified",
            "first-party text fallback",
            "grounding-scope fields are missing",
            "caption refs while presenter-video siblings require preview refs",
            "caption and preview refs dedupe case-insensitively",
            "duplicate companion refs inside one approved explanation packet",
            "source or requested timestamp drift cannot fork stable job ids",
            "rendered timestamps resolve from completed media jobs",
        ):
            self.assertIn(token, signoff, token)

    def test_generated_release_proof_tracks_m145_presenter_package(self):
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

        self.assertEqual(2090633046, package["frontier_id"])
        self.assertEqual(145, package["milestone_id"])
        self.assertEqual("145.5", package["work_task_id"])
        self.assertEqual("W28", package["wave"])
        self.assertEqual("chummer6-media-factory", package["repo"])
        self.assertEqual(["src", "tests", "docs", "scripts"], package["allowed_paths"])
        self.assertEqual(
            ["explain_presenter_siblings:media_factory", "explain_audio_video:media_factory"],
            package["owned_surfaces"],
        )

        for token in (
            "request-level rendering id, approved explanation packet id, explanation packet revision id, grounding scope ref, source, and first-party text fallback normalize surrounding whitespace before scope enforcement, dedupe, and receipt emission so valid padded retries keep stable job ids, receipt ids, and text fallback receipts",
            "null artifact lists and null sibling entries fail closed with explicit request-scoped validation before approved explanation packet normalization continues",
            "explain presenter payloads must stay scoped to the approved explanation packet id, explanation packet revision id, and grounding scope ref before media jobs enqueue, and JSON payloads must match those scope fields exactly instead of passing on substring mentions alone",
            "parseable JSON payloads fail closed when required packet-identity or grounding-scope fields are missing or the root payload is not an object, so JSON strings, arrays, or note-only objects cannot bypass exact scope matching through text fallback",
            "non-JSON scope fallback requires exact keyed values or delimited scope tokens so near-match packet, revision, or grounding-scope ids cannot slip through by substring collision",
            "case-insensitive duplicate caption and preview refs canonicalize to one stable spelling before grouped receipt rows emit so mixed-case ref variants cannot rewrite aggregate receipt casing when callers reorder the same approved explanation packet siblings",
            "receipt hashes include caption refs, preview refs, and first-party text fallback so sibling receipts stay tied to their emitted explanation packet fallback posture",
            "first-party text fallback stays first-class in the render receipt and text fallback receipt so optional media surfaces never become the only explain surface",
            "canonical successor queue rows are now complete with `landed_commit: 7d5a0167`, so future slices can close this package only from the landed proof floor.",
            "queue and registry mirrors must match the canonical M145 package and task blocks exactly so repo-local proof cannot drift on status or scoped fields",
        ):
            self.assertIn(token, package["explain_presenter_guards"])

        for proof_path in (
            "src/Chummer.Media.Factory.Runtime/Assets/ExplainPresenterSiblingRenderingService.cs",
            "src/Chummer.Media.Contracts/Compatibility/RunServices/MediaFactoryContracts.cs",
            "src/Chummer.Media.Contracts/README.md",
            "tests/ExplainPresenterSiblingSmoke/Program.cs",
            "tests/test_explain_presenter_sibling_rendering.py",
            "tests/test_m145_successor_package_authority.py",
            "tests/test_m145_explain_presenter_proof.py",
            "docs/NEXT90_M145_EXPLAIN_PRESENTER_SIBLINGS_PROOF_FLOOR.md",
            "scripts/ai/verify_m145_explain_presenter_siblings.sh",
        ):
            self.assertIn(proof_path, package["proof"])

    def test_published_release_proof_and_certification_track_current_m145_entry(self):
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

    def test_m145_proof_sources_stay_worker_safe(self):
        combined = "\n".join(
            (
                (ROOT / "docs/NEXT90_M145_EXPLAIN_PRESENTER_SIBLINGS_PROOF_FLOOR.md").read_text(encoding="utf-8"),
                (ROOT / "scripts/ai/materialize_media_release_proof.py").read_text(encoding="utf-8"),
                (ROOT / "src/Chummer.Media.Factory.Runtime/Assets/ExplainPresenterSiblingRenderingService.cs").read_text(
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
