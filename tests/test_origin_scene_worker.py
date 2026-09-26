import http.client
import io
import json
import os
from pathlib import Path
import socket
import subprocess
import sys
import tempfile
import threading
import time
import unittest
from contextlib import closing

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
from origin_scene_worker import SceneWorker, load_token
from origin_scene_store import OriginSceneStore, digest
from test_origin_scene_store import contract


class LocalConnection(http.client.HTTPConnection):
    def __init__(self, path):
        super().__init__("origin-scene-worker", timeout=5)
        self.path = path

    def connect(self):
        self.sock = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
        self.sock.settimeout(self.timeout)
        self.sock.connect(str(self.path))


class SceneWorkerTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        self.token = "synthetic-local-test-token-" + "x" * 32
        self.token_file = self.root / "token"
        self.token_file.write_text(self.token)
        self.token_file.chmod(0o600)
        self.store = OriginSceneStore(self.root / "media.sqlite")
        self.path = self.root / "worker.sock"
        output = io.BytesIO()
        Image.new("RGB", (24, 16), (180, 190, 200)).save(output, format="PNG")
        self.png = output.getvalue()
        self.calls = 0
        self.server = SceneWorker(self.path, self.token_file, self.store, self.renderer, dispatch_enabled=True)
        self.thread = threading.Thread(target=self.server.serve_forever)
        self.thread.start()
        self.addCleanup(self.cleanup)

    def cleanup(self):
        self.server.shutdown()
        self.server.server_close()
        self.thread.join(5)
        self.temporary.cleanup()

    def renderer(self, prompt):
        self.calls += 1
        return self.png, "onemin", "d" * 64

    def send(self, path, packet, token=None, headers=None):
        connection = LocalConnection(self.path)
        try:
            connection.request("POST", "/v1/" + path, json.dumps(packet), headers or {
                "Authorization": "Bearer " + (self.token if token is None else token), "Content-Type": "application/json"})
            response = connection.getresponse()
            return response.status, json.loads(response.read())
        finally:
            connection.close()

    def render(self):
        return self.send("render", {"ownerDigest": "a" * 64, "contract": contract(), "admissionDigest": "e" * 64})

    def test_actual_local_transport_render_read_adopt_and_erase(self):
        health_status, health = self.send("health", {})
        self.assertEqual(200, health_status)
        self.assertTrue(health["dispatchEnabled"])
        self.assertTrue(health["accountErasureSupported"])
        status, value = self.render()
        self.assertEqual(200, status)
        key = value["assetId"]
        status, value = self.send("read", {"ownerDigest": "a" * 64, "assetId": key})
        self.assertEqual(200, status)
        self.assertTrue(value["imageBase64"])
        self.assertEqual(200, self.send("decide", {"ownerDigest": "a" * 64, "assetId": key,
            "expectedHash": digest(self.png), "approve": True, "explicitlyConfirmed": True})[0])
        self.assertEqual("persisted", self.render()[1]["state"])
        self.assertEqual(1, self.calls)
        self.assertEqual(1, self.send("erase-owner", {"ownerDigest": "a" * 64})[1]["recordsRemoved"])
        self.assertEqual(403, self.send("read", {"ownerDigest": "a" * 64, "assetId": key})[0])
        self.assertEqual(403, self.render()[0])
        self.assertEqual(1, self.calls)

    def test_missing_wrong_duplicate_or_rotated_auth_never_reads_or_dispatches(self):
        self.assertEqual(401, self.send("health", {}, token="wrong")[0])
        self.assertEqual(401, self.send("health", {}, headers={"Content-Type": "application/json"})[0])
        connection = LocalConnection(self.path)
        try:
            connection.putrequest("POST", "/v1/health")
            connection.putheader("Authorization", "Bearer " + self.token)
            connection.putheader("Authorization", "Bearer " + self.token)
            connection.putheader("Content-Type", "application/json")
            connection.putheader("Content-Length", "2")
            connection.endheaders(b"{}")
            self.assertEqual(401, connection.getresponse().status)
        finally:
            connection.close()
        self.token_file.write_text("replacement-synthetic-token-" + "z" * 32)
        self.assertEqual(401, self.render()[0])
        self.assertEqual(0, self.calls)

    def test_snapshot_worker_health_disallows_new_paid_jobs(self):
        self.render()
        self.store.backup(self.root / "snapshot.sqlite")
        self.server.store = OriginSceneStore(self.root / "snapshot.sqlite")
        status, health = self.send("health", {})
        self.assertEqual(200, status)
        self.assertFalse(health["dispatchEnabled"])
        self.assertTrue(health["accountErasureSupported"])

    def test_disabled_dispatch_preserves_reads_and_erasure_without_new_record(self):
        key = self.render()[1]["assetId"]
        self.server.dispatch_enabled = False
        self.assertFalse(self.send("health", {})[1]["dispatchEnabled"])
        self.assertEqual(200, self.send("read", {"ownerDigest": "a" * 64, "assetId": key})[0])
        self.assertEqual(503, self.render()[0])
        self.assertEqual(1, self.calls)
        self.assertEqual(1, self.send("erase-owner", {"ownerDigest": "a" * 64})[1]["recordsRemoved"])

    def test_owner_confusion_unknown_fields_and_chunked_input_fail_closed(self):
        key = self.render()[1]["assetId"]
        self.assertEqual(404, self.send("read", {"ownerDigest": "f" * 64, "assetId": key})[0])
        self.assertEqual(400, self.send("erase-owner", {"ownerDigest": "a" * 64, "path": "/tmp"})[0])
        self.assertEqual(400, self.send("health", {}, headers={"Authorization": "Bearer " + self.token,
            "Content-Type": "application/json", "Transfer-Encoding": "chunked"})[0])

    def test_socket_and_token_are_private_and_existing_socket_is_never_removed(self):
        self.assertEqual(0, self.path.stat().st_mode & 0o077)
        with self.assertRaises(ValueError):
            SceneWorker(self.path, self.token_file, self.store, self.renderer)
        self.assertEqual(200, self.send("health", {})[0])
        self.token_file.chmod(0o644)
        with self.assertRaises(ValueError):
            load_token(self.token_file)

    def test_live_socket_without_worker_lock_is_not_replaced(self):
        path = self.root / "other.sock"
        with socket.socket(socket.AF_UNIX, socket.SOCK_STREAM) as other:
            other.bind(str(path))
            other.listen(1)
            identity = path.stat().st_ino
            with self.assertRaisesRegex(ValueError, "live socket"):
                SceneWorker(path, self.token_file, self.store, self.renderer)
            self.assertEqual(identity, path.stat().st_ino)

    def test_regular_file_or_symlink_is_never_reclaimed(self):
        path = self.root / "not-a-socket"
        path.write_text("preserve")
        with self.assertRaises(ValueError):
            SceneWorker(path, self.token_file, self.store, self.renderer)
        self.assertEqual("preserve", path.read_text())
        linked = self.root / "linked.sock"
        linked.symlink_to(path)
        with self.assertRaises(ValueError):
            SceneWorker(linked, self.token_file, self.store, self.renderer)
        self.assertTrue(linked.is_symlink())

    def test_repeated_close_leaves_replacement_socket_owned_by_new_worker(self):
        self.server.shutdown()
        self.server.server_close()
        with SceneWorker(self.path, self.token_file, self.store, self.renderer) as replacement:
            identity = self.path.stat().st_ino
            self.server.server_close()
            self.assertEqual(identity, self.path.stat().st_ino)
            with self.assertRaises(ValueError):
                SceneWorker(self.path, self.token_file, self.store, self.renderer)


class SceneWorkerProcessTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        self.addCleanup(self.temporary.cleanup)
        self.token = "synthetic-process-test-token-" + "y" * 32
        self.token_file = self.root / "token"
        self.token_file.write_text(self.token)
        self.token_file.chmod(0o600)
        self.path = self.root / "worker.sock"
        self.database = self.root / "media.sqlite"
        self.command = [sys.executable, str(Path(__file__).resolve().parents[1] / "scripts/origin_scene_worker.py"),
            "--socket", str(self.path), "--token-file", str(self.token_file),
            "--database", str(self.database), "--private-render-directory", str(self.root)]

    def start_worker(self):
        process = subprocess.Popen(self.command, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        def cleanup():
            if process.poll() is None:
                process.kill()
            process.wait(timeout=5)
        self.addCleanup(cleanup)
        deadline = time.monotonic() + 5
        while time.monotonic() < deadline:
            self.assertIsNone(process.poll(), "Worker exited before becoming ready.")
            try:
                if self.request("health", {})[0] == 200:
                    return process
            except OSError:
                pass
            time.sleep(0.02)
        self.fail("Worker did not become ready.")

    def request(self, route, packet):
        with closing(LocalConnection(self.path)) as connection:
            connection.request("POST", "/v1/" + route, json.dumps(packet),
                {"Authorization": "Bearer " + self.token, "Content-Type": "application/json"})
            response = connection.getresponse()
            return response.status, json.loads(response.read())

    def test_sigterm_cleans_socket_and_cold_restart_preserves_erasure(self):
        process = self.start_worker()
        self.assertFalse(self.request("health", {})[1]["dispatchEnabled"])
        self.assertEqual(200, self.request("erase-owner", {"ownerDigest": "a" * 64})[0])
        process.terminate()
        self.assertEqual(0, process.wait(timeout=5))
        self.assertFalse(self.path.exists())
        replacement = self.start_worker()
        self.assertEqual(403, self.request("read", {"ownerDigest": "a" * 64, "assetId": "b" * 64})[0])
        replacement.terminate()
        self.assertEqual(0, replacement.wait(timeout=5))

    def test_crash_socket_recovered_but_live_process_never_displaced(self):
        process = self.start_worker()
        self.assertEqual(200, self.request("erase-owner", {"ownerDigest": "a" * 64})[0])
        identity = self.path.stat().st_ino
        duplicate = subprocess.run(self.command, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=5)
        self.assertNotEqual(0, duplicate.returncode)
        self.assertEqual(identity, self.path.stat().st_ino)
        self.assertEqual(200, self.request("health", {})[0])
        process.kill()
        process.wait(timeout=5)
        self.assertTrue(self.path.exists())
        replacement = self.start_worker()
        self.assertEqual(403, self.request("read", {"ownerDigest": "a" * 64, "assetId": "b" * 64})[0])
        replacement.terminate()
        self.assertEqual(0, replacement.wait(timeout=5))


if __name__ == "__main__":
    unittest.main()
