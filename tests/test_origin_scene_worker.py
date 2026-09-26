import http.client
import io
import json
import os
from pathlib import Path
import socket
import sys
import tempfile
import threading
import unittest

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
        self.server = SceneWorker(self.path, self.token_file, self.store, self.renderer)
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


if __name__ == "__main__":
    unittest.main()
