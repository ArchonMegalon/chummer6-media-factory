from __future__ import annotations

import hashlib
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path
from typing import Any, Callable


ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "scripts/ai/verify_release_snapshot_compatibility.py"
SPEC = importlib.util.spec_from_file_location("media_release_compatibility", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write_json(path: Path, payload: dict[str, Any]) -> None:
    path.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")


class ReleaseSnapshotCompatibilityTests(unittest.TestCase):
    def make_fixture(
        self,
        root: Path,
        *,
        release_version: str = "run-fixture-20260719",
        snapshot_mutator: Callable[[dict[str, Any]], None] | None = None,
        decision_mutator: Callable[[dict[str, Any]], None] | None = None,
        current_mutator: Callable[[dict[str, Any]], None] | None = None,
        provenance_mutator: Callable[[dict[str, Any]], None] | None = None,
    ) -> dict[str, Path | str]:
        manifest = root / "RELEASE_CHANNEL.json"
        decision = root / "RELEASE_DECISION.json"
        snapshot = root / "SNAPSHOT.json"
        current = root / "CURRENT.json"
        provenance = root / "PROVENANCE.json"
        binding = root / "MEDIA_BINDING.json"
        manifest.write_text('{"channel":"preview","artifacts":[]}\n', encoding="utf-8")
        decision_payload: dict[str, Any] = {
            "contractName": "chummer.preview-release-decision/v1",
            "releaseVersion": release_version,
            "channel": "preview",
            "releaseDecisionStatus": "review_required",
            "status": "review_required",
            "manifestSha256": digest(manifest),
            "registryCommit": "0123456789abcdef0123456789abcdef01234567",
            "platforms": [],
            "primaryHeadByPlatform": {},
            "fallbackHeadsByPlatform": {},
            "supportOwner": "registry-operations",
            "artifactAccessClass": "review_required",
            "authoritySnapshotSha256": "",
            "candidateDecisionStatus": "",
            "candidateDecisionSha256": "",
        }
        if decision_mutator is not None:
            decision_mutator(decision_payload)
        write_json(decision, decision_payload)
        snapshot_payload: dict[str, Any] = {
            "authorityContract": MODULE.AUTHORITY_CONTRACT,
            "releaseVersion": release_version,
            "channel": "preview",
            "status": "blocked",
            "rolloutState": "not_started",
            "supportabilityState": "review_required",
            "availablePlatforms": [],
            "primaryHeadByPlatform": {},
            "artifactCount": 0,
            "downloadAccessPosture": "unavailable",
            "knownIssueSummary": "Public release truth remains review-required.",
            "manifestSha256": digest(manifest),
            "registryRepository": MODULE.REGISTRY_REPOSITORY,
            "registryCommit": "0123456789abcdef0123456789abcdef01234567",
            "releaseDecisionStatus": "review_required",
            "releaseDecisionSha256": digest(decision),
            "releaseDecisionPath": "RELEASE_DECISION.json",
            "supportOwner": "registry-operations",
            "nextActions": ["Converge public release truth."],
            "artifacts": [],
            "manifestPath": "RELEASE_CHANNEL.json",
        }
        if snapshot_mutator is not None:
            snapshot_mutator(snapshot_payload)
        write_json(snapshot, snapshot_payload)
        current_payload: dict[str, Any] = {
            "releaseVersion": release_version,
            "snapshotSha256": digest(snapshot),
            "decisionSha256": digest(decision),
            "status": "review_required",
        }
        if current_mutator is not None:
            current_mutator(current_payload)
        write_json(current, current_payload)
        provenance_payload: dict[str, Any] = {
            "contract": MODULE.PROVENANCE_CONTRACT,
            "releaseVersion": release_version,
            "registryRepository": MODULE.REGISTRY_REPOSITORY,
            "registryCommit": "0123456789abcdef0123456789abcdef01234567",
            "currentSha256": digest(current),
            "authoritySnapshotSha256": digest(snapshot),
            "manifestSha256": digest(manifest),
            "releaseDecisionSha256": digest(decision),
        }
        if provenance_mutator is not None:
            provenance_mutator(provenance_payload)
        write_json(provenance, provenance_payload)
        snapshot_digest = digest(snapshot)
        decision_digest = digest(decision)
        provenance_digest = digest(provenance)
        generation = (
            f"registry://release-evidence/snapshots/{release_version}/{snapshot_digest}"
        )
        write_json(
            binding,
            {
                "authorityContract": MODULE.AUTHORITY_CONTRACT,
                "registryRepository": MODULE.REGISTRY_REPOSITORY,
                "registryCommit": "0123456789abcdef0123456789abcdef01234567",
                "releaseVersion": release_version,
                "currentRef": "registry://release-evidence/CURRENT.json",
                "currentSha256": digest(current),
                "authoritySnapshotRef": f"{generation}/SNAPSHOT.json",
                "authoritySnapshotSha256": snapshot_digest,
                "manifestRef": f"{generation}/RELEASE_CHANNEL.json",
                "manifestSha256": digest(manifest),
                "releaseDecisionRef": f"{generation}/RELEASE_DECISION.json",
                "releaseDecisionSha256": decision_digest,
                "releaseDecisionStatus": "review_required",
                "provenanceRef": (
                    "release-evidence://media-factory/snapshots/"
                    f"{release_version}/{snapshot_digest}/decisions/{decision_digest}/"
                    f"provenance/{provenance_digest}.json"
                ),
                "provenanceSha256": provenance_digest,
            },
        )
        return {
            "current_path": current,
            "expected_current_sha256": digest(current),
            "snapshot_path": snapshot,
            "manifest_path": manifest,
            "decision_path": decision,
            "provenance_path": provenance,
            "binding_path": binding,
        }

    def test_fixture_mode_binds_full_v2_chain_but_is_never_release_evidence(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            fixture = self.make_fixture(Path(temporary_directory))
            result = MODULE.verify(mode="fixture", **fixture)
            self.assertEqual("pass", result["status"])
            self.assertFalse(result["releaseEvidenceEligible"])
            self.assertEqual(MODULE.SCHEMA_SHA256, result["schemaSha256"])

    def test_release_mode_accepts_only_a_full_nonfixture_authority_chain(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            fixture = self.make_fixture(
                Path(temporary_directory), release_version="run-20260719-review"
            )
            result = MODULE.verify(mode="release", **fixture)
            self.assertTrue(result["releaseEvidenceEligible"])
            self.assertEqual("review_required", result["releaseDecisionStatus"])

    def test_release_mode_rejects_fixture_authority(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            fixture = self.make_fixture(Path(temporary_directory))
            with self.assertRaisesRegex(MODULE.CompatibilityError, "fixture authority"):
                MODULE.verify(mode="release", **fixture)

    def test_minimal_self_asserted_snapshot_is_rejected_even_when_digests_align(self) -> None:
        def minimize(snapshot: dict[str, Any]) -> None:
            keep = {
                "authorityContract",
                "registryRepository",
                "registryCommit",
                "releaseVersion",
                "manifestSha256",
                "releaseDecisionSha256",
                "releaseDecisionStatus",
            }
            for key in list(snapshot):
                if key not in keep:
                    del snapshot[key]

        with tempfile.TemporaryDirectory() as temporary_directory:
            fixture = self.make_fixture(
                Path(temporary_directory),
                release_version="run-20260719-review",
                snapshot_mutator=minimize,
            )
            with self.assertRaisesRegex(MODULE.CompatibilityError, "invalid fields"):
                MODULE.verify(mode="release", **fixture)

    def test_snapshot_unknown_field_is_rejected_by_exact_21_field_contract(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            fixture = self.make_fixture(
                Path(temporary_directory),
                snapshot_mutator=lambda snapshot: snapshot.__setitem__(
                    "selfAssertedEvidence", True
                ),
            )
            with self.assertRaisesRegex(MODULE.CompatibilityError, "unexpected"):
                MODULE.verify(mode="fixture", **fixture)

    def test_decision_unknown_field_is_rejected_even_when_snapshot_binds_its_digest(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            fixture = self.make_fixture(
                Path(temporary_directory),
                decision_mutator=lambda decision: decision.__setitem__("claim", "ready"),
            )
            with self.assertRaisesRegex(MODULE.CompatibilityError, "preview release decision"):
                MODULE.verify(mode="fixture", **fixture)

    def test_actual_current_must_select_the_supplied_snapshot(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            fixture = self.make_fixture(
                Path(temporary_directory),
                current_mutator=lambda current: current.__setitem__(
                    "snapshotSha256", "a" * 64
                ),
            )
            with self.assertRaisesRegex(MODULE.CompatibilityError, "does not select"):
                MODULE.verify(mode="fixture", **fixture)

    def test_explicit_current_digest_rejects_mutable_pointer_drift(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            fixture = self.make_fixture(Path(temporary_directory))
            current = fixture["current_path"]
            assert isinstance(current, Path)
            current.write_text(current.read_text(encoding="utf-8") + "\n", encoding="utf-8")
            with self.assertRaisesRegex(MODULE.CompatibilityError, "explicit authority digest"):
                MODULE.verify(mode="fixture", **fixture)

    def test_provenance_must_anchor_current_snapshot_manifest_and_decision(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            fixture = self.make_fixture(
                Path(temporary_directory),
                provenance_mutator=lambda provenance: provenance.__setitem__(
                    "currentSha256", "b" * 64
                ),
            )
            with self.assertRaisesRegex(MODULE.CompatibilityError, "not anchored"):
                MODULE.verify(mode="fixture", **fixture)

    def test_schema_handoff_rejects_any_vendored_schema_drift(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            schema = root / "schema.json"
            schema.write_bytes(MODULE.DEFAULT_SCHEMA.read_bytes() + b"\n")
            fixture = self.make_fixture(root)
            with self.assertRaisesRegex(MODULE.CompatibilityError, "pinned Registry digest"):
                MODULE.verify(mode="fixture", schema_path=schema, **fixture)

    def test_machine_local_binding_ref_fails_closed(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            fixture = self.make_fixture(Path(temporary_directory))
            binding_path = fixture["binding_path"]
            assert isinstance(binding_path, Path)
            binding = json.loads(binding_path.read_text(encoding="utf-8"))
            binding["provenanceRef"] = "file:///docker/private/provider.json"
            write_json(binding_path, binding)
            with self.assertRaisesRegex(MODULE.CompatibilityError, "machine-local path"):
                MODULE.verify(mode="fixture", **fixture)

    def test_case_shadowed_json_property_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            fixture = self.make_fixture(Path(temporary_directory))
            binding_path = fixture["binding_path"]
            assert isinstance(binding_path, Path)
            raw = binding_path.read_text(encoding="utf-8").rstrip()
            binding_path.write_text(
                raw[:-1] + ',"ReleaseVersion":"shadow"}\n', encoding="utf-8"
            )
            with self.assertRaisesRegex(MODULE.CompatibilityError, "ambiguous duplicate"):
                MODULE.verify(mode="fixture", **fixture)


if __name__ == "__main__":
    unittest.main()
