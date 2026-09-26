import importlib.util
import json
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import Mock, patch
import urllib.error

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
