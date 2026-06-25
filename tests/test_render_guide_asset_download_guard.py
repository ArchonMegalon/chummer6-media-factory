from __future__ import annotations

import importlib.util
import os
from pathlib import Path
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[1]


def load_render_module():
    module_path = ROOT / "scripts" / "render_guide_asset.py"
    spec = importlib.util.spec_from_file_location("render_guide_asset_test_module", module_path)
    if spec is None or spec.loader is None:
        raise RuntimeError("Could not load render_guide_asset.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class FakeResponse:
    def __init__(self, data: bytes, headers: dict[str, str] | None = None):
        self._data = data
        self._offset = 0
        self.headers = headers or {}

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc, tb):
        return False

    def read(self, size: int = -1) -> bytes:
        if self._offset >= len(self._data):
            return b""
        if size is None or size < 0:
            size = len(self._data) - self._offset
        chunk = self._data[self._offset : self._offset + size]
        self._offset += len(chunk)
        return chunk


class RenderGuideAssetDownloadGuardTests(unittest.TestCase):
    def setUp(self) -> None:
        self.module = load_render_module()
        self._original_env = os.environ.copy()

    def tearDown(self) -> None:
        os.environ.clear()
        os.environ.update(self._original_env)

    def _allow_public_dns(self) -> None:
        self.module.socket.getaddrinfo = lambda *args, **kwargs: [
            (self.module.socket.AF_INET, self.module.socket.SOCK_STREAM, 6, "", ("93.184.216.34", 443))
        ]

    def test_collect_asset_urls_filters_untrusted_urls(self) -> None:
        payload = {
            "url": "http://api.1min.ai/asset/plain-http.png",
            "image_url": "https://evil.example/image/not-allowed.png",
            "nested": [
                "/asset/rendered.png",
                "https://api.1min.ai/download/rendered.png",
                "https://api.1min.ai/download/rendered.png",
            ],
        }

        self.assertEqual(
            [
                "https://api.1min.ai/asset/rendered.png",
                "https://api.1min.ai/download/rendered.png",
            ],
            self.module._collect_asset_urls(payload),
        )

    def test_private_literal_hosts_stay_blocked_even_if_configured(self) -> None:
        os.environ["CHUMMER_MEDIA_FACTORY_ASSET_DOWNLOAD_ALLOWED_HOSTS"] = "127.0.0.1,api.1min.ai"

        self.assertIsNone(self.module._normalize_download_asset_url("https://127.0.0.1/asset/x.png"))
        self.assertEqual(
            "https://api.1min.ai/asset/x.png",
            self.module._normalize_download_asset_url("https://api.1min.ai/asset/x.png"),
        )

    def test_download_asset_enforces_size_and_mime_guards(self) -> None:
        os.environ["CHUMMER_MEDIA_FACTORY_MAX_ASSET_DOWNLOAD_BYTES"] = "4"
        self._allow_public_dns()

        def too_large_urlopen(request, timeout):
            return FakeResponse(b"x" * 1025, {"Content-Type": "image/png"})

        self.module.urllib.request.urlopen = too_large_urlopen
        with tempfile.TemporaryDirectory() as temp:
            with self.assertRaisesRegex(RuntimeError, "asset_too_large"):
                self.module._download_asset("https://api.1min.ai/asset/large.png", Path(temp) / "large.png")

        os.environ["CHUMMER_MEDIA_FACTORY_MAX_ASSET_DOWNLOAD_BYTES"] = "32"

        def bad_mime_urlopen(request, timeout):
            return FakeResponse(b"ok", {"Content-Type": "text/html"})

        self.module.urllib.request.urlopen = bad_mime_urlopen
        with tempfile.TemporaryDirectory() as temp:
            with self.assertRaisesRegex(RuntimeError, "asset_content_type_not_allowed"):
                self.module._download_asset("https://api.1min.ai/asset/page.html", Path(temp) / "page.html")

    def test_download_asset_writes_allowed_provider_asset(self) -> None:
        self._allow_public_dns()

        def ok_urlopen(request, timeout):
            self.assertEqual("https://api.1min.ai/asset/ok.png", request.full_url)
            return FakeResponse(b"png-bytes", {"Content-Type": "image/png", "Content-Length": "9"})

        self.module.urllib.request.urlopen = ok_urlopen
        with tempfile.TemporaryDirectory() as temp:
            output = Path(temp) / "ok.png"
            self.module._download_asset("https://api.1min.ai/asset/ok.png", output)
            self.assertEqual(b"png-bytes", output.read_bytes())

    def test_download_asset_blocks_allowed_host_that_resolves_private(self) -> None:
        self.module.socket.getaddrinfo = lambda *args, **kwargs: [
            (self.module.socket.AF_INET, self.module.socket.SOCK_STREAM, 6, "", ("127.0.0.1", 443))
        ]

        def unexpected_urlopen(request, timeout):
            raise AssertionError("download should not start after private DNS resolution")

        self.module.urllib.request.urlopen = unexpected_urlopen
        with tempfile.TemporaryDirectory() as temp:
            with self.assertRaisesRegex(RuntimeError, "asset_host_resolves_to_private_or_local"):
                self.module._download_asset("https://api.1min.ai/asset/private.png", Path(temp) / "private.png")

    def test_direct_provider_image_response_uses_same_size_guard(self) -> None:
        response = FakeResponse(b"x" * 8, {"Content-Type": "image/png", "Content-Length": "8"})

        with self.assertRaisesRegex(RuntimeError, "asset_too_large"):
            self.module._read_response_bytes_with_limit(response, max_bytes=4, label="asset")


if __name__ == "__main__":
    unittest.main()
