from __future__ import annotations

import importlib.util
import io
import json
from pathlib import Path
import tempfile
import unittest
from concurrent.futures import ThreadPoolExecutor
from threading import Event

from PIL import Image


SPEC = importlib.util.spec_from_file_location("origin_scene_store", Path(__file__).resolve().parents[1] / "scripts/origin_scene_store.py")
scene = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(scene)


def contract(owner="a" * 64, text_digest="b" * 64):
    payload = {"schema": scene.SCHEMA, "workspaceId": "private-workspace", "chapterId": "childhood",
               "chapterDigest": "c" * 64, "textDigest": text_digest,
               "prompt": "A readable daylight forest scene; no words or new story events.", "altText": "Forest after rain"}
    key = scene.identity(owner, payload)
    return {"contractName": "chummer6-hub.horizon_governed_render_request.v1", "contractVersion": "2026-06-30",
            "orchestrationLane": "ea_governed_render", "horizonId": "origin-dossier", "capabilityId": "origin-dossier-media",
            "artifactKind": "dossier_media", "capabilitySlot": "approved_origin_media", "requestedBy": "origin-owner:" + owner,
            "audience": "private", "preferredProvider": "onemin", "workItemId": key, "sourceRef": "origin-dossier:scene:" + key,
            "truthRefs": ["origin-dossier:scene:" + key], "evidenceRefs": ["origin-text:" + text_digest],
            "artifacts": [{"artifactId": key, "role": "chapter_scene", "category": "origin/chapter-scene",
                           "payload": json.dumps(payload), "outputFormat": "png", "deduplicationKey": key,
                           "maxBytes": scene.MAX_IMAGE, "requiresApproval": True, "persistOnApproval": True, "allowPersistentPinning": False}]}


class OriginSceneTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.database = Path(self.temporary.name) / "private.sqlite"
        self.store = scene.OriginSceneStore(self.database)
        self.owner = "a" * 64
        self.request = contract()
        self.calls = 0
        self.now = 1000
        self.authorized = True
        output = io.BytesIO()
        Image.new("RGB", (24, 16), (150, 180, 150)).save(output, format="PNG")
        self.png = output.getvalue()

    def current(self):
        return self.authorized

    def provider(self, prompt):
        self.calls += 1
        self.assertIn("daylight", prompt)
        return self.png, "onemin", "d" * 64

    def render(self, provider=None):
        return self.store.render(self.owner, self.request, "e" * 64, provider or self.provider,
                                 still_authorized=self.current, now=self.now)

    def test_render_reopen_review_and_persist_exact_bytes_without_replay(self):
        result = self.render()
        self.assertEqual("review", result["state"])
        self.assertFalse(result["publicationAuthorized"])
        self.store = scene.OriginSceneStore(self.database)
        self.assertEqual(result, self.render())
        self.assertEqual(1, self.calls)
        status, image = self.store.read(self.owner, result["assetId"], still_authorized=self.current, now=self.now)
        self.assertEqual(self.png, image)
        self.assertNotIn("url", json.dumps(status).lower())
        self.assertNotIn("prompt", json.dumps(status).lower())
        self.store.decide(self.owner, result["assetId"], scene.digest(self.png), approve=True,
                          explicitly_confirmed=True, still_authorized=self.current, now=self.now)
        self.assertEqual(0, self.store.expire(self.now + 8 * 86400))
        self.assertEqual("persisted", self.store.read(self.owner, result["assetId"], still_authorized=self.current, now=self.now + 8 * 86400)[0]["state"])

    def test_wrong_owner_cannot_read_or_admit(self):
        key = self.render()["assetId"]
        with self.assertRaises(KeyError):
            self.store.read("f" * 64, key, still_authorized=lambda: True, now=self.now)
        self.owner = "f" * 64
        with self.assertRaises(ValueError):
            self.render()
        self.assertEqual(1, self.calls)

    def test_revoke_during_render_retains_output_but_returns_no_private_image(self):
        def renderer(prompt):
            result = self.provider(prompt)
            self.authorized = False
            return result
        with self.assertRaises(PermissionError):
            self.render(renderer)
        self.authorized = True
        self.assertEqual("review", self.render()["state"])
        self.assertEqual(1, self.calls)

    def test_no_consent_never_dispatches(self):
        self.authorized = False
        with self.assertRaises(PermissionError):
            self.render()
        self.assertEqual(0, self.calls)

    def test_lifetime_limit_survives_restart_expiry_and_erasure(self):
        self.store = scene.OriginSceneStore(self.database, dispatch_limit=1)
        self.render()
        self.store.expire(now=self.now + 8 * 86400)
        self.store.erase_owner(self.owner)
        self.store = scene.OriginSceneStore(self.database, dispatch_limit=1)
        self.owner = "f" * 64
        self.request = contract(owner=self.owner)
        with self.assertRaisesRegex(PermissionError, "allowance is exhausted"):
            self.render()
        self.assertEqual(1, self.calls)

    def test_lifetime_limit_counts_uncertain_attempts_and_is_atomic(self):
        self.store = scene.OriginSceneStore(self.database, dispatch_limit=1)
        entered, release = Event(), Event()
        def renderer(prompt):
            self.provider(prompt)
            entered.set()
            release.wait(5)
            raise TimeoutError()
        with ThreadPoolExecutor(max_workers=1) as executor:
            first = executor.submit(self.render, renderer)
            try:
                self.assertTrue(entered.wait(5))
                with self.assertRaisesRegex(PermissionError, "allowance is exhausted"):
                    self.store.render(self.owner, contract(text_digest="f" * 64), "e" * 64,
                                      self.provider, still_authorized=self.current, now=self.now)
            finally:
                release.set()
            with self.assertRaises(TimeoutError):
                first.result(timeout=5)
        self.store = scene.OriginSceneStore(self.database, dispatch_limit=1)
        self.assertEqual("uncertain", self.render()["state"])
        self.assertEqual(1, self.calls)

    def test_live_renderer_receives_exact_identity_after_committed_fence(self):
        self.store = scene.OriginSceneStore(self.database, dispatch_limit=1)
        renderer = scene.OneMinSceneRenderer(self.database.parent)
        def admitted(prompt, key, admission):
            with self.store.connect() as connection:
                row = connection.execute("SELECT state FROM origin_scenes WHERE id=?", (key,)).fetchone()
                self.assertEqual("dispatching", row[0])
            self.assertEqual("e" * 64, admission)
            return self.provider(prompt)
        renderer.render_admitted = admitted
        self.assertEqual("review", self.render(renderer)["state"])
        with self.assertRaises(PermissionError):
            renderer("no durable admission")

    def test_unavailable_phygital_never_silently_spends_onemin_credits(self):
        self.request["preferredProvider"] = "phygital"
        with self.assertRaises(ValueError):
            self.render()
        self.assertEqual(0, self.calls)

    def test_timeout_and_process_death_cannot_replay_paid_request(self):
        for error, expected in ((TimeoutError(), "uncertain"), (SystemExit(), "dispatching")):
            with self.subTest(expected=expected):
                self.request = contract(text_digest=scene.digest(expected.encode()))
                def renderer(prompt):
                    self.provider(prompt)
                    raise error
                with self.assertRaises(type(error)):
                    self.render(renderer)
                self.store = scene.OriginSceneStore(self.database)
                before = self.calls
                self.assertEqual(expected, self.render()["state"])
                self.assertEqual(before, self.calls)

    def test_concurrent_requests_share_one_committed_dispatch(self):
        entered, release = Event(), Event()
        def renderer(prompt):
            value = self.provider(prompt)
            entered.set()
            if not release.wait(5):
                raise TimeoutError()
            return value
        with ThreadPoolExecutor(max_workers=1) as executor:
            pending = executor.submit(self.render, renderer)
            try:
                self.assertTrue(entered.wait(5))
                self.assertEqual("dispatching", self.render()["state"])
                self.assertEqual(1, self.calls)
            finally:
                release.set()
            self.assertEqual("review", pending.result(timeout=5)["state"])

    def test_changed_prompt_conflicts_and_changed_text_has_new_identity(self):
        old = self.render()["assetId"]
        payload = json.loads(self.request["artifacts"][0]["payload"])
        payload["prompt"] += " Another scene."
        self.request["artifacts"][0]["payload"] = json.dumps(payload)
        with self.assertRaises(ValueError):
            self.render()
        self.assertEqual(1, self.calls)
        self.request = contract(text_digest="f" * 64)
        self.assertNotEqual(old, self.render()["assetId"])

    def test_corrupt_output_or_mismatched_provider_never_becomes_reviewable(self):
        for data, backend in ((b"not PNG", "onemin"), (self.png, "wrong")):
            self.request = contract(text_digest=scene.digest(data + backend.encode()))
            with self.assertRaises((ValueError, OSError)):
                self.render(lambda prompt: (data, backend, "d" * 64))
            self.assertEqual("uncertain", self.render()["state"])

    def test_approval_requires_exact_image_and_explicit_review(self):
        key = self.render()["assetId"]
        for expected_hash, explicit in (("f" * 64, True), (scene.digest(self.png), False)):
            with self.assertRaises(ValueError):
                self.store.decide(self.owner, key, expected_hash, approve=True, explicitly_confirmed=explicit,
                                  still_authorized=self.current, now=self.now)
        self.store.decide(self.owner, key, scene.digest(self.png), approve=False, explicitly_confirmed=True,
                          still_authorized=self.current, now=self.now)
        self.assertEqual(("rejected", None), (self.render()["state"], self.store.read(self.owner, key, still_authorized=self.current, now=self.now)[1]))
        self.assertEqual(1, self.calls)

    def test_expiry_removes_bytes_without_allowing_regeneration(self):
        key = self.render()["assetId"]
        self.now += 8 * 86400
        self.assertEqual(("expired", None), (self.render()["state"], self.store.read(self.owner, key, still_authorized=self.current, now=self.now)[1]))
        self.assertEqual(1, self.store.expire(self.now))
        self.assertEqual("expired", self.render()["state"])
        self.assertEqual(1, self.calls)

    def test_retained_bytes_are_verified_on_read(self):
        key = self.render()["assetId"]
        with self.store.connect() as connection:
            connection.execute("UPDATE origin_scenes SET image=? WHERE id=?", (b"tampered", key))
        with self.assertRaises(ValueError):
            self.store.read(self.owner, key, still_authorized=self.current, now=self.now)

    def test_contract_rejects_public_wrong_policy_and_duplicate_payload(self):
        for mutation in (lambda c: c.update(audience="public"),
                         lambda c: c.update(evidenceRefs=[]),
                         lambda c: c["artifacts"][0].update(maxBytes=0),
                         lambda c: c["artifacts"][0].update(payload='{"schema":1,"schema":2}')):
            self.request = contract()
            mutation(self.request)
            with self.assertRaises(ValueError):
                self.render()
        self.assertEqual(0, self.calls)

    def test_storage_pressure_fails_before_credit_spend(self):
        with self.store.connect() as connection:
            connection.executemany("INSERT INTO origin_scenes VALUES (?, ?, 'test', 'rejected', 0, NULL, NULL)",
                                   [(str(i), self.owner) for i in range(128)])
        with self.assertRaises(ValueError):
            self.render()
        self.assertEqual(0, self.calls)

    def test_account_erasure_survives_restart_and_fences_new_chapters(self):
        key = self.render()["assetId"]
        self.assertEqual(1, self.store.erase_owner(self.owner))
        self.assertEqual(0, self.store.erase_owner(self.owner))
        self.store = scene.OriginSceneStore(self.database)
        with self.assertRaises(PermissionError):
            self.store.read(self.owner, key, still_authorized=self.current, now=self.now)
        self.request = contract(text_digest="f" * 64)
        with self.assertRaises(PermissionError):
            self.render()
        with self.store.connect() as connection:
            row = connection.execute("SELECT state, manifest, image, request_digest FROM origin_scenes").fetchone()
        self.assertEqual(("erased", None, None, ""), tuple(row))
        self.assertEqual(1, self.calls)

    def test_account_erasure_during_paid_render_cannot_resurrect_private_bytes(self):
        def renderer(prompt):
            result = self.provider(prompt)
            self.assertEqual(1, self.store.erase_owner(self.owner))
            return result
        with self.assertRaises(PermissionError):
            self.render(renderer)
        with self.store.connect() as connection:
            self.assertEqual(("erased", None, None), tuple(connection.execute("SELECT state, image, manifest FROM origin_scenes").fetchone()))
        self.assertEqual(1, self.calls)

    def test_private_online_backup_restores_bytes_but_never_replays_new_paid_jobs(self):
        key = self.render()["assetId"]
        destination = self.database.parent / "recovery.sqlite"
        result = self.store.backup(destination)
        self.assertFalse(result["dispatchEnabled"])
        self.assertEqual(scene.digest(destination.read_bytes()), result["sha256"])
        self.assertEqual(0, destination.stat().st_mode & 0o077)
        restored = scene.OriginSceneStore(destination)
        self.assertEqual(self.png, restored.read(self.owner, key, still_authorized=self.current, now=self.now)[1])
        with self.assertRaises(PermissionError):
            restored.render(self.owner, contract(text_digest="f" * 64), "e" * 64, self.provider,
                            still_authorized=self.current, now=self.now)
        with self.assertRaises(FileExistsError):
            self.store.backup(destination)
        self.assertEqual(1, self.calls)

    def test_backup_keeps_erasure_and_inflight_fences(self):
        first = self.render()["assetId"]
        self.store.erase_owner(self.owner)
        self.owner = "f" * 64
        self.request = contract(owner=self.owner)
        def crash(prompt):
            self.provider(prompt)
            raise SystemExit()
        with self.assertRaises(SystemExit):
            self.render(crash)
        destination = self.database.parent / "recovery.sqlite"
        self.store.backup(destination)
        self.store = scene.OriginSceneStore(destination)
        with self.assertRaises(PermissionError):
            self.store.read("a" * 64, first, still_authorized=self.current, now=self.now)
        self.assertEqual("dispatching", self.render()["state"])
        self.assertEqual(2, self.calls)


if __name__ == "__main__":
    unittest.main()
