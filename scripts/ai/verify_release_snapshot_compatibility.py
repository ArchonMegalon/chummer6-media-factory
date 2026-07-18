#!/usr/bin/env python3
"""Verify that a public media binding matches exact Registry release bytes."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from pathlib import Path
from typing import Any
from urllib.parse import unquote, urlsplit


AUTHORITY_CONTRACT = "chummer.release-authority-snapshot/v2"
REGISTRY_REPOSITORY = "ArchonMegalon/chummer6-hub-registry"
SHA40 = re.compile(r"^[0-9a-f]{40}$")
SHA256 = re.compile(r"^[0-9a-f]{64}$")
RELEASE_VERSION = re.compile(r"^[A-Za-z0-9._+-]{1,128}$")
DECISION_STATUSES = {"review_required", "preview_ready", "stable_ready"}
BINDING_KEYS = {
    "authorityContract",
    "registryRepository",
    "registryCommit",
    "releaseVersion",
    "authoritySnapshotRef",
    "authoritySnapshotSha256",
    "manifestRef",
    "manifestSha256",
    "releaseDecisionRef",
    "releaseDecisionSha256",
    "releaseDecisionStatus",
    "provenanceRef",
    "provenanceSha256",
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
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def load_json(path: Path, label: str) -> dict[str, Any]:
    try:
        payload = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise CompatibilityError(f"unable to load {label}: {exc}") from exc
    if not isinstance(payload, dict):
        raise CompatibilityError(f"{label} must be a JSON object")
    return payload


def require_sha(value: Any, label: str, pattern: re.Pattern[str] = SHA256) -> str:
    if not isinstance(value, str) or pattern.fullmatch(value) is None:
        raise CompatibilityError(f"{label} must be an exact lowercase digest")
    return value


def require_ref(value: Any, label: str) -> str:
    if not isinstance(value, str) or not value or value != value.strip():
        raise CompatibilityError(f"{label} must be canonical non-empty text")
    lowered = value.lower()
    if any(fragment in lowered for fragment in FORBIDDEN_LOCAL_FRAGMENTS):
        raise CompatibilityError(f"{label} contains a machine-local path")
    if "\\" in value or any(
        character.isspace() or ord(character) < 32 or ord(character) == 127
        for character in value
    ):
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
    decoded_segments = unquote(parsed.path).split("/")
    if any(segment in {".", ".."} for segment in decoded_segments):
        raise CompatibilityError(f"{label} contains traversal")
    return value


def verify(
    *,
    mode: str,
    snapshot_path: Path,
    expected_snapshot_sha256: str,
    manifest_path: Path,
    decision_path: Path,
    provenance_path: Path,
    binding_path: Path,
) -> dict[str, Any]:
    expected_snapshot_sha256 = require_sha(
        expected_snapshot_sha256, "expected snapshot SHA256"
    )
    snapshot_digest = sha256(snapshot_path)
    if snapshot_digest != expected_snapshot_sha256:
        raise CompatibilityError("authority snapshot bytes do not match the expected digest")

    snapshot = load_json(snapshot_path, "authority snapshot")
    decision = load_json(decision_path, "release decision")
    binding = load_json(binding_path, "media release binding")
    if set(binding) != BINDING_KEYS:
        raise CompatibilityError("media release binding must contain the exact fields")

    if snapshot.get("authorityContract") != AUTHORITY_CONTRACT:
        raise CompatibilityError("authority snapshot contract is not v2")
    if snapshot.get("registryRepository") != REGISTRY_REPOSITORY:
        raise CompatibilityError("authority snapshot Registry repository is not canonical")
    registry_commit = require_sha(snapshot.get("registryCommit"), "registry commit", SHA40)
    release_version = snapshot.get("releaseVersion")
    if (
        not isinstance(release_version, str)
        or RELEASE_VERSION.fullmatch(release_version) is None
        or release_version in {".", ".."}
    ):
        raise CompatibilityError("authority snapshot releaseVersion is invalid")
    release_decision_status = snapshot.get("releaseDecisionStatus")
    if release_decision_status not in DECISION_STATUSES:
        raise CompatibilityError("authority snapshot decision status is invalid")

    manifest_digest = sha256(manifest_path)
    decision_digest = sha256(decision_path)
    provenance_digest = sha256(provenance_path)
    if snapshot.get("manifestSha256") != manifest_digest:
        raise CompatibilityError("manifest bytes diverge from the authority snapshot")
    if snapshot.get("releaseDecisionSha256") != decision_digest:
        raise CompatibilityError("decision bytes diverge from the authority snapshot")
    if decision.get("releaseVersion") != release_version:
        raise CompatibilityError("release decision version diverges from the authority snapshot")
    if decision.get("releaseDecisionStatus") != release_decision_status:
        raise CompatibilityError("release decision status diverges from the authority snapshot")

    expected_binding = {
        "authorityContract": AUTHORITY_CONTRACT,
        "registryRepository": REGISTRY_REPOSITORY,
        "registryCommit": registry_commit,
        "releaseVersion": release_version,
        "authoritySnapshotSha256": snapshot_digest,
        "manifestSha256": manifest_digest,
        "releaseDecisionSha256": decision_digest,
        "releaseDecisionStatus": release_decision_status,
        "provenanceSha256": provenance_digest,
    }
    for key, expected in expected_binding.items():
        if binding.get(key) != expected:
            raise CompatibilityError(f"media release binding {key} diverges from authority bytes")
    for key in (
        "authoritySnapshotRef",
        "manifestRef",
        "releaseDecisionRef",
        "provenanceRef",
    ):
        require_ref(binding.get(key), key)

    exact_release_refs = {
        "authoritySnapshotRef": (
            f"registry://release-evidence/{release_version}/SNAPSHOT.json"
        ),
        "manifestRef": (
            f"registry://release-evidence/{release_version}/RELEASE_CHANNEL.json"
        ),
        "releaseDecisionRef": (
            f"registry://release-evidence/{release_version}/RELEASE_DECISION.json"
        ),
    }
    for key, expected in exact_release_refs.items():
        if binding[key] != expected:
            raise CompatibilityError(f"{key} is not the canonical release-evidence reference")
    provenance_prefix = (
        f"release-evidence://release-evidence/{release_version}/provenance/"
    )
    provenance_ref = str(binding["provenanceRef"])
    if (
        not provenance_ref.startswith(provenance_prefix)
        or not provenance_ref.endswith(".json")
        or len(provenance_ref) == len(provenance_prefix) + len(".json")
    ):
        raise CompatibilityError("provenanceRef is not a release-scoped JSON authority record")

    if mode == "release" and (
        re.search(r"(?:^|[-_.])(fixture|test|example)(?:$|[-_.])", release_version, re.IGNORECASE)
        or any("example.invalid" in str(binding[key]).lower() for key in BINDING_KEYS)
    ):
        raise CompatibilityError("release mode cannot consume fixture authority")

    return {
        "contract": "chummer.media.release-snapshot-compatibility/v1",
        "status": "pass",
        "mode": mode,
        "releaseEvidenceEligible": mode == "release",
        "releaseVersion": release_version,
        "registryCommit": registry_commit,
        "authoritySnapshotSha256": snapshot_digest,
        "manifestSha256": manifest_digest,
        "releaseDecisionSha256": decision_digest,
        "releaseDecisionStatus": release_decision_status,
        "provenanceSha256": provenance_digest,
    }


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--mode", choices=("fixture", "release"), default="fixture")
    parser.add_argument("--snapshot", required=True, type=Path)
    parser.add_argument("--snapshot-sha256", required=True)
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--decision", required=True, type=Path)
    parser.add_argument("--provenance", required=True, type=Path)
    parser.add_argument("--binding", required=True, type=Path)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    result = verify(
        mode=args.mode,
        snapshot_path=args.snapshot,
        expected_snapshot_sha256=args.snapshot_sha256,
        manifest_path=args.manifest,
        decision_path=args.decision,
        provenance_path=args.provenance,
        binding_path=args.binding,
    )
    print(json.dumps(result, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except CompatibilityError as exc:
        print(f"media release compatibility: {exc}", file=sys.stderr)
        raise SystemExit(1) from exc
