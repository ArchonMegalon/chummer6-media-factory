#!/usr/bin/env python3
"""Verify the bounded design mirror against one exact external owner commit."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import subprocess
import sys
import tempfile
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[2]
LOCK_PATH = ROOT / "eng/design-mirror.lock.json"
MIRROR_ROOT = ROOT / ".codex-design/product"
LOCK_KEYS = {
    "contract",
    "repository",
    "repositoryUrl",
    "commit",
    "sourceRoot",
    "files",
}
FILE_KEYS = {"path", "sha256"}
SHA40 = re.compile(r"^[0-9a-f]{40}$")
SHA256 = re.compile(r"^[0-9a-f]{64}$")


class DesignMirrorError(RuntimeError):
    """Raised when mirror bytes diverge from their external owner."""


def clean_git_environment() -> dict[str, str]:
    environment = {key: value for key, value in os.environ.items() if not key.startswith("GIT_")}
    environment.update(
        {
            "GIT_TERMINAL_PROMPT": "0",
            "GIT_CONFIG_GLOBAL": os.devnull,
            "GIT_CONFIG_SYSTEM": os.devnull,
            "GIT_CONFIG_NOSYSTEM": "1",
        }
    )
    return environment


def run_git(
    arguments: list[str], *, repository: Path, environment: dict[str, str], binary: bool = False
) -> bytes | str:
    completed = subprocess.run(
        ["git", *arguments],
        cwd=repository,
        env=environment,
        check=False,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=not binary,
    )
    if completed.returncode != 0:
        stderr = completed.stderr.decode("utf-8", errors="replace") if binary else completed.stderr
        raise DesignMirrorError(f"git {' '.join(arguments)} failed: {stderr.strip()}")
    return completed.stdout


def load_lock() -> dict[str, Any]:
    try:
        payload = json.loads(LOCK_PATH.read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise DesignMirrorError(f"unable to load design mirror lock: {exc}") from exc
    if not isinstance(payload, dict) or set(payload) != LOCK_KEYS:
        raise DesignMirrorError("design mirror lock has an invalid top-level shape")
    if (
        payload["contract"] != "chummer.media.design-mirror-lock/v1"
        or payload["repository"] != "ArchonMegalon/chummer6-design"
        or payload["repositoryUrl"] != "https://github.com/ArchonMegalon/chummer6-design.git"
        or SHA40.fullmatch(str(payload["commit"])) is None
        or payload["sourceRoot"] != "products/chummer"
    ):
        raise DesignMirrorError("design mirror lock authority is invalid")
    files = payload["files"]
    if not isinstance(files, list) or not files:
        raise DesignMirrorError("design mirror lock must contain file rows")
    paths: list[str] = []
    for row in files:
        if not isinstance(row, dict) or set(row) != FILE_KEYS:
            raise DesignMirrorError("design mirror lock contains an invalid file row")
        path = row["path"]
        if (
            not isinstance(path, str)
            or not path
            or path.startswith("/")
            or "\\" in path
            or any(segment in {"", ".", ".."} for segment in path.split("/"))
            or SHA256.fullmatch(str(row["sha256"])) is None
        ):
            raise DesignMirrorError("design mirror lock contains an unsafe file row")
        paths.append(path)
    if paths != sorted(paths) or len(paths) != len(set(paths)):
        raise DesignMirrorError("design mirror lock file rows must be unique and sorted")
    return payload


def verify_repository(repository: Path, lock: dict[str, Any]) -> dict[str, str]:
    environment = clean_git_environment()
    head = run_git(["rev-parse", "HEAD"], repository=repository, environment=environment)
    assert isinstance(head, str)
    if head.strip() != lock["commit"]:
        raise DesignMirrorError("design source checkout is not at the exact locked commit")
    verified: dict[str, str] = {}
    mirror_root = MIRROR_ROOT.resolve()
    for row in lock["files"]:
        relative_path = row["path"]
        source_path = f"{lock['sourceRoot']}/{relative_path}"
        source_bytes = run_git(
            ["show", f"{lock['commit']}:{source_path}"],
            repository=repository,
            environment=environment,
            binary=True,
        )
        assert isinstance(source_bytes, bytes)
        source_digest = hashlib.sha256(source_bytes).hexdigest()
        if source_digest != row["sha256"]:
            raise DesignMirrorError(f"owner bytes for {relative_path} diverge from the lock")
        mirror_path = MIRROR_ROOT / relative_path
        if mirror_path.is_symlink() or not mirror_path.is_file():
            raise DesignMirrorError(f"missing or unsafe local mirror: {mirror_path}")
        resolved = mirror_path.resolve()
        if not resolved.is_relative_to(mirror_root):
            raise DesignMirrorError(f"local mirror escapes its bounded root: {mirror_path}")
        mirror_bytes = mirror_path.read_bytes()
        if hashlib.sha256(mirror_bytes).hexdigest() != source_digest or mirror_bytes != source_bytes:
            raise DesignMirrorError(f"stale local mirror: {relative_path}")
        verified[relative_path] = source_digest
    return verified


def verify(source_root: Path | None) -> dict[str, Any]:
    lock = load_lock()
    if source_root is not None:
        verified = verify_repository(source_root.resolve(), lock)
    else:
        with tempfile.TemporaryDirectory(prefix="chummer-media-design-mirror-") as temporary:
            repository = Path(temporary) / "owner"
            repository.mkdir()
            environment = clean_git_environment()
            run_git(["init", "--quiet"], repository=repository, environment=environment)
            run_git(
                ["remote", "add", "origin", lock["repositoryUrl"]],
                repository=repository,
                environment=environment,
            )
            run_git(
                ["fetch", "--quiet", "--depth", "1", "origin", lock["commit"]],
                repository=repository,
                environment=environment,
            )
            run_git(
                ["checkout", "--quiet", "--detach", "FETCH_HEAD"],
                repository=repository,
                environment=environment,
            )
            verified = verify_repository(repository, lock)
    return {
        "contract": "chummer.media.design-mirror-verification/v1",
        "status": "pass",
        "repository": lock["repository"],
        "commit": lock["commit"],
        "fileCount": len(verified),
        "files": verified,
    }


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-root", type=Path)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    print(json.dumps(verify(args.source_root), indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (DesignMirrorError, OSError, ValueError) as exc:
        print(f"design mirror verification failed: {exc}", file=sys.stderr)
        raise SystemExit(1) from exc
