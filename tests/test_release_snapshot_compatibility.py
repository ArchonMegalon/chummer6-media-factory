from __future__ import annotations

import hashlib
import importlib.util
import io
import json
import tempfile
import unittest
from unittest import mock
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


class SourceResponse(io.BytesIO):
    def __init__(self, value: bytes, source: str):
        super().__init__(value)
        self._source = source

    def geturl(self) -> str:
        return self._source

    def __enter__(self):
        return self

    def __exit__(self, *_args):
        self.close()


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
        media_manifest_mutator: Callable[[dict[str, Any]], None] | None = None,
        eligibility_mutator: Callable[[dict[str, Any]], None] | None = None,
        acquisition_fixture: bool = True,
    ) -> dict[str, Path | str]:
        manifest = root / "RELEASE_CHANNEL.json"
        decision = root / "RELEASE_DECISION.json"
        snapshot = root / "SNAPSHOT.json"
        current = root / "CURRENT.json"
        provenance = root / "PROVENANCE.json"
        binding = root / "MEDIA_BINDING.json"
        media_manifest = root / "MEDIA_ASSET_MANIFEST.json"
        eligibility = root / "MEDIA_PUBLIC_ELIGIBILITY.json"
        acquisition = root / "AUTHORITY_ACQUISITION.json"
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
        media_manifest_payload: dict[str, Any] = {
            "assetId": "asset-public-proof",
            "catalogKey": "public/proof",
            "renderJobId": "render-public-proof",
            "renderKind": 2,
            "storageBucket": "public-media",
            "storageObjectKey": "release/asset-public-proof.mp4",
            "contentType": "video/mp4",
            "contentLengthBytes": 42,
            "contentHash": "a" * 64,
            "previewAssetId": None,
            "parentAssetId": None,
            "lifecycle": {
                "approvalStatus": 1,
                "createdAtUtc": "2026-07-18T00:00:00Z",
                "approvedAtUtc": "2026-07-18T00:00:00Z",
                "rejectedAtUtc": None,
                "persistedAtUtc": "2026-07-18T00:00:00Z",
                "expiresAtUtc": None,
                "purgedAtUtc": None,
            },
            "derivedAssetIds": [],
        }
        if media_manifest_mutator is not None:
            media_manifest_mutator(media_manifest_payload)
        write_json(media_manifest, media_manifest_payload)
        canonical_manifest_sha256 = MODULE.canonical_media_manifest_sha256(
            media_manifest_payload
        )
        asset_id = str(media_manifest_payload["assetId"])
        asset_content_sha256 = str(media_manifest_payload["contentHash"])
        provenance_payload: dict[str, Any] = {
            "contract": MODULE.PROVENANCE_CONTRACT,
            "releaseVersion": release_version,
            "registryRepository": MODULE.REGISTRY_REPOSITORY,
            "registryCommit": "0123456789abcdef0123456789abcdef01234567",
            "currentSha256": digest(current),
            "authoritySnapshotSha256": digest(snapshot),
            "manifestSha256": digest(manifest),
            "releaseDecisionSha256": digest(decision),
            "assetId": asset_id,
            "assetContentSha256": asset_content_sha256,
            "canonicalMediaManifestSha256": canonical_manifest_sha256,
        }
        if provenance_mutator is not None:
            provenance_mutator(provenance_payload)
        write_json(provenance, provenance_payload)
        eligibility_payload: dict[str, Any] = {
            "contract": MODULE.ELIGIBILITY_CONTRACT,
            "curatedForPublicRelease": True,
            "curatedBy": "media-release-curator",
            "curatedAtUtc": "2026-07-18T00:00:00Z",
            "authoritySnapshotSha256": digest(snapshot),
            "assetId": asset_id,
            "assetContentSha256": asset_content_sha256,
            "canonicalMediaManifestSha256": canonical_manifest_sha256,
        }
        if eligibility_mutator is not None:
            eligibility_mutator(eligibility_payload)
        write_json(eligibility, eligibility_payload)
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
                    f"assets/{asset_id}/{asset_content_sha256}/"
                    f"manifests/{canonical_manifest_sha256}/"
                    f"provenance/{provenance_digest}.json"
                ),
                "provenanceSha256": provenance_digest,
                "assetId": asset_id,
                "assetContentSha256": asset_content_sha256,
                "canonicalMediaManifestSha256": canonical_manifest_sha256,
            },
        )
        registry_commit = "0123456789abcdef0123456789abcdef01234567"
        write_json(
            acquisition,
            {
                "contract": MODULE.ACQUISITION_CONTRACT,
                "authenticationMode": MODULE.ACQUISITION_MODE,
                "source": (
                    "https://raw.githubusercontent.com/"
                    f"{MODULE.REGISTRY_REPOSITORY}/{registry_commit}/"
                    "release-evidence/CURRENT.json"
                ),
                "registryRepository": MODULE.REGISTRY_REPOSITORY,
                "registryCommit": registry_commit,
                "currentRef": "registry://release-evidence/CURRENT.json",
                "currentSha256": digest(current),
                "fixture": acquisition_fixture,
                "releaseEvidenceEligible": not acquisition_fixture,
            },
        )
        return {
            "current_path": current,
            "acquisition_receipt_path": acquisition,
            "snapshot_path": snapshot,
            "manifest_path": manifest,
            "decision_path": decision,
            "provenance_path": provenance,
            "binding_path": binding,
            "media_manifest_path": media_manifest,
            "eligibility_path": eligibility,
        }

    def verify_release(self, fixture: dict[str, Path | str]) -> dict[str, Any]:
        current_path = fixture["current_path"]
        acquisition_path = fixture["acquisition_receipt_path"]
        assert isinstance(current_path, Path)
        assert isinstance(acquisition_path, Path)
        source = json.loads(acquisition_path.read_text(encoding="utf-8"))["source"]
        with mock.patch.object(
            MODULE.urllib.request,
            "urlopen",
            return_value=SourceResponse(current_path.read_bytes(), source),
        ):
            return MODULE.verify(mode="release", **fixture)

    def assert_public_manifest_rejected(
        self,
        expected_error: str,
        *,
        media_manifest_mutator: Callable[[dict[str, Any]], None] | None = None,
        eligibility_mutator: Callable[[dict[str, Any]], None] | None = None,
    ) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            fixture = self.make_fixture(
                Path(temporary_directory),
                release_version="run-20260719-review",
                media_manifest_mutator=media_manifest_mutator,
                eligibility_mutator=eligibility_mutator,
                acquisition_fixture=False,
            )
            with self.assertRaisesRegex(MODULE.CompatibilityError, expected_error):
                self.verify_release(fixture)

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
                Path(temporary_directory),
                release_version="run-20260719-review",
                acquisition_fixture=False,
            )
            result = self.verify_release(fixture)
            self.assertTrue(result["releaseEvidenceEligible"])
            self.assertEqual("review_required", result["releaseDecisionStatus"])
            self.assertEqual("asset-public-proof", result["assetId"])

    def test_release_mode_accepts_exact_ordered_unexpired_public_lifecycle(self) -> None:
        def make_ordered_lifecycle(manifest: dict[str, Any]) -> None:
            manifest["lifecycle"].update(
                {
                    "createdAtUtc": "2026-07-17T00:00:00Z",
                    "persistedAtUtc": "2026-07-17T01:00:00Z",
                    "approvedAtUtc": "2026-07-17T02:00:00Z",
                    "expiresAtUtc": "2026-07-19T00:00:00Z",
                }
            )

        with tempfile.TemporaryDirectory() as temporary_directory:
            fixture = self.make_fixture(
                Path(temporary_directory),
                release_version="run-20260719-review",
                media_manifest_mutator=make_ordered_lifecycle,
                acquisition_fixture=False,
            )
            result = self.verify_release(fixture)
            self.assertTrue(result["releaseEvidenceEligible"])

    def test_release_mode_rejects_nonapproved_public_media_lifecycle(self) -> None:
        self.assert_public_manifest_rejected(
            "approvalStatus must be Approved",
            media_manifest_mutator=lambda manifest: manifest["lifecycle"].__setitem__(
                "approvalStatus", 2
            ),
        )

    def test_release_mode_rejects_rejected_public_media_lifecycle(self) -> None:
        self.assert_public_manifest_rejected(
            "rejectedAtUtc must be null",
            media_manifest_mutator=lambda manifest: manifest["lifecycle"].__setitem__(
                "rejectedAtUtc", "2026-07-18T00:00:00Z"
            ),
        )

    def test_release_mode_rejects_purged_public_media_lifecycle(self) -> None:
        self.assert_public_manifest_rejected(
            "purgedAtUtc must be null",
            media_manifest_mutator=lambda manifest: manifest["lifecycle"].__setitem__(
                "purgedAtUtc", "2026-07-18T00:00:00Z"
            ),
        )

    def test_release_mode_requires_public_media_approval_timestamp(self) -> None:
        self.assert_public_manifest_rejected(
            "approvedAtUtc is required",
            media_manifest_mutator=lambda manifest: manifest["lifecycle"].__setitem__(
                "approvedAtUtc", None
            ),
        )

    def test_release_mode_requires_public_media_persistence_timestamp(self) -> None:
        self.assert_public_manifest_rejected(
            "persistedAtUtc is required",
            media_manifest_mutator=lambda manifest: manifest["lifecycle"].__setitem__(
                "persistedAtUtc", None
            ),
        )

    def test_release_mode_rejects_out_of_order_public_media_timestamps(self) -> None:
        cases = {
            "approval_before_creation": (
                lambda manifest: manifest["lifecycle"].__setitem__(
                    "approvedAtUtc", "2026-07-17T23:59:59Z"
                ),
                None,
            ),
            "persistence_before_creation": (
                lambda manifest: manifest["lifecycle"].__setitem__(
                    "persistedAtUtc", "2026-07-17T23:59:59Z"
                ),
                None,
            ),
            "curation_before_approval": (
                lambda manifest: manifest["lifecycle"].__setitem__(
                    "approvedAtUtc", "2026-07-19T00:00:00Z"
                ),
                None,
            ),
            "curation_before_persistence": (
                lambda manifest: manifest["lifecycle"].__setitem__(
                    "persistedAtUtc", "2026-07-19T00:00:00Z"
                ),
                None,
            ),
        }
        for label, (manifest_mutator, eligibility_mutator) in cases.items():
            with self.subTest(label=label):
                self.assert_public_manifest_rejected(
                    "timestamps are out of order at curation",
                    media_manifest_mutator=manifest_mutator,
                    eligibility_mutator=eligibility_mutator,
                )

    def test_release_mode_rejects_public_media_expired_at_curation(self) -> None:
        self.assert_public_manifest_rejected(
            "expired at curation",
            media_manifest_mutator=lambda manifest: manifest["lifecycle"].__setitem__(
                "expiresAtUtc", "2026-07-18T00:00:00Z"
            ),
        )

    def test_release_mode_preserves_seventh_tick_in_lifecycle_ordering(self) -> None:
        self.assert_public_manifest_rejected(
            "timestamps are out of order at curation",
            media_manifest_mutator=lambda manifest: manifest["lifecycle"].__setitem__(
                "approvedAtUtc", "2026-07-18T00:00:00.0000002Z"
            ),
            eligibility_mutator=lambda eligibility: eligibility.__setitem__(
                "curatedAtUtc", "2026-07-18T00:00:00.0000001Z"
            ),
        )

    def test_lifecycle_timestamp_grammar_rejects_unicode_digit_aliases(self) -> None:
        with self.assertRaisesRegex(MODULE.CompatibilityError, "UTC timestamp"):
            MODULE.canonical_timestamp(
                "٢٠٢٦-٠٧-١٨T٠٠:٠٠:٠٠Z",
                "createdAtUtc",
            )

    def test_lifecycle_timestamp_preserves_nondefault_first_dotnet_tick(self) -> None:
        self.assertEqual(
            "0001-01-01T00:00:00.0000001Z",
            MODULE.canonical_timestamp(
                "0001-01-01T00:00:00.0000001Z",
                "createdAtUtc",
            ),
        )
        with self.assertRaisesRegex(MODULE.CompatibilityError, "UTC timestamp"):
            MODULE.canonical_timestamp(
                "0001-01-01T00:00:00.0000000Z",
                "createdAtUtc",
            )

    def test_release_mode_rejects_noncanonical_bound_asset_identifier(self) -> None:
        self.assert_public_manifest_rejected(
            "assetId is invalid",
            media_manifest_mutator=lambda manifest: manifest.__setitem__(
                "assetId", "asset/public"
            ),
        )

    def test_release_mode_rejects_content_length_outside_dotnet_int64(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            fixture = self.make_fixture(
                Path(temporary_directory),
                release_version="run-20260719-review",
                acquisition_fixture=False,
            )
            media_manifest_path = fixture["media_manifest_path"]
            assert isinstance(media_manifest_path, Path)
            media_manifest = json.loads(media_manifest_path.read_text(encoding="utf-8"))
            media_manifest["contentLengthBytes"] = 9223372036854775808
            write_json(media_manifest_path, media_manifest)
            with self.assertRaisesRegex(
                MODULE.CompatibilityError,
                "between 1 and 9223372036854775807",
            ):
                self.verify_release(fixture)

    def test_release_mode_requires_boolean_public_curation_flag(self) -> None:
        self.assert_public_manifest_rejected(
            "must be boolean true",
            eligibility_mutator=lambda eligibility: eligibility.__setitem__(
                "curatedForPublicRelease", 1
            ),
        )

    def test_release_mode_rejects_self_derived_public_media_lineage(self) -> None:
        self.assert_public_manifest_rejected(
            "cannot derive from itself",
            media_manifest_mutator=lambda manifest: manifest.__setitem__(
                "derivedAssetIds", [manifest["assetId"]]
            ),
        )

    def test_release_mode_rejects_self_parented_public_media_lineage(self) -> None:
        self.assert_public_manifest_rejected(
            "cannot name itself as parent",
            media_manifest_mutator=lambda manifest: manifest.__setitem__(
                "parentAssetId", manifest["assetId"]
            ),
        )

    def test_release_mode_rejects_unsafe_public_media_storage_key(self) -> None:
        self.assert_public_manifest_rejected(
            "safe relative object key",
            media_manifest_mutator=lambda manifest: manifest.__setitem__(
                "storageObjectKey", "release/../private/asset.mp4"
            ),
        )

    def test_release_mode_rejects_invalid_public_media_content_type(self) -> None:
        self.assert_public_manifest_rejected(
            "canonical type/subtype pair",
            media_manifest_mutator=lambda manifest: manifest.__setitem__(
                "contentType", "video/mp4/extra"
            ),
        )

    def test_release_mode_rejects_fixture_authority(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            fixture = self.make_fixture(Path(temporary_directory))
            with self.assertRaisesRegex(MODULE.CompatibilityError, "nonfixture authority acquisition"):
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
                acquisition_fixture=False,
            )
            current_path = fixture["current_path"]
            acquisition_path = fixture["acquisition_receipt_path"]
            assert isinstance(current_path, Path)
            assert isinstance(acquisition_path, Path)
            source = json.loads(acquisition_path.read_text(encoding="utf-8"))["source"]
            with mock.patch.object(
                MODULE.urllib.request,
                "urlopen",
                return_value=SourceResponse(current_path.read_bytes(), source),
            ):
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
            with self.assertRaisesRegex(MODULE.CompatibilityError, "authenticated acquisition"):
                MODULE.verify(mode="fixture", **fixture)

    def test_acquisition_receipt_cannot_be_rehashed_inside_the_authority_chain(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            fixture = self.make_fixture(Path(temporary_directory))
            receipt_path = fixture["acquisition_receipt_path"]
            assert isinstance(receipt_path, Path)
            receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
            receipt["currentSha256"] = "f" * 64
            write_json(receipt_path, receipt)
            with self.assertRaisesRegex(MODULE.CompatibilityError, "authenticated acquisition"):
                MODULE.verify(mode="fixture", **fixture)

    def test_release_mode_rejects_a_self_hashed_chain_absent_registry_source_bytes(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            fixture = self.make_fixture(
                Path(temporary_directory),
                release_version="run-20260719-review",
                acquisition_fixture=False,
            )
            acquisition_path = fixture["acquisition_receipt_path"]
            assert isinstance(acquisition_path, Path)
            source = json.loads(acquisition_path.read_text(encoding="utf-8"))["source"]
            with mock.patch.object(
                MODULE.urllib.request,
                "urlopen",
                return_value=SourceResponse(b'{"different":"registry bytes"}\n', source),
            ):
                with self.assertRaisesRegex(MODULE.CompatibilityError, "Registry source bytes"):
                    MODULE.verify(mode="release", **fixture)

    def test_acquisition_commit_must_equal_the_snapshot_issuer_commit(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            fixture = self.make_fixture(
                Path(temporary_directory),
                release_version="run-20260719-review",
                acquisition_fixture=False,
            )
            acquisition_path = fixture["acquisition_receipt_path"]
            current_path = fixture["current_path"]
            assert isinstance(acquisition_path, Path)
            assert isinstance(current_path, Path)
            acquisition = json.loads(acquisition_path.read_text(encoding="utf-8"))
            acquisition["registryCommit"] = "f" * 40
            acquisition["source"] = (
                "https://raw.githubusercontent.com/"
                f"{MODULE.REGISTRY_REPOSITORY}/{'f' * 40}/release-evidence/CURRENT.json"
            )
            write_json(acquisition_path, acquisition)
            with mock.patch.object(
                MODULE.urllib.request,
                "urlopen",
                return_value=SourceResponse(current_path.read_bytes(), acquisition["source"]),
            ):
                with self.assertRaisesRegex(MODULE.CompatibilityError, "issuer commit"):
                    MODULE.verify(mode="release", **fixture)

    def test_asset_substitution_fails_the_shared_manifest_binding(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            fixture = self.make_fixture(Path(temporary_directory))
            media_manifest_path = fixture["media_manifest_path"]
            assert isinstance(media_manifest_path, Path)
            media_manifest = json.loads(media_manifest_path.read_text(encoding="utf-8"))
            media_manifest["assetId"] = "asset-substitution"
            write_json(media_manifest_path, media_manifest)
            with self.assertRaisesRegex(MODULE.CompatibilityError, "exact asset"):
                MODULE.verify(mode="fixture", **fixture)

    def test_canonical_media_manifest_digest_matches_the_csharp_vector(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            fixture = self.make_fixture(Path(temporary_directory))
            media_manifest_path = fixture["media_manifest_path"]
            assert isinstance(media_manifest_path, Path)
            media_manifest = json.loads(media_manifest_path.read_text(encoding="utf-8"))
            self.assertEqual(
                "6b6a51b3b345d7a3734697ce66499e7ebc3001fb19f57d847347c10c6b2e0671",
                MODULE.canonical_media_manifest_sha256(media_manifest),
            )
            media_manifest["lifecycle"]["approvedAtUtc"] = (
                "2026-07-18T00:00:00.0000001Z"
            )
            self.assertEqual(
                "8645e54e544bf72f553f21b3945e9d42fe795312a9bdbd326914ec07d3b73b47",
                MODULE.canonical_media_manifest_sha256(media_manifest),
            )
            media_manifest["lifecycle"]["approvedAtUtc"] = "2026-07-18T00:00:00Z"
            media_manifest["derivedAssetIds"] = ["\ue000", "\U00010000"]
            self.assertEqual(
                "adde2158cc4667b0675a984d7fb44a7eceb3dca864881098cf3e76e753e582d8",
                MODULE.canonical_media_manifest_sha256(media_manifest),
            )

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
