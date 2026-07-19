#!/usr/bin/env python3
"""Verify media publication against one exact Registry v2 authority generation."""

from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import re
import sys
import unicodedata
import urllib.request
from pathlib import Path
from typing import Any
from urllib.parse import unquote, urlsplit


ROOT = Path(__file__).resolve().parents[2]
DEFAULT_SCHEMA = ROOT / "eng/contracts/release-authority-v2.schema.json"
DEFAULT_SCHEMA_LOCK = ROOT / "eng/release-authority-schema.lock.json"
AUTHORITY_CONTRACT = "chummer.release-authority-snapshot/v2"
REGISTRY_REPOSITORY = "ArchonMegalon/chummer6-hub-registry"
SCHEMA_CONTRACT = "chummer.media.external-release-authority-schema-lock/v1"
SCHEMA_COMMIT = "4a312798a10cb7ae97c77731450e24fe6a74d963"
SCHEMA_PATH = "contracts/release-authority-v2.schema.json"
SCHEMA_SHA256 = "cbdad3c9ce8e9e0c0e37374771f99aad8957cd51dd4d2447425aea2d00ba5fb0"
PROVENANCE_CONTRACT = "chummer.media.release-provenance/v2"
ACQUISITION_CONTRACT = "chummer.media.registry-authority-acquisition/v1"
ACQUISITION_MODE = "authenticated-immutable-source-replay"
MEDIA_MANIFEST_DIGEST_CONTRACT = "chummer.media.asset-manifest-digest/v1"
ELIGIBILITY_CONTRACT = "chummer.media.public-eligibility/v2"
SHA40 = re.compile(r"^[0-9a-f]{40}$")
SHA256 = re.compile(r"^[0-9a-f]{64}$")
UTC_TIMESTAMP = re.compile(
    r"^([0-9]{4})-([0-9]{2})-([0-9]{2})T([0-9]{2}):([0-9]{2}):([0-9]{2})(?:\.([0-9]{1,7}))?Z$"
)
RELEASE_VERSION = re.compile(r"^[A-Za-z0-9._+-]{1,128}$")
NORMALIZED_TOKEN = re.compile(r"^[a-z0-9][a-z0-9._+-]*$")
NORMALIZED_IDENTIFIER = re.compile(
    r"^(?!unknown$|missing$|invalid$)[a-z0-9][a-z0-9._+-]*$"
)
DECISION_STATUSES = {"review_required", "preview_ready", "stable_ready"}
INSTALL_ACCESS_CLASSES = {"open_public", "account_recommended", "account_required"}
DOWNLOAD_ACCESS_POSTURES = INSTALL_ACCESS_CLASSES | {"unavailable", "mixed"}
INT64_MAX = (1 << 63) - 1
SCHEMA_LOCK_KEYS = {"contract", "repository", "commit", "path", "sha256"}
CURRENT_KEYS = {"releaseVersion", "snapshotSha256", "decisionSha256", "status"}
BINDING_KEYS = {
    "authorityContract",
    "registryRepository",
    "registryCommit",
    "releaseVersion",
    "currentRef",
    "currentSha256",
    "authoritySnapshotRef",
    "authoritySnapshotSha256",
    "manifestRef",
    "manifestSha256",
    "releaseDecisionRef",
    "releaseDecisionSha256",
    "releaseDecisionStatus",
    "provenanceRef",
    "provenanceSha256",
    "assetId",
    "assetContentSha256",
    "canonicalMediaManifestSha256",
}
PROVENANCE_KEYS = {
    "contract",
    "releaseVersion",
    "registryRepository",
    "registryCommit",
    "currentSha256",
    "authoritySnapshotSha256",
    "manifestSha256",
    "releaseDecisionSha256",
    "assetId",
    "assetContentSha256",
    "canonicalMediaManifestSha256",
}
ACQUISITION_KEYS = {
    "contract",
    "authenticationMode",
    "source",
    "registryRepository",
    "registryCommit",
    "currentRef",
    "currentSha256",
    "fixture",
    "releaseEvidenceEligible",
}
MEDIA_MANIFEST_KEYS = {
    "assetId",
    "catalogKey",
    "renderJobId",
    "renderKind",
    "storageBucket",
    "storageObjectKey",
    "contentType",
    "contentLengthBytes",
    "contentHash",
    "previewAssetId",
    "parentAssetId",
    "lifecycle",
    "derivedAssetIds",
}
MEDIA_LIFECYCLE_KEYS = {
    "approvalStatus",
    "createdAtUtc",
    "approvedAtUtc",
    "rejectedAtUtc",
    "persistedAtUtc",
    "expiresAtUtc",
    "purgedAtUtc",
}
ELIGIBILITY_KEYS = {
    "contract",
    "curatedForPublicRelease",
    "curatedBy",
    "curatedAtUtc",
    "authoritySnapshotSha256",
    "assetId",
    "assetContentSha256",
    "canonicalMediaManifestSha256",
}
PREVIEW_DECISION_KEYS = {
    "contractName",
    "releaseVersion",
    "channel",
    "releaseDecisionStatus",
    "status",
    "manifestSha256",
    "registryCommit",
    "platforms",
    "primaryHeadByPlatform",
    "fallbackHeadsByPlatform",
    "supportOwner",
    "artifactAccessClass",
    "authoritySnapshotSha256",
    "candidateDecisionStatus",
    "candidateDecisionSha256",
}
STABLE_DECISION_KEYS = {
    "contract_name",
    "contract_version",
    "releaseVersion",
    "releaseDecisionStatus",
    "status",
    "live_release",
    "release_authority",
}
STABLE_LIVE_KEYS = {
    "version",
    "channel",
    "manifest_sha256",
    "registry_commit",
    "available_platforms",
    "primary_head_by_platform",
    "status",
    "rollout_state",
    "supportability_state",
    "artifact_count",
    "download_access_posture",
    "known_issue_summary",
    "release_decision_status",
}
STABLE_AUTHORITY_KEYS = {
    "contract",
    "manifest_sha256",
    "registry_commit",
    "release_decision_status",
}
FORBIDDEN_LOCAL_FRAGMENTS = (
    "/tmp/",
    "/var/tmp/",
    "/docker/",
    "/workspace/",
    "/home/",
    "file://",
)


class CompatibilityError(RuntimeError):
    """Raised when release authority and media publication bytes diverge."""


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    try:
        with path.open("rb") as stream:
            for chunk in iter(lambda: stream.read(1024 * 1024), b""):
                digest.update(chunk)
    except OSError as exc:
        raise CompatibilityError(f"unable to read {path}: {exc}") from exc
    return digest.hexdigest()


def _strict_object(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    folded: set[str] = set()
    for key, value in pairs:
        folded_key = key.casefold()
        if key in result or folded_key in folded:
            raise CompatibilityError(f"ambiguous duplicate JSON property: {key}")
        result[key] = value
        folded.add(folded_key)
    return result


def load_json(path: Path, label: str) -> dict[str, Any]:
    try:
        payload = json.loads(
            path.read_text(encoding="utf-8"), object_pairs_hook=_strict_object
        )
    except CompatibilityError:
        raise
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise CompatibilityError(f"unable to load {label}: {exc}") from exc
    if not isinstance(payload, dict):
        raise CompatibilityError(f"{label} must be a JSON object")
    return payload


def require_exact_keys(payload: dict[str, Any], expected: set[str], label: str) -> None:
    if set(payload) != expected:
        missing = sorted(expected - set(payload))
        unexpected = sorted(set(payload) - expected)
        raise CompatibilityError(
            f"{label} has invalid fields (missing={missing}, unexpected={unexpected})"
        )


def require_sha(value: Any, label: str, pattern: re.Pattern[str] = SHA256) -> str:
    if not isinstance(value, str) or pattern.fullmatch(value) is None:
        raise CompatibilityError(f"{label} must be an exact lowercase digest")
    return value


def require_text(value: Any, label: str) -> str:
    if (
        not isinstance(value, str)
        or not value
        or value != value.strip()
        or any(ord(character) < 32 or ord(character) == 127 for character in value)
    ):
        raise CompatibilityError(f"{label} must be canonical non-empty text")
    return value


def require_token(value: Any, label: str) -> str:
    value = require_text(value, label)
    if NORMALIZED_TOKEN.fullmatch(value) is None:
        raise CompatibilityError(f"{label} must be a normalized token")
    return value


def require_identifier(value: Any, label: str) -> str:
    value = require_text(value, label)
    if NORMALIZED_IDENTIFIER.fullmatch(value) is None:
        raise CompatibilityError(f"{label} must be a normalized identifier")
    return value


def require_release_version(value: Any, label: str = "releaseVersion") -> str:
    if (
        not isinstance(value, str)
        or RELEASE_VERSION.fullmatch(value) is None
        or value in {".", ".."}
    ):
        raise CompatibilityError(f"{label} is invalid")
    return value


def require_integer(
    value: Any, label: str, minimum: int, maximum: int | None = None
) -> int:
    if (
        not isinstance(value, int)
        or isinstance(value, bool)
        or value < minimum
        or maximum is not None
        and value > maximum
    ):
        range_label = (
            f"between {minimum} and {maximum}"
            if maximum is not None
            else f">= {minimum}"
        )
        raise CompatibilityError(f"{label} must be an integer {range_label}")
    return value


def require_nullable_text(value: Any, label: str) -> str | None:
    if value is None:
        return None
    return require_text(value, label)


def parse_utc_timestamp(value: Any, label: str) -> tuple[dt.datetime, int]:
    value = require_text(value, label)
    match = UTC_TIMESTAMP.fullmatch(value)
    if match is None:
        raise CompatibilityError(f"{label} must be a UTC timestamp")
    year, month, day, hour, minute, second = (
        int(component) for component in match.groups()[:6]
    )
    fraction = (match.group(7) or "").ljust(7, "0")
    try:
        parsed = dt.datetime(
            year,
            month,
            day,
            hour,
            minute,
            second,
            microsecond=int(fraction[:6]),
            tzinfo=dt.timezone.utc,
        )
    except ValueError as exc:
        raise CompatibilityError(f"{label} must be a UTC timestamp") from exc
    final_tick = int(fraction[6])
    if parsed == dt.datetime.min.replace(tzinfo=dt.timezone.utc) and final_tick == 0:
        raise CompatibilityError(f"{label} must be a UTC timestamp")
    return parsed, final_tick


def canonical_timestamp(value: Any, label: str) -> str:
    parsed, final_tick = parse_utc_timestamp(value, label)
    return (
        f"{parsed.year:04d}-{parsed.month:02d}-{parsed.day:02d}"
        f"T{parsed.hour:02d}:{parsed.minute:02d}:{parsed.second:02d}."
        f"{parsed.microsecond:06d}{final_tick}Z"
    )


def canonical_nullable_timestamp(value: Any, label: str) -> str | None:
    return None if value is None else canonical_timestamp(value, label)


def _append_manifest_segment(digest: Any, value: str | None) -> None:
    if value is None:
        digest.update(b"\x00")
        return
    encoded = value.encode("utf-8")
    digest.update(b"\x01")
    digest.update(len(encoded).to_bytes(4, "big", signed=False))
    digest.update(encoded)


def utf16_ordinal_key(value: str) -> bytes:
    """Return the byte-sort key matching .NET StringComparer.Ordinal code units."""
    return value.encode("utf-16-be")


def canonical_media_manifest_sha256(payload: dict[str, Any]) -> str:
    """Compute the exact cross-language media-manifest digest contract."""
    require_exact_keys(payload, MEDIA_MANIFEST_KEYS, "media asset manifest")
    lifecycle = payload["lifecycle"]
    if not isinstance(lifecycle, dict):
        raise CompatibilityError("media asset lifecycle must be an object")
    require_exact_keys(lifecycle, MEDIA_LIFECYCLE_KEYS, "media asset lifecycle")
    render_kind = require_integer(payload["renderKind"], "renderKind", 0)
    approval_status = require_integer(
        lifecycle["approvalStatus"], "approvalStatus", 0
    )
    if render_kind not in {0, 1, 2} or approval_status not in {0, 1, 2}:
        raise CompatibilityError("media manifest enum value is invalid")
    content_length = require_integer(
        payload["contentLengthBytes"], "contentLengthBytes", 1, INT64_MAX
    )
    content_hash = require_sha(payload["contentHash"], "contentHash")
    derived = payload["derivedAssetIds"]
    if not isinstance(derived, list):
        raise CompatibilityError("derivedAssetIds must be an array")
    derived_ids = [require_text(item, "derivedAssetIds item") for item in derived]
    if len(derived_ids) != len(set(derived_ids)):
        raise CompatibilityError("derivedAssetIds must be unique")
    values: list[str | None] = [
        MEDIA_MANIFEST_DIGEST_CONTRACT,
        require_text(payload["assetId"], "assetId"),
        require_text(payload["catalogKey"], "catalogKey"),
        require_text(payload["renderJobId"], "renderJobId"),
        str(render_kind),
        require_text(payload["storageBucket"], "storageBucket"),
        require_text(payload["storageObjectKey"], "storageObjectKey"),
        require_text(payload["contentType"], "contentType"),
        str(content_length),
        content_hash,
        require_nullable_text(payload["previewAssetId"], "previewAssetId"),
        require_nullable_text(payload["parentAssetId"], "parentAssetId"),
        str(approval_status),
        canonical_timestamp(lifecycle["createdAtUtc"], "createdAtUtc"),
        canonical_nullable_timestamp(lifecycle["approvedAtUtc"], "approvedAtUtc"),
        canonical_nullable_timestamp(lifecycle["rejectedAtUtc"], "rejectedAtUtc"),
        canonical_nullable_timestamp(lifecycle["persistedAtUtc"], "persistedAtUtc"),
        canonical_nullable_timestamp(lifecycle["expiresAtUtc"], "expiresAtUtc"),
        canonical_nullable_timestamp(lifecycle["purgedAtUtc"], "purgedAtUtc"),
        str(len(derived_ids)),
        *sorted(derived_ids, key=utf16_ordinal_key),
    ]
    digest = hashlib.sha256()
    for value in values:
        _append_manifest_segment(digest, value)
    return digest.hexdigest()


def validate_eligibility(
    eligibility: dict[str, Any],
    *,
    snapshot_sha256: str,
    asset_id: str,
    content_sha256: str,
    canonical_manifest_sha256: str,
) -> None:
    require_exact_keys(eligibility, ELIGIBILITY_KEYS, "media public eligibility")
    if eligibility["curatedForPublicRelease"] is not True:
        raise CompatibilityError(
            "media public eligibility curatedForPublicRelease must be boolean true"
        )
    expected = {
        "contract": ELIGIBILITY_CONTRACT,
        "authoritySnapshotSha256": snapshot_sha256,
        "assetId": asset_id,
        "assetContentSha256": content_sha256,
        "canonicalMediaManifestSha256": canonical_manifest_sha256,
    }
    for key, value in expected.items():
        if eligibility.get(key) != value:
            raise CompatibilityError(
                f"media public eligibility {key} diverges from the exact asset"
            )
    require_text(eligibility["curatedBy"], "media public eligibility curatedBy")
    canonical_timestamp(
        eligibility["curatedAtUtc"], "media public eligibility curatedAtUtc"
    )


def require_storage_object_key(value: Any) -> str:
    value = require_text(value, "storageObjectKey")
    segments = value.split("/")
    if (
        value.startswith("/")
        or "\\" in value
        or any(unicodedata.category(character) == "Cc" for character in value)
        or any(segment in {"", ".", ".."} for segment in segments)
    ):
        raise CompatibilityError("storageObjectKey must be a safe relative object key")
    return value


def require_content_type(value: Any) -> str:
    value = require_text(value, "contentType")
    separator = value.find("/")
    if (
        separator <= 0
        or separator == len(value) - 1
        or separator != value.rfind("/")
        or any(character.isspace() for character in value)
    ):
        raise CompatibilityError("contentType must contain one canonical type/subtype pair")
    return value


def validate_public_media_manifest(
    manifest: dict[str, Any], eligibility: dict[str, Any]
) -> None:
    """Match PublicMediaAssetProjection's public lifecycle and storage gate."""
    require_exact_keys(manifest, MEDIA_MANIFEST_KEYS, "media asset manifest")
    asset_id = require_release_version(manifest["assetId"], "assetId")
    require_text(manifest["catalogKey"], "catalogKey")
    require_text(manifest["renderJobId"], "renderJobId")
    render_kind = require_integer(manifest["renderKind"], "renderKind", 0)
    if render_kind not in {0, 1, 2}:
        raise CompatibilityError("media manifest renderKind is invalid")
    require_text(manifest["storageBucket"], "storageBucket")
    require_storage_object_key(manifest["storageObjectKey"])
    require_content_type(manifest["contentType"])
    require_integer(
        manifest["contentLengthBytes"], "contentLengthBytes", 1, INT64_MAX
    )
    require_sha(manifest["contentHash"], "contentHash")

    lifecycle = manifest["lifecycle"]
    if not isinstance(lifecycle, dict):
        raise CompatibilityError("media asset lifecycle must be an object")
    require_exact_keys(lifecycle, MEDIA_LIFECYCLE_KEYS, "media asset lifecycle")
    created_at = parse_utc_timestamp(lifecycle["createdAtUtc"], "createdAtUtc")
    if lifecycle["approvalStatus"] != 1:
        raise CompatibilityError(
            "public media lifecycle approvalStatus must be Approved"
        )
    if lifecycle["approvedAtUtc"] is None:
        raise CompatibilityError("public media lifecycle approvedAtUtc is required")
    if lifecycle["persistedAtUtc"] is None:
        raise CompatibilityError("public media lifecycle persistedAtUtc is required")
    if lifecycle["rejectedAtUtc"] is not None:
        raise CompatibilityError("public media lifecycle rejectedAtUtc must be null")
    if lifecycle["purgedAtUtc"] is not None:
        raise CompatibilityError("public media lifecycle purgedAtUtc must be null")

    approved_at = parse_utc_timestamp(lifecycle["approvedAtUtc"], "approvedAtUtc")
    persisted_at = parse_utc_timestamp(lifecycle["persistedAtUtc"], "persistedAtUtc")
    curated_at = parse_utc_timestamp(
        eligibility["curatedAtUtc"], "media public eligibility curatedAtUtc"
    )
    expires_at = (
        None
        if lifecycle["expiresAtUtc"] is None
        else parse_utc_timestamp(lifecycle["expiresAtUtc"], "expiresAtUtc")
    )
    if (
        approved_at < created_at
        or persisted_at < created_at
        or curated_at < approved_at
        or curated_at < persisted_at
    ):
        raise CompatibilityError(
            "public media lifecycle timestamps are out of order at curation"
        )
    if expires_at is not None and expires_at <= curated_at:
        raise CompatibilityError("public media lifecycle is expired at curation")

    derived = manifest["derivedAssetIds"]
    if not isinstance(derived, list):
        raise CompatibilityError("derivedAssetIds must be an array")
    derived_ids = [require_text(item, "derivedAssetIds item") for item in derived]
    if len(derived_ids) != len(set(derived_ids)):
        raise CompatibilityError("derivedAssetIds must be unique")
    if asset_id in derived_ids:
        raise CompatibilityError("media asset lineage cannot derive from itself")
    require_nullable_text(manifest["previewAssetId"], "previewAssetId")
    parent_asset_id = require_nullable_text(manifest["parentAssetId"], "parentAssetId")
    if parent_asset_id == asset_id:
        raise CompatibilityError("media asset lineage cannot name itself as parent")


def require_identifier_list(value: Any, label: str, *, sorted_values: bool) -> list[str]:
    if not isinstance(value, list):
        raise CompatibilityError(f"{label} must be an array")
    result = [require_identifier(item, f"{label} item") for item in value]
    if len(result) != len(set(result)):
        raise CompatibilityError(f"{label} must contain unique identifiers")
    if sorted_values and result != sorted(result):
        raise CompatibilityError(f"{label} must use canonical ordinal ordering")
    return result


def require_primary_heads(value: Any, label: str) -> dict[str, str]:
    if not isinstance(value, dict):
        raise CompatibilityError(f"{label} must be an object")
    result: dict[str, str] = {}
    for platform, head in value.items():
        result[require_identifier(platform, f"{label} platform")] = require_identifier(
            head, f"{label} head"
        )
    if list(result) != sorted(result):
        raise CompatibilityError(f"{label} keys must use canonical ordinal ordering")
    return result


def require_ref(value: Any, label: str) -> str:
    value = require_text(value, label)
    lowered = value.lower()
    if any(fragment in lowered for fragment in FORBIDDEN_LOCAL_FRAGMENTS):
        raise CompatibilityError(f"{label} contains a machine-local path")
    if "\\" in value or any(character.isspace() for character in value):
        raise CompatibilityError(f"{label} is not portable")
    if any(encoded in lowered for encoded in ("%2e", "%2f", "%5c")):
        raise CompatibilityError(f"{label} contains encoded traversal")
    parsed = urlsplit(value)
    if parsed.scheme not in {"https", "registry", "release-evidence", "urn"}:
        raise CompatibilityError(f"{label} uses a non-authority URI scheme")
    if parsed.scheme != "urn" and not parsed.netloc:
        raise CompatibilityError(f"{label} must name an authority host")
    if parsed.username or parsed.password or parsed.query or parsed.fragment:
        raise CompatibilityError(f"{label} contains mutable or credentialed URI parts")
    if any(segment in {".", ".."} for segment in unquote(parsed.path).split("/")):
        raise CompatibilityError(f"{label} contains traversal")
    return value


def verify_schema_handoff(schema_path: Path, schema_lock_path: Path) -> dict[str, Any]:
    lock = load_json(schema_lock_path, "release-authority schema lock")
    require_exact_keys(lock, SCHEMA_LOCK_KEYS, "release-authority schema lock")
    expected_lock = {
        "contract": SCHEMA_CONTRACT,
        "repository": REGISTRY_REPOSITORY,
        "commit": SCHEMA_COMMIT,
        "path": SCHEMA_PATH,
        "sha256": SCHEMA_SHA256,
    }
    if lock != expected_lock:
        raise CompatibilityError("release-authority schema lock diverges from the reviewed Registry handoff")
    if sha256(schema_path) != SCHEMA_SHA256:
        raise CompatibilityError("release-authority schema bytes diverge from the pinned Registry digest")
    schema = load_json(schema_path, "release-authority v2 schema")
    definitions = schema.get("$defs")
    if not isinstance(definitions, dict):
        raise CompatibilityError("release-authority schema is missing $defs")
    snapshot_schema = definitions.get("snapshot")
    artifact_schema = definitions.get("artifact")
    current_schema = definitions.get("current")
    if not all(isinstance(item, dict) for item in (snapshot_schema, artifact_schema, current_schema)):
        raise CompatibilityError("release-authority schema lacks required contract definitions")
    assert isinstance(snapshot_schema, dict)
    assert isinstance(artifact_schema, dict)
    assert isinstance(current_schema, dict)
    snapshot_keys = set(snapshot_schema.get("required", []))
    artifact_keys = set(artifact_schema.get("required", []))
    current_keys = set(current_schema.get("required", []))
    if (
        len(snapshot_keys) != 21
        or snapshot_keys != set(snapshot_schema.get("properties", {}))
        or snapshot_schema.get("additionalProperties") is not False
        or len(artifact_keys) != 15
        or artifact_keys != set(artifact_schema.get("properties", {}))
        or artifact_schema.get("additionalProperties") is not False
        or current_keys != CURRENT_KEYS
        or current_schema.get("additionalProperties") is not False
    ):
        raise CompatibilityError("release-authority schema does not pin the exact v2 shapes")
    return {
        "schema": schema,
        "snapshotKeys": snapshot_keys,
        "artifactKeys": artifact_keys,
    }


def validate_artifact(payload: Any, expected_keys: set[str], index: int) -> dict[str, Any]:
    if not isinstance(payload, dict):
        raise CompatibilityError(f"authority snapshot artifact {index} must be an object")
    require_exact_keys(payload, expected_keys, f"authority snapshot artifact {index}")
    for key in ("artifactId", "head", "platform", "rid", "arch"):
        require_identifier(payload[key], f"artifact {index} {key}")
    if payload["kind"] != "installer":
        raise CompatibilityError(f"artifact {index} kind must be installer")
    require_sha(payload["sha256"], f"artifact {index} sha256")
    require_integer(payload["sizeBytes"], f"artifact {index} sizeBytes", 1)
    expected_constants = {
        "compatibilityState": "compatible",
        "promotionState": "promoted",
        "publicationScope": "signed-in-and-public",
        "revokeState": "not_revoked",
    }
    for key, expected in expected_constants.items():
        if payload[key] != expected:
            raise CompatibilityError(f"artifact {index} {key} must be {expected}")
    if payload["installAccessClass"] not in INSTALL_ACCESS_CLASSES:
        raise CompatibilityError(f"artifact {index} installAccessClass is invalid")
    download_url = require_text(payload["downloadUrl"], f"artifact {index} downloadUrl")
    lowered = download_url.lower()
    parsed = urlsplit(download_url)
    path_match = re.fullmatch(r"/downloads/g/([^/]+)/files/([^/]+)", parsed.path)
    if (
        parsed.scheme != "https"
        or not parsed.netloc
        or parsed.username
        or parsed.password
        or parsed.query
        or parsed.fragment
        or "\\" in download_url
        or any(character.isspace() for character in download_url)
        or any(encoded in lowered for encoded in ("%2e", "%2f", "%5c"))
        or path_match is None
        or any(part in {".", ".."} for part in path_match.groups())
    ):
        raise CompatibilityError(f"artifact {index} downloadUrl is not immutable and safe")
    public_route = require_text(
        payload["publicInstallRoute"], f"artifact {index} publicInstallRoute"
    )
    route_match = re.fullmatch(r"/downloads/install/([^/?#\\]+)", public_route)
    if (
        route_match is None
        or route_match.group(1) in {".", ".."}
        or any(encoded in public_route.lower() for encoded in ("%2e", "%2f", "%5c"))
    ):
        raise CompatibilityError(f"artifact {index} publicInstallRoute is unsafe")
    return payload


def validate_snapshot(
    snapshot: dict[str, Any], snapshot_keys: set[str], artifact_keys: set[str]
) -> dict[str, Any]:
    require_exact_keys(snapshot, snapshot_keys, "authority snapshot")
    if snapshot["authorityContract"] != AUTHORITY_CONTRACT:
        raise CompatibilityError("authority snapshot contract is not v2")
    release_version = require_release_version(snapshot["releaseVersion"])
    for key in ("channel", "status", "rolloutState", "supportabilityState"):
        require_token(snapshot[key], f"authority snapshot {key}")
    platforms = require_identifier_list(
        snapshot["availablePlatforms"], "authority snapshot availablePlatforms", sorted_values=True
    )
    primary_heads = require_primary_heads(
        snapshot["primaryHeadByPlatform"], "authority snapshot primaryHeadByPlatform"
    )
    artifact_count = require_integer(snapshot["artifactCount"], "artifactCount", 0)
    posture = snapshot["downloadAccessPosture"]
    if posture not in DOWNLOAD_ACCESS_POSTURES:
        raise CompatibilityError("authority snapshot downloadAccessPosture is invalid")
    require_text(snapshot["knownIssueSummary"], "knownIssueSummary")
    require_sha(snapshot["manifestSha256"], "manifestSha256")
    if snapshot["registryRepository"] != REGISTRY_REPOSITORY:
        raise CompatibilityError("authority snapshot Registry repository is not canonical")
    require_sha(snapshot["registryCommit"], "registryCommit", SHA40)
    if snapshot["releaseDecisionStatus"] not in DECISION_STATUSES:
        raise CompatibilityError("authority snapshot releaseDecisionStatus is invalid")
    require_sha(snapshot["releaseDecisionSha256"], "releaseDecisionSha256")
    if snapshot["releaseDecisionPath"] != "RELEASE_DECISION.json":
        raise CompatibilityError("authority snapshot releaseDecisionPath is invalid")
    require_text(snapshot["supportOwner"], "supportOwner")
    next_actions = snapshot["nextActions"]
    if not isinstance(next_actions, list) or any(
        not isinstance(item, str) or not item or item != item.strip() for item in next_actions
    ):
        raise CompatibilityError("authority snapshot nextActions must contain canonical text")
    if snapshot["releaseDecisionStatus"] == "review_required" and not next_actions:
        raise CompatibilityError("review-required authority must include a next action")
    if snapshot["manifestPath"] != "RELEASE_CHANNEL.json":
        raise CompatibilityError("authority snapshot manifestPath is invalid")
    artifacts_value = snapshot["artifacts"]
    if not isinstance(artifacts_value, list):
        raise CompatibilityError("authority snapshot artifacts must be an array")
    artifacts = [
        validate_artifact(artifact, artifact_keys, index)
        for index, artifact in enumerate(artifacts_value)
    ]
    artifact_ids = [str(artifact["artifactId"]) for artifact in artifacts]
    if artifact_ids != sorted(artifact_ids) or len(artifact_ids) != len(set(artifact_ids)):
        raise CompatibilityError("authority snapshot artifacts must be uniquely artifactId-sorted")
    if artifact_count != len(artifacts):
        raise CompatibilityError("authority snapshot artifactCount diverges from artifacts")
    derived_platforms = sorted({str(artifact["platform"]) for artifact in artifacts})
    if platforms != derived_platforms:
        raise CompatibilityError("authority snapshot availablePlatforms diverges from artifacts")
    if set(primary_heads) != set(platforms):
        raise CompatibilityError("authority snapshot must name exactly one primary head per platform")
    for platform, head in primary_heads.items():
        if not any(
            artifact["platform"] == platform and artifact["head"] == head
            for artifact in artifacts
        ):
            raise CompatibilityError("authority snapshot primary head is not backed by an artifact")
    access_classes = sorted({str(artifact["installAccessClass"]) for artifact in artifacts})
    derived_posture = (
        "unavailable"
        if not access_classes
        else access_classes[0]
        if len(access_classes) == 1
        else "mixed"
    )
    if posture != derived_posture:
        raise CompatibilityError("authority snapshot downloadAccessPosture diverges from artifacts")
    if not artifacts and snapshot["releaseDecisionStatus"] != "review_required":
        raise CompatibilityError("an empty authority shelf must remain review_required")
    if snapshot["releaseDecisionStatus"] in {"preview_ready", "stable_ready"} and not artifacts:
        raise CompatibilityError("ready authority requires at least one artifact")
    snapshot["releaseVersion"] = release_version
    return snapshot


def validate_current(current: dict[str, Any]) -> dict[str, Any]:
    require_exact_keys(current, CURRENT_KEYS, "CURRENT.json")
    require_release_version(current["releaseVersion"], "CURRENT.json releaseVersion")
    require_sha(current["snapshotSha256"], "CURRENT.json snapshotSha256")
    require_sha(current["decisionSha256"], "CURRENT.json decisionSha256")
    if current["status"] not in DECISION_STATUSES:
        raise CompatibilityError("CURRENT.json status is invalid")
    return current


def validate_acquisition_receipt(
    receipt: dict[str, Any],
    *,
    mode: str,
) -> dict[str, Any]:
    require_exact_keys(receipt, ACQUISITION_KEYS, "authority acquisition receipt")
    if (
        receipt["contract"] != ACQUISITION_CONTRACT
        or receipt["authenticationMode"] != ACQUISITION_MODE
        or receipt["registryRepository"] != REGISTRY_REPOSITORY
        or receipt["currentRef"] != "registry://release-evidence/CURRENT.json"
    ):
        raise CompatibilityError("authority acquisition receipt contract is invalid")
    commit = require_sha(receipt["registryCommit"], "acquisition registryCommit", SHA40)
    current_sha256 = require_sha(
        receipt["currentSha256"], "acquisition currentSha256"
    )
    source = require_ref(receipt["source"], "authority acquisition source")
    expected_source = (
        "https://raw.githubusercontent.com/"
        f"{REGISTRY_REPOSITORY}/{commit}/release-evidence/CURRENT.json"
    )
    if source != expected_source:
        raise CompatibilityError(
            "authority acquisition source is not the immutable Registry commit path"
        )
    if not isinstance(receipt["fixture"], bool) or not isinstance(
        receipt["releaseEvidenceEligible"], bool
    ):
        raise CompatibilityError("authority acquisition eligibility flags must be booleans")
    if mode == "release":
        if receipt["fixture"] or not receipt["releaseEvidenceEligible"]:
            raise CompatibilityError(
                "release mode requires an evidence-eligible nonfixture authority acquisition"
            )
    elif not receipt["fixture"] or receipt["releaseEvidenceEligible"]:
        raise CompatibilityError(
            "fixture acquisition receipts must remain evidence-ineligible"
        )
    return receipt


def authenticate_acquisition_source(
    receipt: dict[str, Any], current_path: Path
) -> None:
    """Replay acquisition from the immutable TLS-authenticated Registry source."""
    source = str(receipt["source"])
    try:
        request = urllib.request.Request(
            source,
            headers={"User-Agent": "chummer-media-release-authority/1"},
        )
        with urllib.request.urlopen(request, timeout=20) as response:
            if response.geturl() != source:
                raise CompatibilityError(
                    "authority acquisition source redirected away from the immutable pin"
                )
            downloaded = response.read(1024 * 1024 + 1)
    except CompatibilityError:
        raise
    except OSError as exc:
        raise CompatibilityError(
            f"unable to authenticate Registry authority acquisition: {exc}"
        ) from exc
    if len(downloaded) > 1024 * 1024:
        raise CompatibilityError("authenticated CURRENT.json exceeds the bounded size")
    try:
        local_bytes = current_path.read_bytes()
    except OSError as exc:
        raise CompatibilityError(f"unable to read CURRENT.json: {exc}") from exc
    if (
        downloaded != local_bytes
        or hashlib.sha256(downloaded).hexdigest() != receipt["currentSha256"]
    ):
        raise CompatibilityError(
            "authenticated Registry source bytes diverge from supplied CURRENT.json"
        )


def validate_preview_decision(decision: dict[str, Any], snapshot: dict[str, Any]) -> None:
    require_exact_keys(decision, PREVIEW_DECISION_KEYS, "preview release decision")
    if decision["contractName"] != "chummer.preview-release-decision/v1":
        raise CompatibilityError("preview release decision contract is invalid")
    if decision["releaseDecisionStatus"] not in {"review_required", "preview_ready"}:
        raise CompatibilityError("preview release decision status is invalid")
    if decision["status"] != decision["releaseDecisionStatus"]:
        raise CompatibilityError("preview release decision status aliases disagree")
    comparisons = {
        "releaseVersion": "releaseVersion",
        "channel": "channel",
        "manifestSha256": "manifestSha256",
        "registryCommit": "registryCommit",
        "platforms": "availablePlatforms",
        "primaryHeadByPlatform": "primaryHeadByPlatform",
        "supportOwner": "supportOwner",
    }
    for decision_key, snapshot_key in comparisons.items():
        if decision[decision_key] != snapshot[snapshot_key]:
            raise CompatibilityError(
                f"preview release decision {decision_key} diverges from authority snapshot"
            )
    if decision["releaseDecisionStatus"] != snapshot["releaseDecisionStatus"]:
        raise CompatibilityError("preview release decision posture diverges from authority snapshot")
    fallbacks = decision["fallbackHeadsByPlatform"]
    if not isinstance(fallbacks, dict):
        raise CompatibilityError("preview release fallbackHeadsByPlatform must be an object")
    for platform, heads in fallbacks.items():
        require_identifier(platform, "fallback platform")
        if platform not in snapshot["availablePlatforms"]:
            raise CompatibilityError("preview release fallback platform is outside authority scope")
        validated = require_identifier_list(heads, "fallback heads", sorted_values=True)
        if snapshot["primaryHeadByPlatform"].get(platform) in validated:
            raise CompatibilityError("preview release fallback heads include the primary head")
    expected_access = (
        "review_required" if snapshot["artifactCount"] == 0 else snapshot["downloadAccessPosture"]
    )
    if decision["artifactAccessClass"] != expected_access:
        raise CompatibilityError("preview release artifact access diverges from authority snapshot")
    closure = (
        decision["authoritySnapshotSha256"],
        decision["candidateDecisionStatus"],
        decision["candidateDecisionSha256"],
    )
    if decision["releaseDecisionStatus"] == "review_required" and closure == ("", "", ""):
        return
    require_sha(closure[0], "preview candidate authoritySnapshotSha256")
    if closure[1] not in {"review_required", "preview_ready"}:
        raise CompatibilityError("preview candidate decision status is invalid")
    require_sha(closure[2], "preview candidate decision SHA256")


def validate_stable_decision(decision: dict[str, Any], snapshot: dict[str, Any]) -> None:
    require_exact_keys(decision, STABLE_DECISION_KEYS, "stable release decision")
    if (
        decision["contract_name"] != "chummer.final_gold_graph"
        or decision["contract_version"] != 2
        or decision["releaseDecisionStatus"] != "stable_ready"
        or decision["status"] != "pass"
        or snapshot["releaseDecisionStatus"] != "stable_ready"
        or decision["releaseVersion"] != snapshot["releaseVersion"]
    ):
        raise CompatibilityError("stable release decision posture diverges from authority snapshot")
    live = decision["live_release"]
    authority = decision["release_authority"]
    if not isinstance(live, dict) or not isinstance(authority, dict):
        raise CompatibilityError("stable release decision authority sections must be objects")
    require_exact_keys(live, STABLE_LIVE_KEYS, "stable live_release")
    require_exact_keys(authority, STABLE_AUTHORITY_KEYS, "stable release_authority")
    live_expected = {
        "version": snapshot["releaseVersion"],
        "channel": snapshot["channel"],
        "manifest_sha256": snapshot["manifestSha256"],
        "registry_commit": snapshot["registryCommit"],
        "available_platforms": snapshot["availablePlatforms"],
        "primary_head_by_platform": snapshot["primaryHeadByPlatform"],
        "status": snapshot["status"],
        "rollout_state": snapshot["rolloutState"],
        "supportability_state": snapshot["supportabilityState"],
        "artifact_count": snapshot["artifactCount"],
        "download_access_posture": snapshot["downloadAccessPosture"],
        "known_issue_summary": snapshot["knownIssueSummary"],
        "release_decision_status": "stable_ready",
    }
    authority_expected = {
        "contract": AUTHORITY_CONTRACT,
        "manifest_sha256": snapshot["manifestSha256"],
        "registry_commit": snapshot["registryCommit"],
        "release_decision_status": "stable_ready",
    }
    if live != live_expected or authority != authority_expected:
        raise CompatibilityError("stable release decision scope diverges from authority snapshot")


def validate_decision(decision: dict[str, Any], snapshot: dict[str, Any]) -> None:
    if "contractName" in decision:
        validate_preview_decision(decision, snapshot)
    elif "contract_name" in decision:
        validate_stable_decision(decision, snapshot)
    else:
        raise CompatibilityError("release decision does not use a recognized exact contract")


def validate_provenance(
    provenance: dict[str, Any],
    *,
    snapshot: dict[str, Any],
    current_digest: str,
    snapshot_digest: str,
    manifest_digest: str,
    decision_digest: str,
    asset_id: str,
    asset_content_sha256: str,
    canonical_media_manifest_sha256: str,
) -> None:
    require_exact_keys(provenance, PROVENANCE_KEYS, "media release provenance")
    expected = {
        "contract": PROVENANCE_CONTRACT,
        "releaseVersion": snapshot["releaseVersion"],
        "registryRepository": REGISTRY_REPOSITORY,
        "registryCommit": snapshot["registryCommit"],
        "currentSha256": current_digest,
        "authoritySnapshotSha256": snapshot_digest,
        "manifestSha256": manifest_digest,
        "releaseDecisionSha256": decision_digest,
        "assetId": asset_id,
        "assetContentSha256": asset_content_sha256,
        "canonicalMediaManifestSha256": canonical_media_manifest_sha256,
    }
    if provenance != expected:
        raise CompatibilityError("media provenance is not anchored to CURRENT/snapshot/manifest/decision")


def verify(
    *,
    mode: str,
    schema_path: Path = DEFAULT_SCHEMA,
    schema_lock_path: Path = DEFAULT_SCHEMA_LOCK,
    current_path: Path,
    acquisition_receipt_path: Path,
    snapshot_path: Path,
    manifest_path: Path,
    decision_path: Path,
    provenance_path: Path,
    binding_path: Path,
    media_manifest_path: Path,
    eligibility_path: Path,
) -> dict[str, Any]:
    if mode not in {"fixture", "release"}:
        raise CompatibilityError("mode must be fixture or release")
    schema_handoff = verify_schema_handoff(schema_path, schema_lock_path)
    acquisition_digest = sha256(acquisition_receipt_path)
    acquisition = validate_acquisition_receipt(
        load_json(acquisition_receipt_path, "authority acquisition receipt"),
        mode=mode,
    )
    current_digest = sha256(current_path)
    if current_digest != acquisition["currentSha256"]:
        raise CompatibilityError(
            "CURRENT.json bytes do not match the independently authenticated acquisition"
        )
    if mode == "release":
        authenticate_acquisition_source(acquisition, current_path)
    current = validate_current(load_json(current_path, "CURRENT.json"))
    snapshot_digest = sha256(snapshot_path)
    if current["snapshotSha256"] != snapshot_digest:
        raise CompatibilityError("CURRENT.json does not select the supplied authority snapshot")
    snapshot = validate_snapshot(
        load_json(snapshot_path, "authority snapshot"),
        schema_handoff["snapshotKeys"],
        schema_handoff["artifactKeys"],
    )
    if acquisition["registryCommit"] != snapshot["registryCommit"]:
        raise CompatibilityError(
            "authenticated acquisition issuer commit diverges from the authority snapshot"
        )
    manifest_digest = sha256(manifest_path)
    decision_digest = sha256(decision_path)
    provenance_digest = sha256(provenance_path)
    decision = load_json(decision_path, "release decision")
    provenance = load_json(provenance_path, "media release provenance")
    binding = load_json(binding_path, "media release binding")
    media_manifest = load_json(media_manifest_path, "media asset manifest")
    eligibility = load_json(eligibility_path, "media public eligibility")
    require_exact_keys(binding, BINDING_KEYS, "media release binding")
    if current["releaseVersion"] != snapshot["releaseVersion"]:
        raise CompatibilityError("CURRENT.json releaseVersion diverges from the authority snapshot")
    if current["decisionSha256"] != decision_digest:
        raise CompatibilityError("CURRENT.json does not select the supplied release decision")
    if current["status"] != snapshot["releaseDecisionStatus"]:
        raise CompatibilityError("CURRENT.json status diverges from the authority snapshot")
    if snapshot["manifestSha256"] != manifest_digest:
        raise CompatibilityError("manifest bytes diverge from the authority snapshot")
    if snapshot["releaseDecisionSha256"] != decision_digest:
        raise CompatibilityError("decision bytes diverge from the authority snapshot")
    asset_id = require_text(media_manifest.get("assetId"), "media manifest assetId")
    asset_content_sha256 = require_sha(
        media_manifest.get("contentHash"), "media manifest contentHash"
    )
    canonical_manifest_sha256 = canonical_media_manifest_sha256(media_manifest)
    validate_eligibility(
        eligibility,
        snapshot_sha256=snapshot_digest,
        asset_id=asset_id,
        content_sha256=asset_content_sha256,
        canonical_manifest_sha256=canonical_manifest_sha256,
    )
    validate_public_media_manifest(media_manifest, eligibility)
    validate_decision(decision, snapshot)
    validate_provenance(
        provenance,
        snapshot=snapshot,
        current_digest=current_digest,
        snapshot_digest=snapshot_digest,
        manifest_digest=manifest_digest,
        decision_digest=decision_digest,
        asset_id=asset_id,
        asset_content_sha256=asset_content_sha256,
        canonical_media_manifest_sha256=canonical_manifest_sha256,
    )
    expected_binding = {
        "authorityContract": AUTHORITY_CONTRACT,
        "registryRepository": REGISTRY_REPOSITORY,
        "registryCommit": snapshot["registryCommit"],
        "releaseVersion": snapshot["releaseVersion"],
        "currentSha256": current_digest,
        "authoritySnapshotSha256": snapshot_digest,
        "manifestSha256": manifest_digest,
        "releaseDecisionSha256": decision_digest,
        "releaseDecisionStatus": snapshot["releaseDecisionStatus"],
        "provenanceSha256": provenance_digest,
        "assetId": asset_id,
        "assetContentSha256": asset_content_sha256,
        "canonicalMediaManifestSha256": canonical_manifest_sha256,
    }
    for key, expected in expected_binding.items():
        if binding.get(key) != expected:
            raise CompatibilityError(f"media release binding {key} diverges from authority bytes")
    for key in (
        "currentRef",
        "authoritySnapshotRef",
        "manifestRef",
        "releaseDecisionRef",
        "provenanceRef",
    ):
        require_ref(binding.get(key), key)
    release_version = snapshot["releaseVersion"]
    generation_prefix = (
        f"registry://release-evidence/snapshots/{release_version}/{snapshot_digest}"
    )
    exact_release_refs = {
        "currentRef": "registry://release-evidence/CURRENT.json",
        "authoritySnapshotRef": f"{generation_prefix}/SNAPSHOT.json",
        "manifestRef": f"{generation_prefix}/RELEASE_CHANNEL.json",
        "releaseDecisionRef": f"{generation_prefix}/RELEASE_DECISION.json",
        "provenanceRef": (
            "release-evidence://media-factory/snapshots/"
            f"{release_version}/{snapshot_digest}/decisions/{decision_digest}/"
            f"assets/{asset_id}/{asset_content_sha256}/"
            f"manifests/{canonical_manifest_sha256}/"
            f"provenance/{provenance_digest}.json"
        ),
    }
    for key, expected in exact_release_refs.items():
        if binding[key] != expected:
            raise CompatibilityError(f"{key} is not the canonical content-bound authority reference")
    if mode == "release" and (
        re.search(
            r"(?:^|[-_.])(fixture|test|example)(?:$|[-_.])",
            release_version,
            re.IGNORECASE,
        )
        or any("example.invalid" in str(binding[key]).lower() for key in BINDING_KEYS)
    ):
        raise CompatibilityError("release mode cannot consume fixture authority")
    return {
        "contract": "chummer.media.release-snapshot-compatibility/v3",
        "status": "pass",
        "mode": mode,
        "releaseEvidenceEligible": mode == "release",
        "releaseVersion": release_version,
        "registryCommit": snapshot["registryCommit"],
        "schemaRepository": REGISTRY_REPOSITORY,
        "schemaCommit": SCHEMA_COMMIT,
        "schemaSha256": SCHEMA_SHA256,
        "authorityAcquisitionSha256": acquisition_digest,
        "authorityAcquisitionSource": acquisition["source"],
        "currentSha256": current_digest,
        "authoritySnapshotSha256": snapshot_digest,
        "manifestSha256": manifest_digest,
        "releaseDecisionSha256": decision_digest,
        "releaseDecisionStatus": snapshot["releaseDecisionStatus"],
        "provenanceSha256": provenance_digest,
        "assetId": asset_id,
        "assetContentSha256": asset_content_sha256,
        "canonicalMediaManifestSha256": canonical_manifest_sha256,
    }


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--mode", choices=("fixture", "release"), default="fixture")
    parser.add_argument("--schema", type=Path, default=DEFAULT_SCHEMA)
    parser.add_argument("--schema-lock", type=Path, default=DEFAULT_SCHEMA_LOCK)
    parser.add_argument("--current", required=True, type=Path)
    parser.add_argument("--acquisition-receipt", required=True, type=Path)
    parser.add_argument("--snapshot", required=True, type=Path)
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--decision", required=True, type=Path)
    parser.add_argument("--provenance", required=True, type=Path)
    parser.add_argument("--binding", required=True, type=Path)
    parser.add_argument("--media-manifest", required=True, type=Path)
    parser.add_argument("--eligibility", required=True, type=Path)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    result = verify(
        mode=args.mode,
        schema_path=args.schema,
        schema_lock_path=args.schema_lock,
        current_path=args.current,
        acquisition_receipt_path=args.acquisition_receipt,
        snapshot_path=args.snapshot,
        manifest_path=args.manifest,
        decision_path=args.decision,
        provenance_path=args.provenance,
        binding_path=args.binding,
        media_manifest_path=args.media_manifest,
        eligibility_path=args.eligibility,
    )
    print(json.dumps(result, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except CompatibilityError as exc:
        print(f"media release compatibility: {exc}", file=sys.stderr)
        raise SystemExit(1) from exc
