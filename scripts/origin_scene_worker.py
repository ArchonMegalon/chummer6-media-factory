"""Local Docker-only Origin media transport over a private Unix-domain socket.

Hub authenticates users/installs and admits consent/quota. This worker authenticates
Hub with a dedicated file-mounted token; neither public TCP nor provider credentials
are exposed. It owns rendering, retained bytes and account-media deletion.
"""
from __future__ import annotations

import argparse
import base64
import errno
import fcntl
import hmac
import json
import os
from pathlib import Path
import socket
import socketserver
import signal
import stat
import threading
from http.server import BaseHTTPRequestHandler, HTTPServer

from origin_scene_store import MAX_PACKET, OriginSceneStore, OneMinSceneRenderer, encoded, unique_object


def load_token(path: Path) -> str:
    if (not path.is_absolute() or path.is_symlink() or not path.is_file()
            or path.stat().st_mode & 0o077):
        raise ValueError("A dedicated private token file is required.")
    with path.open("rb") as handle:
        value = handle.read(257)
    if not 32 <= len(value) <= 256 or any(byte < 33 or byte > 126 for byte in value):
        raise ValueError("The worker token must contain 32–256 printable ASCII bytes, without whitespace.")
    return value.decode("ascii")


class SceneWorker(socketserver.ThreadingMixIn, HTTPServer):
    address_family = socket.AF_UNIX
    daemon_threads = False
    request_queue_size = 8

    def __init__(self, socket_path: Path, token_file: Path, store: OriginSceneStore, renderer, *, dispatch_enabled=False):
        if (not socket_path.is_absolute() or not socket_path.parent.is_dir()
                or socket_path.parent.stat().st_mode & 0o077 or socket_path.is_symlink()):
            raise ValueError("A socket in a private shared volume is required.")
        self.token_file = token_file
        load_token(token_file)
        self.store, self.renderer = store, renderer
        self.dispatch_enabled = dispatch_enabled
        self.socket_path = socket_path
        self.socket_identity = None
        self.lifecycle_lock = None
        self.slots = threading.BoundedSemaphore(8)
        self.render_slot = threading.BoundedSemaphore(1)
        try:
            self.lifecycle_lock = os.open(str(socket_path) + ".lock",
                os.O_CREAT | os.O_RDWR | os.O_NOFOLLOW | os.O_CLOEXEC, 0o600)
            info = os.fstat(self.lifecycle_lock)
            if not stat.S_ISREG(info.st_mode) or info.st_mode & 0o077 or info.st_uid != os.geteuid():
                raise ValueError("A private worker lifecycle lock is required.")
            try:
                fcntl.flock(self.lifecycle_lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
            except BlockingIOError as error:
                raise ValueError("The private worker is already running.") from error
            self.remove_stale_socket()
            super().__init__(str(socket_path), SceneHandler)
        except BaseException:
            self.release_lifecycle_lock()
            raise

    def remove_stale_socket(self):
        # The lock is never unlinked: replacing it would create two owners.
        # Only a refused, same-inode socket left by a dead process is reclaimed.
        try:
            previous = self.socket_path.lstat()
        except FileNotFoundError:
            return
        if not stat.S_ISSOCK(previous.st_mode) or previous.st_uid != os.geteuid():
            raise ValueError("Refusing to replace a non-owned socket path.")
        with socket.socket(socket.AF_UNIX, socket.SOCK_STREAM) as probe:
            probe.settimeout(1)
            try:
                probe.connect(str(self.socket_path))
            except OSError as error:
                if error.errno != errno.ECONNREFUSED:
                    raise ValueError("Socket ownership is uncertain.") from error
            else:
                raise ValueError("Refusing to unlink a live socket.")
        current = self.socket_path.lstat()
        if (current.st_dev, current.st_ino) != (previous.st_dev, previous.st_ino):
            raise ValueError("Socket identity changed during recovery.")
        self.socket_path.unlink()

    def release_lifecycle_lock(self):
        if self.lifecycle_lock is not None:
            os.close(self.lifecycle_lock)
            self.lifecycle_lock = None

    def server_bind(self):
        socketserver.TCPServer.server_bind(self)
        info = self.socket_path.lstat()
        self.socket_identity = (info.st_dev, info.st_ino)
        os.chmod(self.socket_path, 0o600)
        self.server_name, self.server_port = "origin-scene-worker", 0

    def process_request(self, request, client_address):
        if not self.slots.acquire(blocking=False):
            self.shutdown_request(request)
            return
        try:
            super().process_request(request, client_address)
        except BaseException:
            self.slots.release()
            raise

    def process_request_thread(self, request, client_address):
        try:
            super().process_request_thread(request, client_address)
        finally:
            self.slots.release()

    def handle_error(self, request, client_address):
        pass  # Never log private request bodies, provider failures or credentials.

    def server_close(self):
        try:
            super().server_close()  # Drain admitted operations before releasing ownership.
            try:
                current = self.socket_path.lstat()
            except FileNotFoundError:
                return
            if stat.S_ISSOCK(current.st_mode) and (current.st_dev, current.st_ino) == self.socket_identity:
                self.socket_path.unlink()
        finally:
            self.release_lifecycle_lock()


class SceneHandler(BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.0"
    server_version = "OriginMedia"

    def setup(self):
        self.request.settimeout(10)
        super().setup()

    def log_message(self, format, *args):
        pass

    def respond(self, status, value):
        body = encoded(value)
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.send_header("Connection", "close")
        self.end_headers()
        self.wfile.write(body)
        self.close_connection = True

    def do_POST(self):
        try:
            expected = "Bearer " + load_token(self.server.token_file)
            supplied = self.headers.get_all("Authorization", [])
            if (len(supplied) != 1 or not supplied[0].isascii()
                    or not hmac.compare_digest(supplied[0], expected)):
                self.respond(401, {"error": "unauthorized"})
                return
            lengths = self.headers.get_all("Content-Length", [])
            if (len(lengths) != 1 or not lengths[0].isdigit() or self.headers.get_all("Transfer-Encoding")
                    or self.headers.get("Content-Type") != "application/json"):
                self.respond(400, {"error": "invalid_framing"})
                return
            length = int(lengths[0])
            if not 0 < length <= MAX_PACKET:
                self.respond(413, {"error": "request_too_large"})
                return
            raw = self.rfile.read(length)
            if len(raw) != length:
                raise ValueError("Incomplete request.")
            payload = json.loads(raw, object_pairs_hook=unique_object)
            if not isinstance(payload, dict):
                raise ValueError("Object required.")
            result = self.execute(payload)
            self.respond(200, result)
        except PermissionError:
            self.respond(403, {"error": "access_revoked"})
        except KeyError:
            self.respond(404, {"error": "not_found"})
        except (ValueError, TypeError, json.JSONDecodeError):
            self.respond(400, {"error": "invalid_request_or_state"})
        except Exception:
            self.respond(503, {"error": "unavailable_check_existing_request"})

    def execute(self, packet):
        store = self.server.store
        if self.path == "/v1/health" and not packet:
            with store.connect() as connection:
                connection.execute("SELECT count(*) FROM origin_erased_owners").fetchone()
                recovery = connection.execute("SELECT recovery_only FROM origin_store_mode WHERE id=1").fetchone()[0]
            return {"schema": "chummer.media.origin-scene-worker/v1", "accountErasureSupported": True,
                    "dispatchEnabled": self.server.dispatch_enabled and not bool(recovery)}
        fields = {
            "/v1/read": {"ownerDigest", "assetId"},
            "/v1/decide": {"ownerDigest", "assetId", "expectedHash", "approve", "explicitlyConfirmed"},
            "/v1/erase-owner": {"ownerDigest"},
            "/v1/render": {"ownerDigest", "contract", "admissionDigest"},
        }
        if self.path not in fields or set(packet) != fields[self.path]:
            raise ValueError("Unknown operation or fields.")
        owner = packet["ownerDigest"]
        # This token authorizes Hub-to-worker orchestration, never a phone. Hub
        # revalidates the user's current install before sending and after I/O.
        if self.path == "/v1/read":
            status, data = store.read(owner, packet["assetId"], still_authorized=lambda: True)
            return {**status, "imageBase64": base64.b64encode(data).decode("ascii") if data else None}
        if self.path == "/v1/decide":
            return store.decide(owner, packet["assetId"], packet["expectedHash"], approve=packet["approve"],
                                explicitly_confirmed=packet["explicitlyConfirmed"], still_authorized=lambda: True)
        if self.path == "/v1/erase-owner":
            return {"ownerDigest": owner, "recordsRemoved": store.erase_owner(owner)}
        if not self.server.dispatch_enabled:
            raise OSError("Provider dispatch is not enabled on this worker.")
        if not self.server.render_slot.acquire(blocking=False):
            raise OSError("Renderer occupied; read the existing request before trying again.")
        try:
            return store.render(owner, packet["contract"], packet["admissionDigest"], self.server.renderer,
                                still_authorized=lambda: True)
        finally:
            self.server.render_slot.release()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--socket", type=Path, required=True)
    parser.add_argument("--token-file", type=Path, required=True)
    parser.add_argument("--database", type=Path, required=True)
    parser.add_argument("--private-render-directory", type=Path, required=True)
    parser.add_argument("--enable-dispatch", action="store_true",
                        help="Enable explicit admitted renders only after the private provider/quota route is configured.")
    args = parser.parse_args()
    with SceneWorker(args.socket, args.token_file, OriginSceneStore(args.database),
                     OneMinSceneRenderer(args.private_render_directory), dispatch_enabled=args.enable_dispatch) as server:
        stopped = threading.Event()
        previous = {signum: signal.signal(signum, lambda *_: stopped.set())
                    for signum in (signal.SIGTERM, signal.SIGINT)}
        listener = threading.Thread(target=server.serve_forever, name="origin-scene-listener")
        try:
            listener.start()
            stopped.wait()
        finally:
            server.shutdown()
            listener.join()
            # The context manager drains request threads with handlers still
            # installed. A second TERM must not interrupt a durable commit.
            server.server_close()
            for signum, handler in previous.items():
                signal.signal(signum, handler)


if __name__ == "__main__":
    main()
