import importlib.util
import io
import json
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import Mock, patch
import urllib.error
from PIL import Image

from test_render_guide_asset_download_guard import FakeResponse, load_render_module


class OriginSceneRendererTests(unittest.TestCase):
    def setUp(self):
        self.module = load_render_module()
        self.write_receipt = self.module._write_receipt
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.output = Path(self.temp.name) / "scene.png"
        self.env = patch.dict(os.environ, {"SCENE_TEST_KEY": "synthetic-key-not-a-credential"}, clear=True)
        self.env.start()
        self.addCleanup(self.env.stop)
        for name, value in {
            "_seed_runtime_env": None,
            "_write_attempt_status": None, "_record_health_attempt": None,
            "_release_onemin_image_slot": None, "_release_onemin_image_slot_locally": None,
            "_reserve_onemin_image_slot": {"lease_id": "test-lease", "secret_env_name": "SCENE_TEST_KEY"},
            "_model_candidates": ["gpt-image-1-mini", "unused-model"],
            "_size_candidates": ["1536x1024", "unused-size"],
            "_write_receipt": Path(self.temp.name) / "receipt.json",
        }.items():
            setattr(self.module, name, Mock(return_value=value))
        self.module._configured_onemin_slots = Mock(side_effect=AssertionError("No fallback accounts allowed"))
        self.open = Mock()
        opener_patch = patch.object(self.module.urllib.request, "build_opener", return_value=Mock(open=self.open))
        opener_patch.start()
        self.addCleanup(opener_patch.stop)

    def render(self, **kwargs):
        return self.module.render_asset(prompt="All exact chapter facts. " * 100, output_path=self.output,
                                        width=1536, height=1024, single_dispatch=True, **kwargs)

    def test_one_post_even_when_models_sizes_and_accounts_offer_fallback(self):
        for error in (TimeoutError(), urllib.error.URLError("ambiguous transport"),
                      urllib.error.HTTPError("https://api.1min.ai/api/features", 429, "quota", {}, None)):
            self.open.reset_mock()
            self.open.side_effect = error
            with self.assertRaises(RuntimeError):
                self.render()
            self.assertEqual(1, self.open.call_count)
        self.module._configured_onemin_slots.assert_not_called()

    def test_exact_prompt_and_one_output_requested(self):
        self.open.return_value = FakeResponse(b"png-received-by-adapter", {"Content-Type": "image/png"})
        result = self.render()
        self.assertEqual("onemin", result["backend_provider"])
        payload = json.loads(self.open.call_args.args[0].data)
        self.assertEqual("All exact chapter facts. " * 100, payload["promptObject"]["prompt"])
        self.assertEqual(1, payload["promptObject"]["n"])
        self.assertEqual(1, self.open.call_count)

    def test_missing_manager_does_not_fall_back_to_ephemeral_quota_or_spend(self):
        self.module._reserve_onemin_image_slot.return_value = None
        self.module._reserve_onemin_image_slot_locally = Mock(
            side_effect=AssertionError("Private scenes cannot use per-call memory quota"))
        with self.assertRaisesRegex(RuntimeError, "onemin_manager_capacity_unavailable"):
            self.render()
        self.module._reserve_onemin_image_slot_locally.assert_not_called()
        self.open.assert_not_called()
        self.module._write_receipt.assert_not_called()
        self.assertFalse(self.output.exists())

    def test_image_success_does_not_report_an_estimate_as_observed_provider_spend(self):
        self.open.return_value = FakeResponse(b"png-received-by-adapter", {"Content-Type": "image/png"})
        self.render()
        self.module._release_onemin_image_slot.assert_called_once()
        self.assertIsNone(self.module._release_onemin_image_slot.call_args.kwargs["actual_credits_delta"])

    def admission(self):
        key = Path(self.temp.name) / "onemin.key"
        key.write_text("scoped-synthetic-key-not-a-credential")
        key.chmod(0o600)
        return self.module.OriginSceneAdmission("a" * 64, "b" * 64, key)

    def test_hub_admitted_scene_uses_one_scoped_key_and_not_the_ea_pool(self):
        self.open.return_value = FakeResponse(b"png", {"Content-Type": "image/png"})
        admission = self.admission()
        result = self.render(origin_admission=admission)
        self.assertEqual("hub_admission_and_media_lifetime_batch", result["manager_reservation_source"])
        self.module._seed_runtime_env.assert_not_called()
        self.module._reserve_onemin_image_slot.assert_not_called()
        self.module._release_onemin_image_slot.assert_not_called()
        self.module._configured_onemin_slots.assert_not_called()
        self.assertEqual(1, self.open.call_count)
        request = self.open.call_args.args[0]
        self.assertEqual(admission.key_file.read_text(), request.get_header("Api-key"))
        payload = json.loads(request.data)
        self.assertEqual("gpt-image-1-mini", payload["model"])
        self.assertEqual("low", payload["promptObject"]["quality"])
        self.assertEqual(1, payload["promptObject"]["n"])
        self.assertEqual("1536x1024", payload["promptObject"]["size"])
        receipt = self.module._write_receipt.call_args.kwargs["result_json"]["receipt_json"]
        self.assertEqual("a" * 64, receipt["scene_id"])
        self.assertEqual("b" * 64, receipt["hub_admission_digest"])
        self.assertIsNone(receipt["actual_credits_delta"])
        self.assertNotIn(admission.key_file.read_text(), json.dumps(receipt))

    def test_admitted_scene_timeout_never_releases_refills_or_retries(self):
        self.open.side_effect = TimeoutError()
        with self.assertRaises(RuntimeError):
            self.render(origin_admission=self.admission())
        self.assertEqual(1, self.open.call_count)
        self.module._reserve_onemin_image_slot.assert_not_called()
        self.module._release_onemin_image_slot.assert_not_called()

    def reference(self):
        output = io.BytesIO()
        Image.new("RGB", (24, 16), (150, 180, 150)).save(output, format="PNG")
        return output.getvalue()

    def test_character_reference_is_uploaded_privately_and_used_in_one_edit(self):
        reference = self.reference()
        key = "images/synthetic_character.png"
        self.open.side_effect = [
            FakeResponse(json.dumps({"asset": {"key": key, "acl": "private", "mimetype": "image/png", "size": len(reference)},
                                     "fileContent": {"path": key}}).encode(), {"Content-Type": "application/json"}),
            FakeResponse(b"rendered PNG", {"Content-Type": "image/png"})]
        self.render(origin_admission=self.admission(), origin_reference_png=reference)
        upload, edit = [call.args[0] for call in self.open.call_args_list]
        self.assertEqual("https://api.1min.ai/api/assets", upload.full_url)
        self.assertIn(reference, upload.data)
        payload = json.loads(edit.data)
        self.assertEqual("IMAGE_EDITOR", payload["type"])
        self.assertEqual(key, payload["promptObject"]["imageUrl"])
        self.assertEqual(1, payload["promptObject"]["n"])
        receipt = self.module._write_receipt.call_args.kwargs["result_json"]["receipt_json"]
        self.assertEqual("IMAGE_EDITOR", receipt["feature_type"])
        self.assertEqual(self.module.hashlib.sha256(reference).hexdigest(), receipt["reference_image_sha256"])
        self.assertNotIn(key, json.dumps(receipt))

    def test_reference_upload_failure_never_generates_a_different_person_or_retries(self):
        for reply in (TimeoutError(), FakeResponse(json.dumps({"asset": {"acl": "public-read"}}).encode(), {})):
            self.open.reset_mock()
            self.open.side_effect = reply if isinstance(reply, Exception) else None
            self.open.return_value = reply
            with self.assertRaises((RuntimeError, TimeoutError)):
                self.render(origin_admission=self.admission(), origin_reference_png=self.reference())
            self.assertEqual(1, self.open.call_count)
            self.assertEqual("https://api.1min.ai/api/assets", self.open.call_args.args[0].full_url)

    def test_reference_requires_an_admission_and_valid_image_before_network(self):
        with self.assertRaises(ValueError):
            self.render(origin_reference_png=self.reference())
        with self.assertRaises((ValueError, OSError)):
            self.render(origin_admission=self.admission(), origin_reference_png=b"not a PNG")
        self.open.assert_not_called()

    def test_scoped_key_rejects_links_permissions_and_oversized_material(self):
        admission = self.admission()
        admission.key_file.chmod(0o644)
        with self.assertRaises(ValueError):
            self.render(origin_admission=admission)
        admission.key_file.chmod(0o600)
        admission.key_file.write_text("x" * 513)
        with self.assertRaises(ValueError):
            self.render(origin_admission=admission)
        link = admission.key_file.parent / "linked.key"
        link.symlink_to(admission.key_file)
        with self.assertRaises(OSError):
            self.render(origin_admission=self.module.OriginSceneAdmission("a" * 64, "b" * 64, link))
        self.open.assert_not_called()

    def test_admission_cannot_enable_general_guide_or_another_recipe(self):
        with self.assertRaises(ValueError):
            self.module.render_asset(prompt="facts", output_path=self.output, width=1536, height=1024,
                                     origin_admission=self.admission())
        with self.assertRaises(ValueError):
            self.module.render_asset(prompt="facts", output_path=self.output, width=4096, height=4096,
                                     single_dispatch=True, origin_admission=self.admission())
        self.open.assert_not_called()

    def test_known_provider_response_uses_strict_download_not_url_crawler(self):
        self.open.return_value = FakeResponse(json.dumps({"aiRecord": {"aiRecordDetail": {
            "resultObject": ["https://s3.us-east-1.amazonaws.com/example/scene.png"]}}}).encode(), {"Content-Type": "application/json"})
        self.module._download_origin_asset = Mock()
        self.module._download_asset = Mock(side_effect=AssertionError("Do not use the general downloader"))
        self.render()
        self.module._download_origin_asset.assert_called_once()
        self.module._download_asset.assert_not_called()
        self.assertTrue(self.module._write_receipt.call_args.kwargs["private_output"])

    def test_private_receipt_retains_integrity_not_a_second_private_asset_or_prose_copy(self):
        self.output.write_bytes(b"exact-local-asset")
        self.module.RECEIPTS_ROOT = Path(self.temp.name) / "receipts"
        path = self.write_receipt(render_id="synthetic", requested_prompt="private childhood facts",
            submitted_prompt="private childhood facts", output_path=self.output, width=24, height=16,
            backend_provider="onemin", quality="low", model_candidates=["gpt-image-1-mini"],
            manager_principal_id="operator", manager_allow_reserve=False, private_output=True,
            result_json={"receipt_json": {"model": "gpt-image-1-mini"}, "output_json": {
                "asset_urls": ["https://provider.invalid/private-asset"], "preview_text": "private childhood facts"}})
        receipt = path.read_text()
        self.assertNotIn("private childhood facts", receipt)
        self.assertNotIn("provider.invalid", receipt)
        self.assertEqual(self.module.hashlib.sha256(self.output.read_bytes()).hexdigest(), json.loads(receipt)["output_json"]["content_sha256"])

    def test_documented_relative_result_uses_only_its_matching_temporary_url(self):
        asset = "development/images/synthetic_scene.png"
        url = "https://s3.us-east-1.amazonaws.com/asset.1min.ai/" + asset + "?signature=synthetic"
        body = {"aiRecord": {"status": "SUCCESS", "temporaryUrl": url,
                "aiRecordDetail": {"resultObject": [asset]}}}
        self.open.return_value = FakeResponse(json.dumps(body).encode(), {"Content-Type": "application/json"})
        self.module._download_origin_asset = Mock()
        self.render(origin_admission=self.admission())
        self.module._download_origin_asset.assert_called_once_with(url, self.output)
        self.assertEqual(1, self.open.call_count)

    def test_relative_result_cannot_redirect_to_another_asset_or_extra_results(self):
        asset = "development/images/synthetic_scene.png"
        for url in ("https://other.invalid/" + asset,
                    "https://s3.us-east-1.amazonaws.com/another-bucket/" + asset,
                    "https://s3.us-east-1.amazonaws.com/asset.1min.ai/development/images/other.png"):
            with self.assertRaises(RuntimeError):
                self.module._origin_asset_url({"aiRecord": {"temporaryUrl": url,
                    "aiRecordDetail": {"resultObject": [asset]}}})
        for result in ([asset, asset], ["development/images/../secret.png"], [], None):
            with self.assertRaises(RuntimeError):
                self.module._origin_asset_url({"aiRecord": {"aiRecordDetail": {"resultObject": result}}})

    def test_documented_storage_host_preserves_exact_relative_asset_binding(self):
        asset = "images/2026_09_26_scene.png"
        url = "https://storage.1min.ai/" + asset + "?signature=synthetic"
        self.assertEqual(url, self.module._origin_asset_url({"aiRecord": {"temporaryUrl": url,
            "aiRecordDetail": {"resultObject": [asset]}}}))

    def test_failed_response_preserves_private_recovery_identity_without_private_fields(self):
        body = {"aiRecord": {"uuid": "synthetic-record-id", "status": "PROCESSING", "teamId": "private-account",
                "temporaryUrl": "https://private.invalid/secret", "aiRecordDetail": {
                    "promptObject": {"prompt": "private prose"}, "resultObject": []}}}
        self.open.return_value = FakeResponse(json.dumps(body).encode(), {"Content-Type": "application/json"})
        with self.assertRaises(RuntimeError):
            self.render(origin_admission=self.admission())
        attempt = self.module._write_attempt_status.call_args.kwargs
        self.assertEqual("failed_no_retry", attempt["phase"])
        self.assertEqual("synthetic-record-id", attempt["provider_response"]["uuid"])
        self.assertEqual("PROCESSING", attempt["provider_response"]["status"])
        self.assertNotIn("private", json.dumps(attempt["provider_response"]))
        self.assertEqual(1, self.open.call_count)

    def test_non_json_private_response_never_enters_error_receipts(self):
        self.open.return_value = FakeResponse(b"private childhood facts", {"Content-Type": "text/plain"})
        with self.assertRaises(RuntimeError) as error:
            self.render()
        self.assertNotIn("private childhood facts", str(error.exception))
        self.assertNotIn("private childhood facts", str(self.module._write_attempt_status.call_args_list))

    def test_disabled_or_unapproved_backend_never_reserves_or_dispatches(self):
        for env in ({"CHUMMER_MEDIA_FACTORY_ENABLE_IMAGE_EXECUTION": "0"},
                    {"CHUMMER_MEDIA_FACTORY_IMAGE_BACKEND": "openai"},
                    {"CHUMMER6_ONEMIN_ENDPOINT": "https://other.example/features"}):
            with patch.dict(os.environ, env):
                with self.assertRaises(RuntimeError):
                    self.render()
        self.module._reserve_onemin_image_slot.assert_not_called()
        self.open.assert_not_called()

    def test_dry_run_never_reserves_or_dispatches(self):
        self.assertTrue(self.render(dry_run=True)["single_dispatch"])
        self.module._reserve_onemin_image_slot.assert_not_called()
        self.open.assert_not_called()

    def test_api_redirect_is_rejected(self):
        with self.assertRaisesRegex(RuntimeError, "redirect_rejected"):
            self.module._NoRenderRedirects().redirect_request(None, None, 307, "redirect", {}, "https://other.example/")

    def test_origin_download_rejects_private_hosts_ports_and_credentials(self):
        for url in ("https://localhost/image.png", "https://s3.other-region.amazonaws.com/image.png",
                    "https://api.1min.ai:8080/a.png", "https://secret@api.1min.ai/a.png", "http://api.1min.ai/a.png"):
            with self.assertRaisesRegex(RuntimeError, "origin_rejected"):
                self.module._download_origin_asset(url, self.output)
        with patch.object(self.module.socket, "getaddrinfo", return_value=[(2, 1, 6, "", ("127.0.0.1", 443))]):
            with self.assertRaisesRegex(RuntimeError, "address_rejected"):
                self.module._download_origin_asset("https://api.1min.ai/a.png", self.output)

    def test_origin_download_does_not_follow_redirect_or_overallocate_chunked_body(self):
        for status, body, error in ((302, b"", "status_rejected"), (200, b"x" * (4 * 1024 * 1024 + 1), "too_large")):
            response = FakeResponse(body, {"Content-Type": "image/png"})
            response.status = status
            connection = Mock()
            connection.getresponse.return_value = response
            with patch.object(self.module.socket, "getaddrinfo", return_value=[(2, 1, 6, "", ("1.1.1.1", 443))]), \
                 patch.object(self.module.http.client.HTTPSConnection, "__new__", return_value=connection):
                with self.assertRaisesRegex(RuntimeError, error):
                    self.module._download_origin_asset("https://api.1min.ai/a.png", self.output)
            connection.request.assert_called_once()
            connection.close.assert_called_once()
        self.assertFalse(self.output.exists())


if __name__ == "__main__":
    unittest.main()
