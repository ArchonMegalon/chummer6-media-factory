from __future__ import annotations

import hashlib
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "scripts/ai/verify_release_snapshot_compatibility.py"
SPEC = importlib.util.spec_from_file_location("media_release_compatibility", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


class ReleaseSnapshotCompatibilityTests(unittest.TestCase):
    def make_fixture(self, root: Path) -> dict[str, Path | str]:
        manifest = root / "RELEASE_CHANNEL.json"
        decision = root / "RELEASE_DECISION.json"
        provenance = root / "PROVENANCE.json"
        snapshot = root / "SNAPSHOT.json"
        binding = root / "MEDIA_BINDING.json"
        manifest.write_text('{"channel":"preview"}\n', encoding="utf-8")
        decision.write_text(
            json.dumps(
                {
                    "releaseVersion": "run-fixture-20260718",
                    "releaseDecisionStatus": "review_required",
                },
                sort_keys=True,
            )
            + "\n",
            encoding="utf-8",
        )
        provenance.write_text('{"providerEvidence":"private"}\n', encoding="utf-8")
        snapshot.write_text(
            json.dumps(
                {
                    "authorityContract": MODULE.AUTHORITY_CONTRACT,
                    "registryRepository": MODULE.REGISTRY_REPOSITORY,
                    "registryCommit": "0123456789abcdef0123456789abcdef01234567",
                    "releaseVersion": "run-fixture-20260718",
                    "manifestSha256": digest(manifest),
                    "releaseDecisionSha256": digest(decision),
                    "releaseDecisionStatus": "review_required",
                },
                sort_keys=True,
            )
            + "\n",
            encoding="utf-8",
        )
        binding.write_text(
            json.dumps(
                {
                    "authorityContract": MODULE.AUTHORITY_CONTRACT,
                    "registryRepository": MODULE.REGISTRY_REPOSITORY,
                    "registryCommit": "0123456789abcdef0123456789abcdef01234567",
                    "releaseVersion": "run-fixture-20260718",
                    "authoritySnapshotRef": "registry://release-evidence/run-fixture-20260718/SNAPSHOT.json",
                    "authoritySnapshotSha256": digest(snapshot),
                    "manifestRef": "registry://release-evidence/run-fixture-20260718/RELEASE_CHANNEL.json",
                    "manifestSha256": digest(manifest),
                    "releaseDecisionRef": "registry://release-evidence/run-fixture-20260718/RELEASE_DECISION.json",
                    "releaseDecisionSha256": digest(decision),
                    "releaseDecisionStatus": "review_required",
                    "provenanceRef": "release-evidence://release-evidence/run-fixture-20260718/provenance/media.json",
                    "provenanceSha256": digest(provenance),
                },
                sort_keys=True,
            )
            + "\n",
            encoding="utf-8",
        )
        return {
            "snapshot_path": snapshot,
            "expected_snapshot_sha256": digest(snapshot),
            "manifest_path": manifest,
            "decision_path": decision,
            "provenance_path": provenance,
            "binding_path": binding,
        }

    def test_fixture_mode_binds_all_exact_bytes_but_is_not_release_evidence(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            fixture = self.make_fixture(Path(temporary_directory))
            result = MODULE.verify(mode="fixture", **fixture)
            self.assertEqual("pass", result["status"])
            self.assertFalse(result["releaseEvidenceEligible"])

    def test_manifest_drift_fails_closed(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            fixture = self.make_fixture(Path(temporary_directory))
            manifest = fixture["manifest_path"]
            assert isinstance(manifest, Path)
            manifest.write_text('{"channel":"tampered"}\n', encoding="utf-8")
            with self.assertRaisesRegex(MODULE.CompatibilityError, "manifest bytes diverge"):
                MODULE.verify(mode="fixture", **fixture)

    def test_machine_local_binding_ref_fails_closed(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            fixture = self.make_fixture(Path(temporary_directory))
            binding_path = fixture["binding_path"]
            assert isinstance(binding_path, Path)
            binding = json.loads(binding_path.read_text(encoding="utf-8"))
            binding["provenanceRef"] = "file:///docker/private/provider.json"
            binding_path.write_text(json.dumps(binding) + "\n", encoding="utf-8")
            with self.assertRaisesRegex(MODULE.CompatibilityError, "machine-local path"):
                MODULE.verify(mode="fixture", **fixture)

    def test_release_mode_rejects_fixture_authority(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            fixture = self.make_fixture(Path(temporary_directory))
            with self.assertRaisesRegex(MODULE.CompatibilityError, "fixture authority"):
                MODULE.verify(mode="release", **fixture)


if __name__ == "__main__":
    unittest.main()
