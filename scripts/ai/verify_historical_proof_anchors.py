#!/usr/bin/env python3
"""Verify archived raw Git commit objects for unreachable historical proof floors."""

from __future__ import annotations

import hashlib
import json
import re
import sys
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[2]
LOCK_PATH = ROOT / "eng/historical-proof-anchors.lock.json"
LOCK_KEYS = {"contract", "repository", "repositoryUrl", "commits"}
COMMIT_KEYS = {
    "name",
    "gitObjectPath",
    "gitObjectSha1",
    "payloadSha256",
    "treeSha1",
    "parentSha1",
}
SHA1 = re.compile(r"^[0-9a-f]{40}$")
SHA256 = re.compile(r"^[0-9a-f]{64}$")
NAME = re.compile(r"^[a-z0-9][a-z0-9._-]*$")


class ProofAnchorError(RuntimeError):
    """Raised when an archived historical proof anchor is not exact."""


def load_lock() -> dict[str, Any]:
    try:
        payload = json.loads(LOCK_PATH.read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise ProofAnchorError(f"unable to load historical proof lock: {exc}") from exc
    if not isinstance(payload, dict) or set(payload) != LOCK_KEYS:
        raise ProofAnchorError("historical proof lock has an invalid top-level shape")
    if (
        payload["contract"] != "chummer.media.historical-proof-anchors-lock/v1"
        or payload["repository"] != "ArchonMegalon/chummer6-media-factory"
        or payload["repositoryUrl"]
        != "https://github.com/ArchonMegalon/chummer6-media-factory.git"
    ):
        raise ProofAnchorError("historical proof lock authority is invalid")
    commits = payload["commits"]
    if not isinstance(commits, list) or not commits:
        raise ProofAnchorError("historical proof lock must contain commit rows")
    names: list[str] = []
    object_ids: list[str] = []
    for row in commits:
        if not isinstance(row, dict) or set(row) != COMMIT_KEYS:
            raise ProofAnchorError("historical proof lock contains an invalid commit row")
        path = row["gitObjectPath"]
        if (
            NAME.fullmatch(str(row["name"])) is None
            or not isinstance(path, str)
            or not path.startswith("eng/historical-proof-anchors/")
            or not path.endswith(".commit")
            or "\\" in path
            or any(segment in {"", ".", ".."} for segment in path.split("/"))
            or SHA1.fullmatch(str(row["gitObjectSha1"])) is None
            or SHA256.fullmatch(str(row["payloadSha256"])) is None
            or SHA1.fullmatch(str(row["treeSha1"])) is None
            or SHA1.fullmatch(str(row["parentSha1"])) is None
        ):
            raise ProofAnchorError("historical proof lock contains an invalid commit identity")
        names.append(row["name"])
        object_ids.append(row["gitObjectSha1"])
    if (
        names != sorted(names)
        or len(names) != len(set(names))
        or len(object_ids) != len(set(object_ids))
    ):
        raise ProofAnchorError("historical proof rows must be uniquely name-sorted")
    return payload


def verify_commit(row: dict[str, str]) -> dict[str, str]:
    payload_path = ROOT / row["gitObjectPath"]
    if payload_path.is_symlink() or not payload_path.is_file():
        raise ProofAnchorError(f"historical proof payload is missing or unsafe: {payload_path}")
    resolved = payload_path.resolve()
    anchor_root = (ROOT / "eng/historical-proof-anchors").resolve()
    if not resolved.is_relative_to(anchor_root):
        raise ProofAnchorError("historical proof payload escapes its bounded directory")
    payload = payload_path.read_bytes()
    if hashlib.sha256(payload).hexdigest() != row["payloadSha256"]:
        raise ProofAnchorError(f"historical proof payload {row['name']} diverges from its SHA-256")
    git_object = f"commit {len(payload)}\0".encode("ascii") + payload
    if hashlib.sha1(git_object, usedforsecurity=False).hexdigest() != row["gitObjectSha1"]:
        raise ProofAnchorError(f"historical proof payload {row['name']} is not the pinned Git commit")
    try:
        headers, message = payload.split(b"\n\n", 1)
        header_lines = headers.decode("utf-8").splitlines()
    except (ValueError, UnicodeDecodeError) as exc:
        raise ProofAnchorError(f"historical proof payload {row['name']} is malformed") from exc
    expected_prefix = [f"tree {row['treeSha1']}", f"parent {row['parentSha1']}"]
    if header_lines[:2] != expected_prefix or not message.strip():
        raise ProofAnchorError(f"historical proof payload {row['name']} metadata diverges")
    return {
        "name": row["name"],
        "gitObjectSha1": row["gitObjectSha1"],
        "payloadSha256": row["payloadSha256"],
        "treeSha1": row["treeSha1"],
        "parentSha1": row["parentSha1"],
    }


def verify() -> dict[str, Any]:
    lock = load_lock()
    return {
        "contract": "chummer.media.historical-proof-anchors-verification/v1",
        "status": "pass",
        "repository": lock["repository"],
        "commits": [verify_commit(row) for row in lock["commits"]],
    }


if __name__ == "__main__":
    try:
        print(json.dumps(verify(), indent=2, sort_keys=True))
    except (OSError, ValueError, ProofAnchorError) as exc:
        print(f"historical proof anchors: {exc}", file=sys.stderr)
        raise SystemExit(1) from exc
