from pathlib import Path
import json
import subprocess
import sys
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[1]
PACKAGE_ID = "next90-m109-media-factory-build-explain-bundles"
PUBLISHED_RELEASE_PROOF = ROOT / ".codex-studio/published/MEDIA_LOCAL_RELEASE_PROOF.generated.json"
PUBLISHED_CERTIFICATION = ROOT / ".codex-studio/published/ARTIFACT_PUBLICATION_CERTIFICATION.generated.json"
PROOF_FLOOR = ROOT / "docs/NEXT90_M109_BUILD_EXPLAIN_COMPANION_PROOF_FLOOR.md"
SIGNOFF = ROOT / "docs/MEDIA_CAPABILITY_SIGNOFF.md"
VERIFY_SCRIPT = ROOT / "scripts/ai/verify_m109_build_explain_companion.sh"
MATERIALIZER = ROOT / "scripts/ai/materialize_media_release_proof.py"
SERVICE = ROOT / "src/Chummer.Media.Factory.Runtime/Assets/BuildExplainCompanionRenderingService.cs"


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def load_package(path: Path) -> dict:
    payload = json.loads(read(path))
    return next(candidate for candidate in payload["successor_packages"] if candidate["package_id"] == PACKAGE_ID)


class M109BuildExplainProofTests(unittest.TestCase):
    def test_proof_floor_tracks_required_companion_receipt_language(self):
        proof_floor = read(PROOF_FLOOR)

        for token in (
            PACKAGE_ID,
            "4037265286",
            "BuildExplainCompanionReadyRef",
            "BuildExplainCompanionRoleReceiptGroup",
            "verify_closed_package_only",
            "7d5a0167",
            "build explain package authority requires exactly one canonical queue row per mirror and exactly one registry task block",
            "generated and published proof artifacts now require exactly one M109 successor package entry before package drift checks run",
            "queue and registry mirrors must now match the canonical M109 package/task blocks exactly",
            "package-scoped proof is green for the current M109 worktree",
            "published `MEDIA_LOCAL_RELEASE_PROOF.generated.json` and `ARTIFACT_PUBLICATION_CERTIFICATION.generated.json` entries must match a freshly materialized M109 package entry exactly",
            "canonical successor queue rows are now complete with `landed_commit: 7d5a0167`, so future slices can close this package only from the landed proof floor.",
            "proof citations now stay anchored to repo-local `src`, `tests`, `docs`, and `scripts` paths only",
            "rejects worker-unsafe blocked run-helper citations in proof sources",
        ):
            self.assertIn(token, proof_floor, token)

    def test_media_capability_signoff_mentions_m109_companion_rendering_receipt_guards(self):
        signoff = read(SIGNOFF)

        for token in (
            "build explain companion receipts stay render-verified",
            "null artifact lists and null sibling entries",
            "approved explain packet id or explain packet revision id",
            "JSON and keyed text build explain scope values trim surrounding whitespace",
            "build explain caption and preview refs trim surrounding whitespace",
            "build explain caption and preview refs dedupe case-insensitively",
            "trims surrounding whitespace and rejects case-insensitive duplicate companion refs",
            "duplicate companion refs inside one approved explain packet",
            "source or requested timestamp drift cannot fork stable job ids, receipt ids, ready refs, or grouped role receipts",
        ):
            self.assertIn(token, signoff, token)

    def test_package_verify_entrypoint_materializes_release_proof_and_signoff_checks(self):
        verify_script = read(VERIFY_SCRIPT)

        for token in (
            "mktemp -d",
            "trap 'rm -rf \"$tmp_dir\"' EXIT",
            "python3 scripts/ai/materialize_media_release_proof.py",
            "--out-dir \"$tmp_dir\"",
            "$tmp_dir/MEDIA_LOCAL_RELEASE_PROOF.generated.json",
            "$tmp_dir/ARTIFACT_PUBLICATION_CERTIFICATION.generated.json",
            ".codex-studio/published/MEDIA_LOCAL_RELEASE_PROOF.generated.json",
            ".codex-studio/published/ARTIFACT_PUBLICATION_CERTIFICATION.generated.json",
            "docs/NEXT90_M109_BUILD_EXPLAIN_COMPANION_PROOF_FLOOR.md",
            "docs/MEDIA_CAPABILITY_SIGNOFF.md",
            "next90-m109-media-factory-build-explain-bundles",
            "tests.test_m109_successor_package_authority",
            "tests.test_m109_build_explain_proof",
            "tests.test_build_explain_companion_rendering",
            "tests/BuildExplainCompanionSmoke/Chummer.Media.Factory.BuildExplainCompanionSmoke.csproj",
            "4037265286",
            "BuildExplainCompanionReadyRef",
            "BuildExplainCompanionRoleReceiptGroup",
            "verify_closed_package_only",
            "7d5a0167",
            "\"status\":[[:space:]]+\"complete\"",
            "build explain companion receipts stay render-verified",
            "null artifact lists and null sibling entries",
            "canonical successor queue rows are now complete with `landed_commit: 7d5a0167`, so future slices can close this package only from the landed proof floor.",
            "queue and registry mirrors must match the canonical M109 package and task blocks exactly",
            "build explain caption and preview refs dedupe case-insensitively",
            "duplicate companion refs inside one approved explain packet",
            "source or requested timestamp drift cannot fork stable job ids, receipt ids, ready refs, or grouped role receipts",
            "TASK_LOCAL_TELEMETRY\\.generated\\.json",
            "ACTIVE_RUN_HANDOFF\\.generated\\.md",
            "operator[[:space:]]+telemetry",
            "supervisor[[:space:]]+status",
            "status[[:space:]]+query",
            "eta[[:space:]]+query",
            "active-run[[:space:]]+helper",
            "verify failed: published MEDIA_LOCAL_RELEASE_PROOF.generated.json drifted from the current M109 package entry",
            "verify failed: published ARTIFACT_PUBLICATION_CERTIFICATION.generated.json drifted from the current M109 package entry",
            "verify failed: {package_name} cited proof outside the M109 allowed paths: {proof_path}",
            "verify failed: expected exactly one {package_id} successor package entry in {path.name}, found {len(matches)}",
            "verify failed: {path.name} repeated successor package ids:",
            "proof citations stay anchored to repo-local src, tests, docs, and scripts paths so closed-package evidence cannot drift into sibling surfaces",
            "verify failed: M109 build explain proof sources must stay worker-safe and must not cite blocked run-helper context",
        ):
            self.assertIn(token, verify_script, token)

    def test_generated_release_proof_tracks_m109_build_explain_package(self):
        proof_floor = read(PROOF_FLOOR)

        for token in (
            "Current closure posture:",
            "./scripts/ai/verify_m109_build_explain_companion.sh",
            "package-scoped proof is green for the current M109 worktree",
            "canonical successor queue rows now pin the assigned M109 frontier id `4037265286`",
            "repo-local `.codex-design` queue and registry mirrors pin that same M109 frontier and task identity, and queue plus registry can cite `landed_commit: 7d5a0167` honestly",
            "future slices can close this package only from the landed proof floor",
            "future shards should reuse this proof floor and the package-scoped verify results",
            "published `MEDIA_LOCAL_RELEASE_PROOF.generated.json` and `ARTIFACT_PUBLICATION_CERTIFICATION.generated.json` entries must match a freshly materialized M109 package entry exactly",
            "request-level `RenderingId`, approved explain packet id, explain packet revision id, and source normalize surrounding whitespace before scope enforcement, dedupe, and receipt emission",
            "null artifact lists and null sibling entries now fail closed with explicit request-scoped validation",
            "JSON and keyed text scope values now trim surrounding whitespace before exact scope matching",
            "caption and preview refs now trim surrounding whitespace before grouped receipt rows and receipt hashes emit",
            "valid JSON payloads now fail closed when required scope fields are missing",
            "case-insensitive duplicate caption and preview refs now canonicalize to one stable spelling before grouped receipt rows emit",
            "normalized sibling ordering keeps receipt ids, companion refs, ready refs, and grouped role/caption/preview receipt rows stable",
            "source or requested timestamp drift cannot rewrite stable job ids, receipt ids, ready refs, or grouped role receipts for the same approved explain packet siblings",
            "build explain package authority now requires exactly one canonical queue row per mirror, exactly one repo-local `.codex-design` queue mirror row, and exactly one registry task block per canonical and repo-local mirror",
            "generated and published proof artifacts now require exactly one M109 successor package entry before package drift checks run",
            "package-scoped queue and registry mirrors must now match the canonical M109 package/task blocks exactly",
            "generated M109 package entries now match the canonical queue scope directly and the canonical registry task owner/title scope directly",
            "repo-local proof materialization now fails closed when either generated or published proof artifact repeats any successor package id",
            "proof citations now stay anchored to repo-local `src`, `tests`, `docs`, and `scripts` paths only",
            "rejects worker-unsafe blocked run-helper citations in proof sources",
        ):
            self.assertIn(token, proof_floor, token)

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
            release = json.loads(read(Path(tmp) / "MEDIA_LOCAL_RELEASE_PROOF.generated.json"))

        package = next(candidate for candidate in release["successor_packages"] if candidate["package_id"] == PACKAGE_ID)
        self.assertEqual(1, sum(1 for candidate in release["successor_packages"] if candidate["package_id"] == PACKAGE_ID))
        self.assertEqual(4037265286, package["frontier_id"])
        self.assertEqual(109, package["milestone_id"])
        self.assertEqual(
            "Render build explainer video, audio, preview-card, and packet siblings from approved explain packets",
            package["title"],
        )
        self.assertEqual(
            "Render approved Build Lab explain packets into video, audio, preview-card, and packet companions without mutating engine truth.",
            package["task"],
        )
        self.assertEqual("109.3", package["work_task_id"])
        self.assertEqual("W9", package["wave"])
        self.assertEqual("chummer6-media-factory", package["repo"])
        self.assertEqual("complete", package["status"])
        self.assertEqual("verify_closed_package_only", package["completion_action"])
        self.assertEqual("7d5a0167", package["proof_floor_commit"])
        self.assertEqual(["src", "tests", "docs", "scripts"], package["allowed_paths"])
        self.assertEqual(
            ["build_explain_companion_rendering", "explain_artifact_receipts"],
            package["owned_surfaces"],
        )

        for token in (
            "request-level rendering id, approved explain packet id, explain packet revision id, and source normalize surrounding whitespace before scope enforcement, dedupe, and receipt emission so valid padded retries keep stable job ids and receipt ids",
            "null artifact lists and null sibling entries fail closed with explicit request-scoped validation before approved explain packet normalization continues",
            "sibling payloads must stay scoped to the approved explain packet id and explain packet revision id before media jobs enqueue, and JSON payloads must match those scope fields exactly instead of passing on substring mentions alone",
            "JSON and keyed text scope values trim surrounding whitespace before exact scope matching so padded approved explain payloads stay valid without reopening substring spoof paths",
            "valid JSON payloads fail closed when required scope fields are missing so object, array, or string note payloads cannot bypass approved explain packet validation through substring fallback",
            "non-JSON scope fallback requires exact keyed values or delimited scope tokens so near-match packet and revision ids cannot slip through by substring collision",
            "caption and preview refs trim surrounding whitespace before grouped receipt rows and receipt hashes emit so padded sibling refs cannot fork stable companion receipt ids or ready refs",
            "case-insensitive duplicate caption and preview refs canonicalize to one stable spelling before grouped receipt rows emit so mixed-case ref variants cannot rewrite aggregate receipt casing when callers reorder the same approved explain packet siblings",
            "build explain companion rendering requires one video, one audio, one preview-card, and one packet companion before the bundle can render",
            "build explain video and audio siblings require caption refs while video, preview-card, and packet companions require preview refs",
            "companion refs trim surrounding whitespace and stay unique case-insensitively per approved explain packet so downstream shelves cannot confuse sibling outputs with padded or mixed-case retries",
            "bundle-scoped dedupe keys include approved explain packet id, explain packet revision id, rendering id, sibling role, category, output format, companion ref, and caller dedupe key",
            "receipt hashes include caption and preview refs so companion receipts stay tied to their emitted explain siblings",
            "receipt hashes use length-prefixed caption and preview ref segments so delimiter-heavy refs cannot collapse distinct companion outputs onto one receipt id",
            "normalized sibling ordering keeps receipt ids, companion refs, ready refs, and grouped role, caption, and preview receipt rows stable when callers reorder the same approved explain packet artifacts",
            "source and requested timestamp metadata stay outside bundle-scoped dedupe and receipt identity so replayed approved explain packet renders cannot fork stable job ids, receipt ids, ready refs, or grouped role receipts",
            "companion ready refs preserve per-artifact ref, receipt id, job id, job state, output format, caption refs, preview refs, asset id, asset url, cache ttl, approval state, retention state, and storage class",
            "role, caption, and preview receipt groups preserve aggregate job ids, grouped companion refs, and grouped artifact rows so downstream shelves do not need to reconstruct explain evidence from raw artifact receipts",
            "rendered timestamps resolve from completed media jobs so later deduped retries cannot rewrite bundle render time with a newer request timestamp",
            "build explain package authority requires exactly one canonical queue row per mirror and exactly one registry task block",
            "generated and published proof artifacts require exactly one M109 successor package entry before drift comparison",
            "queue and registry mirrors must match the canonical M109 package and task blocks exactly so repo-local proof cannot drift on status or scoped fields",
            "proof citations stay anchored to repo-local src, tests, docs, and scripts paths so closed-package evidence cannot drift into sibling surfaces",
            "canonical successor queue rows are now complete with `landed_commit: 7d5a0167`, so future slices can close this package only from the landed proof floor.",
        ):
            self.assertIn(token, package["build_explain_guards"])

        for proof_path in (
            "src/Chummer.Media.Factory.Runtime/Assets/BuildExplainCompanionRenderingService.cs",
            "src/Chummer.Media.Contracts/Compatibility/RunServices/MediaFactoryContracts.cs",
            "src/Chummer.Media.Contracts/README.md",
            "tests/BuildExplainCompanionSmoke/Program.cs",
            "tests/test_m109_successor_package_authority.py",
            "tests/test_build_explain_companion_rendering.py",
            "tests/test_m109_build_explain_proof.py",
            "docs/NEXT90_M109_BUILD_EXPLAIN_COMPANION_PROOF_FLOOR.md",
            "scripts/ai/materialize_media_release_proof.py",
            "scripts/ai/verify.sh",
            "scripts/ai/verify_m109_build_explain_companion.sh",
        ):
            self.assertIn(proof_path, package["proof"])

        for proof_path in package["proof"]:
            self.assertTrue(
                proof_path.startswith(("src/", "tests/", "docs/", "scripts/")),
                f"M109 proof path escaped allowed roots: {proof_path}",
            )

    def test_published_release_proof_and_certification_track_current_m109_entry(self):
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

        self.assertEqual(expected_release, load_package(PUBLISHED_RELEASE_PROOF))
        self.assertEqual(expected_certification, load_package(PUBLISHED_CERTIFICATION))

    def test_m109_proof_sources_stay_worker_safe(self):
        combined = "\n".join(
            (
                read(PROOF_FLOOR),
                read(VERIFY_SCRIPT),
                read(MATERIALIZER),
                read(SERVICE),
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
