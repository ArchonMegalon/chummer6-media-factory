"""Private Origin illustration retention; invoked only by trusted Hub orchestration.

Not a public API or an authorization service. Hub must authenticate the owner,
admit consent/quota, and supply a composed origin-dossier-media contract. Reads
never execute a provider. A committed dispatch fence survives process death.
"""
from __future__ import annotations

import hashlib
import io
import json
import os
import re
import sqlite3
import time
import tempfile
from contextlib import contextmanager
from pathlib import Path
from typing import Callable

from PIL import Image


MAX_IMAGE = 4 * 1024 * 1024
MAX_PACKET = 64 * 1024
SCHEMA = "chummer.origin.chapter-scene/v1"
MANIFEST_SCHEMA = "chummer.media.origin-scene/v1"


def encoded(value: object) -> bytes:
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"), allow_nan=False).encode("utf-8")


def digest(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def require_sha(value: object) -> str:
    if not isinstance(value, str) or not re.fullmatch(r"[0-9a-f]{64}", value):
        raise ValueError("A canonical SHA-256 is required.")
    return value


def text(value: object, maximum: int) -> str:
    if not isinstance(value, str) or not value.strip() or len(value.encode("utf-8")) > maximum or "\0" in value:
        raise ValueError("Invalid or oversized scene text.")
    return value


def identity(owner: str, payload: dict) -> str:
    return digest("\0".join([owner, *(payload[k] for k in ("workspaceId", "chapterId", "chapterDigest", "textDigest"))]).encode("utf-8"))


def unique_object(pairs):
    value = {}
    for key, item in pairs:
        if key in value:
            raise ValueError("Duplicate field.")
        value[key] = item
    return value


def capture(owner: str, contract: dict) -> tuple[str, dict]:
    """Capture the existing governed contract, with one chapter-bound artifact.

    owner is a Hub-derived opaque SHA-256, never accepted from a phone parameter.
    The contract is an internal, already-admitted message, not a bearer capability.
    """
    require_sha(owner)
    if len(encoded(contract)) > MAX_PACKET:
        raise ValueError("Scene contract is too large.")
    contract = json.loads(encoded(contract), object_pairs_hook=unique_object)
    expected = {
        "contractName": "chummer6-hub.horizon_governed_render_request.v1",
        "contractVersion": "2026-06-30", "orchestrationLane": "ea_governed_render",
        "horizonId": "origin-dossier", "capabilityId": "origin-dossier-media",
        "artifactKind": "dossier_media", "capabilitySlot": "approved_origin_media",
        "requestedBy": "origin-owner:" + owner, "audience": "private",
    }
    if any(contract.get(k) != v for k, v in expected.items()):
        raise ValueError("The private Origin render contract does not match its owner/lane.")
    if contract.get("preferredProvider") not in (None, "onemin", "phygital"):
        raise ValueError("This Origin provider is not in scope.")
    artifacts = contract.get("artifacts")
    if not isinstance(artifacts, list) or len(artifacts) != 1 or not isinstance(artifacts[0], dict):
        raise ValueError("Exactly one scene is required per request.")
    artifact = artifacts[0]
    if (artifact.get("role") != "chapter_scene" or artifact.get("category") != "origin/chapter-scene"
            or artifact.get("outputFormat") != "png" or artifact.get("maxBytes") != MAX_IMAGE
            or artifact.get("requiresApproval") is not True or artifact.get("persistOnApproval") is not True
            or artifact.get("allowPersistentPinning") is not False):
        raise ValueError("Invalid Origin image/lifecycle policy.")
    payload = json.loads(text(artifact.get("payload"), 16 * 1024), object_pairs_hook=unique_object)
    if not isinstance(payload, dict) or set(payload) != {
        "schema", "workspaceId", "chapterId", "chapterDigest", "textDigest", "prompt", "altText"
    } or payload["schema"] != SCHEMA:
        raise ValueError("Invalid chapter scene payload.")
    text(payload["workspaceId"], 256)
    text(payload["chapterId"], 256)
    require_sha(payload["chapterDigest"])
    require_sha(payload["textDigest"])
    text(payload["prompt"], 4096)
    text(payload["altText"], 1024)
    key = identity(owner, payload)
    source = "origin-scene:" + key
    if (contract.get("sourceRef") != source or contract.get("workItemId") != key
            or artifact.get("deduplicationKey") != key or artifact.get("artifactId") != key
            or source not in contract.get("truthRefs", [])
            or "origin-text:" + payload["textDigest"] not in contract.get("evidenceRefs", [])):
        raise ValueError("Scene source, text and deduplication binding differ.")
    return key, payload


def inspect_png(data: bytes) -> tuple[int, int]:
    if not isinstance(data, bytes) or not 0 < len(data) <= MAX_IMAGE:
        raise ValueError("Scene image exceeds the byte limit.")
    with Image.open(io.BytesIO(data)) as image:
        if (image.format != "PNG" or getattr(image, "n_frames", 1) != 1
                or not 1 <= image.width <= 4096 or not 1 <= image.height <= 4096):
            raise ValueError("Only bounded, still PNG scenes are supported.")
        size = image.size
        image.verify()
    with Image.open(io.BytesIO(data)) as image:
        image.load()  # A plausible header alone is not a decoded image.
    return size


class OriginSceneStore:
    """Media-owned volume, not Hub/wwwroot or an Android provider cache.

    Scene blobs and manifests commit in the same SQLite transaction. Use SQLite
    backup (not a live bare-file copy) for this volume. No provider URLs are stored
    in client manifests; provider-private receipt custody remains in Media Factory.
    """
    def __init__(self, database: Path):
        self.database = database
        if not database.is_absolute() or not database.parent.is_dir() or database.is_symlink():
            raise ValueError("An existing private media volume is required.")
        if database.parent.stat().st_mode & 0o077:
            raise ValueError("The private media directory must not be group/world accessible.")
        if not database.exists():
            descriptor = os.open(database, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
            os.close(descriptor)
        if database.stat().st_mode & 0o077:
            raise ValueError("The private media database must not be group/world accessible.")
        with self.connect() as connection:
            connection.execute("""CREATE TABLE IF NOT EXISTS origin_scenes (
                id TEXT PRIMARY KEY, owner TEXT NOT NULL, request_digest TEXT NOT NULL,
                state TEXT NOT NULL, expires REAL NOT NULL, manifest BLOB, image BLOB)""")

    @contextmanager
    def connect(self):
        connection = sqlite3.connect(self.database, timeout=5)
        connection.execute("PRAGMA synchronous=FULL")
        connection.row_factory = sqlite3.Row
        try:
            with connection:
                yield connection
        finally:
            connection.close()

    def render(self, owner: str, contract: dict, admission_digest: str,
               renderer: Callable[[str], tuple[bytes, str, str]], *,
               still_authorized: Callable[[], bool], now: float | None = None) -> dict:
        """One admitted execution, never retry after an uncertain dispatch.

        renderer returns exact PNG bytes, actual backend and retained private
        receipt digest. Hub must provide fresh authorization before dispatch AND
        readback. The admission digest identifies its consent/quota receipt.
        """
        require_sha(admission_digest)
        key, payload = capture(owner, contract)
        backend = getattr(renderer, "backend", "onemin")
        if backend not in ("onemin", "phygital") or contract.get("preferredProvider") not in (None, backend):
            raise ValueError("The requested scene provider is not available; no fallback is authorized.")
        request_digest = digest(encoded({"payload": payload, "backend": backend}))
        now = time.time() if now is None else now
        with self.connect() as connection:
            connection.execute("BEGIN IMMEDIATE")
            row = connection.execute("SELECT * FROM origin_scenes WHERE id=?", (key,)).fetchone()
            if row:
                if row["owner"] != owner or row["request_digest"] != request_digest:
                    raise ValueError("Existing scene is bound to a different request; it cannot be overwritten.")
                return self._project(row, owner, still_authorized, now)[0]
            if not still_authorized():
                raise PermissionError("Origin image consent/owner is no longer current.")
            if (connection.execute("SELECT count(*) FROM origin_scenes WHERE owner=?", (owner,)).fetchone()[0] >= 128
                    or connection.execute("SELECT count(*) FROM origin_scenes").fetchone()[0] >= 2048):
                raise ValueError("Private scene storage is full; no provider call was made.")
            # Fence committed BEFORE entering a potentially paid provider call.
            connection.execute("INSERT INTO origin_scenes VALUES (?, ?, ?, 'dispatching', ?, NULL, NULL)",
                               (key, owner, request_digest, now + 7 * 86400))
        try:
            if not still_authorized():
                raise PermissionError("Origin image consent/owner is no longer current.")
            data, provider, receipt_digest = renderer(payload["prompt"])
            width, height = inspect_png(data)
            require_sha(receipt_digest)
            if provider != backend:
                raise ValueError("Unexpected scene provider.")
            preferred = contract.get("preferredProvider")
            if preferred is not None and provider != preferred:
                raise ValueError("Provider did not match the admitted selection.")
            manifest = {
                "schema": MANIFEST_SCHEMA, "assetId": key, "ownerDigest": owner,
                **{k: payload[k] for k in ("workspaceId", "chapterId", "chapterDigest", "textDigest", "altText")},
                "contentType": "image/png", "contentLengthBytes": len(data), "contentHash": digest(data),
                "width": width, "height": height, "provider": provider,
                "providerReceiptDigest": receipt_digest, "admissionDigest": admission_digest,
                "publicationAuthorized": False,
            }
            with self.connect() as connection:
                connection.execute("UPDATE origin_scenes SET state='review', manifest=?, image=? WHERE id=? AND state='dispatching'",
                                   (encoded(manifest), data, key))
        except Exception:
            with self.connect() as connection:
                connection.execute("UPDATE origin_scenes SET state='uncertain' WHERE id=? AND state='dispatching'", (key,))
            raise
        # A revoked owner cannot receive private bytes, even after a successful render.
        return self.read(owner, key, still_authorized=still_authorized, now=now)[0]

    def read(self, owner: str, key: str, *, still_authorized: Callable[[], bool], now: float | None = None):
        require_sha(owner)
        require_sha(key)
        with self.connect() as connection:
            row = connection.execute("SELECT * FROM origin_scenes WHERE id=? AND owner=?", (key, owner)).fetchone()
        return self._project(row, owner, still_authorized, time.time() if now is None else now)

    @staticmethod
    def _project(row, owner, still_authorized, now):
        if not still_authorized():
            raise PermissionError("Private image access is no longer authorized.")
        if row is None or row["owner"] != owner:
            raise KeyError("Scene not found.")
        state = row["state"]
        if state != "persisted" and row["expires"] <= now and state != "rejected":
            state = "expired"
        status = {"assetId": row["id"], "state": state, "publicationAuthorized": False}
        if state not in ("review", "persisted"):
            return status, None
        data = row["image"]
        manifest = json.loads(row["manifest"])
        if (manifest["schema"] != MANIFEST_SCHEMA or manifest["ownerDigest"] != owner or manifest["assetId"] != row["id"]
                or identity(owner, manifest) != row["id"] or manifest["publicationAuthorized"] is not False
                or len(data) != manifest["contentLengthBytes"] or digest(data) != manifest["contentHash"]):
            raise ValueError("Retained image does not match its manifest.")
        if not still_authorized():
            raise PermissionError("Private image access is no longer authorized.")
        return {**status, "manifest": manifest}, data

    def decide(self, owner: str, key: str, expected_hash: str, *, approve: bool,
               explicitly_confirmed: bool, still_authorized: Callable[[], bool], now: float | None = None) -> dict:
        require_sha(owner)
        require_sha(key)
        require_sha(expected_hash)
        if explicitly_confirmed is not True or type(approve) is not bool:
            raise ValueError("Explicit image review is required.")
        now = time.time() if now is None else now
        with self.connect() as connection:
            connection.execute("BEGIN IMMEDIATE")
            row = connection.execute("SELECT * FROM origin_scenes WHERE id=? AND owner=?", (key, owner)).fetchone()
            status, data = self._project(row, owner, still_authorized, now)
            if status["state"] not in ("review", "persisted") or status["manifest"]["contentHash"] != expected_hash:
                raise ValueError("Reopen the exact image before deciding.")
            if not still_authorized():
                raise PermissionError("Private image access is no longer authorized.")
            state = "persisted" if approve else "rejected"
            connection.execute("UPDATE origin_scenes SET state=?, image=? WHERE id=?", (state, data if approve else None, key))
        return {"assetId": key, "state": state, "publicationAuthorized": False}

    def expire(self, now: float | None = None) -> int:
        """Remove expired private image bytes, keeping no-replay tombstones."""
        with self.connect() as connection:
            return connection.execute("UPDATE origin_scenes SET state='expired', image=NULL WHERE state IN ('review','uncertain','dispatching') AND expires<=?",
                                      (time.time() if now is None else now,)).rowcount


class OneMinSceneRenderer:
    """Use the existing Media Factory adapter and its quota manager, once.

    Configure that adapter's private state volume and credential source in the
    isolated worker, not in Hub or Android. Phygital remains unavailable until an
    equivalent verified adapter exists; it is never silently mapped to OneMinAI.
    """
    backend = "onemin"

    def __init__(self, private_work_directory: Path):
        if not private_work_directory.is_absolute() or not private_work_directory.is_dir():
            raise ValueError("An existing private worker directory is required.")
        self.directory = private_work_directory

    def __call__(self, prompt: str) -> tuple[bytes, str, str]:
        from render_guide_asset import render_asset, STATE_ROOT

        text(prompt, 4096)
        if (not os.environ.get("CHUMMER_MEDIA_FACTORY_STATE_DIR") or STATE_ROOT.resolve() != self.directory.resolve()
                or self.directory.stat().st_mode & 0o077):
            raise ValueError("Configure the dedicated private Media Factory scene volume before execution.")
        with tempfile.TemporaryDirectory(prefix="origin-scene-", dir=self.directory) as temporary:
            output = Path(temporary) / "scene.png"
            result = render_asset(prompt=prompt, output_path=output, width=1536, height=1024,
                                  single_dispatch=True)
            with output.open("rb") as handle:
                data = handle.read(MAX_IMAGE + 1)
            inspect_png(data)
            # The exact private receipt stays in the renderer-owned receipt
            # volume. Only its digest and actual backend cross to the manifest.
            with Path(result["receipt_path"]).open("rb") as handle:
                receipt = handle.read(MAX_PACKET + 1)
            if not 0 < len(receipt) <= MAX_PACKET or result.get("backend_provider") != "onemin":
                raise ValueError("Image receipt is invalid.")
            return data, "onemin", digest(receipt)
