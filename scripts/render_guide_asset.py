#!/usr/bin/env python3
from __future__ import annotations

import argparse
import hashlib
import json
import os
import sys
import urllib.request
import uuid
from datetime import datetime, timezone
from math import gcd
from pathlib import Path


MEDIA_FACTORY_ROOT = Path(__file__).resolve().parents[1]
EA_ROOT = Path("/docker/EA")
EA_APP_ROOT = EA_ROOT / "ea"
EA_SCRIPTS_ROOT = EA_ROOT / "scripts"
STATE_ROOT = Path(os.environ.get("CHUMMER_MEDIA_FACTORY_STATE_DIR", "/docker/fleet/state/chummer6/media-factory"))
RECEIPTS_ROOT = STATE_ROOT / "receipts"

for root in (EA_APP_ROOT, EA_SCRIPTS_ROOT):
    if str(root) not in sys.path:
        sys.path.insert(0, str(root))

from chummer6_runtime_config import load_local_env, load_runtime_overrides  # type: ignore  # noqa: E402
from app.domain.models import ToolDefinition, ToolInvocationRequest  # type: ignore  # noqa: E402


def _seed_runtime_env() -> None:
    for mapping in (load_local_env(), load_runtime_overrides()):
        for key, value in mapping.items():
            os.environ.setdefault(str(key), str(value))


def _now_iso() -> str:
    return datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")


def _aspect_ratio(width: int, height: int) -> str:
    if width <= 0 or height <= 0:
        return "16:9"
    factor = gcd(width, height) or 1
    return f"{max(1, width // factor)}:{max(1, height // factor)}"


def _model_candidates() -> list[str]:
    values: list[str] = []
    for candidate in (
        os.environ.get("CHUMMER6_ONEMIN_MODEL"),
        os.environ.get("EA_ONEMIN_TOOL_IMAGE_MODEL"),
        "gpt-image-1-mini",
        "gpt-image-1",
        "dall-e-3",
    ):
        cleaned = str(candidate or "").strip()
        if cleaned and cleaned not in values:
            values.append(cleaned)
    return values


def _image_execution_enabled() -> bool:
    raw = str(os.environ.get("CHUMMER_MEDIA_FACTORY_ENABLE_IMAGE_EXECUTION", "1")).strip().lower()
    return raw not in {"0", "false", "no", "off", "disabled"}


def _selected_backend() -> str:
    backend = str(os.environ.get("CHUMMER_MEDIA_FACTORY_IMAGE_BACKEND") or "onemin").strip().lower()
    if backend in {"", "default"}:
        return "onemin"
    if backend in {"ea_onemin", "onemin"}:
        return "onemin"
    if backend in {"disabled", "off", "none"}:
        return "disabled"
    return backend


def _size_candidates(model: str, *, width: int, height: int) -> list[str]:
    configured = str(os.environ.get("CHUMMER6_ONEMIN_IMAGE_SIZE") or "").strip().lower()
    if configured and configured != "auto":
        return [configured]
    landscape = width > height
    square = width == height
    if square:
        return ["1024x1024"]
    normalized = str(model or "").strip().lower()
    if normalized.startswith("gpt-image-") or normalized.startswith("dall-e-"):
        return ["1536x1024", "1024x1024"] if landscape else ["1024x1536", "1024x1024"]
    return [f"{max(1, width)}x{max(1, height)}"]


def _tool_definition() -> ToolDefinition:
    return ToolDefinition(
        tool_name="provider.onemin.image_generate",
        version="v1",
        input_schema_json={},
        output_schema_json={},
        policy_json={"builtin": True, "action_kind": "image.generate"},
        allowed_channels=(),
        approval_default="none",
        enabled=True,
        updated_at=_now_iso(),
    )


def _download_asset(url: str, output_path: Path) -> None:
    request = urllib.request.Request(str(url), headers={"User-Agent": "Chummer-Media-Factory/1.0"})
    with urllib.request.urlopen(request, timeout=180) as response:
        data = response.read()
    if not data:
        raise RuntimeError(f"empty_asset:{url}")
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_bytes(data)


def _write_receipt(
    *,
    render_id: str,
    prompt: str,
    output_path: Path,
    width: int,
    height: int,
    backend_provider: str,
    quality: str,
    result,
) -> Path:
    RECEIPTS_ROOT.mkdir(parents=True, exist_ok=True)
    receipt_path = RECEIPTS_ROOT / f"{render_id}.json"
    payload = {
        "render_id": render_id,
        "observed_at": _now_iso(),
        "prompt_sha256": hashlib.sha256(prompt.encode("utf-8")).hexdigest(),
        "output_path": str(output_path),
        "width": width,
        "height": height,
        "provider": "media_factory",
        "backend_provider": backend_provider,
        "backend_selection_env": "CHUMMER_MEDIA_FACTORY_IMAGE_BACKEND",
        "backend_enable_env": "CHUMMER_MEDIA_FACTORY_ENABLE_IMAGE_EXECUTION",
        "image_execution_enabled": _image_execution_enabled(),
        "quality": quality,
        "requested_model_candidates": _model_candidates(),
        "tool_name": result.tool_name,
        "action_kind": result.action_kind,
        "receipt_json": dict(result.receipt_json or {}),
        "output_json": {
            "asset_urls": list(result.output_json.get("asset_urls") or []),
            "preview_text": str(result.output_json.get("preview_text") or ""),
            "provider_account_name": str(result.output_json.get("provider_account_name") or ""),
            "provider_key_slot": str(result.output_json.get("provider_key_slot") or ""),
            "model": str(result.output_json.get("model") or ""),
        },
    }
    receipt_path.write_text(json.dumps(payload, indent=2, ensure_ascii=True) + "\n", encoding="utf-8")
    return receipt_path


def render_asset(*, prompt: str, output_path: Path, width: int, height: int, dry_run: bool = False) -> dict[str, object]:
    _seed_runtime_env()
    render_id = f"mf-{uuid.uuid4().hex}"
    backend_provider = _selected_backend()
    image_execution_enabled = _image_execution_enabled()
    quality = str(os.environ.get("CHUMMER6_ONEMIN_IMAGE_QUALITY") or "low").strip() or "low"
    payload = {
        "prompt": prompt,
        "aspect_ratio": _aspect_ratio(width, height),
        "quality": quality,
        "model": _model_candidates()[0],
        "output_format": "png",
    }
    if dry_run:
        return {
            "render_id": render_id,
            "dry_run": True,
            "provider": "media_factory",
            "backend_provider": "disabled" if not image_execution_enabled else backend_provider,
            "image_execution_enabled": image_execution_enabled,
            "backend_selection_env": "CHUMMER_MEDIA_FACTORY_IMAGE_BACKEND",
            "backend_enable_env": "CHUMMER_MEDIA_FACTORY_ENABLE_IMAGE_EXECUTION",
            "output_path": str(output_path),
            "payload_json": payload,
        }
    if not image_execution_enabled or backend_provider == "disabled":
        raise RuntimeError("media_factory:rendering_disabled")
    if backend_provider != "onemin":
        raise RuntimeError(f"media_factory:unsupported_backend:{backend_provider}")
    from app.services.tool_execution_common import ToolExecutionError  # type: ignore  # noqa: E402
    from app.services.tool_execution_onemin_adapter import OneminToolAdapter  # type: ignore  # noqa: E402

    adapter = OneminToolAdapter()
    errors: list[str] = []
    result = None
    for model in _model_candidates():
        for size in _size_candidates(model, width=width, height=height):
            request = ToolInvocationRequest(
                session_id=render_id,
                step_id=f"{render_id}-{model}-{size}".replace("/", "_"),
                tool_name="provider.onemin.image_generate",
                action_kind="image.generate",
                payload_json={
                    "prompt": prompt,
                    "aspect_ratio": _aspect_ratio(width, height),
                    "quality": quality,
                    "model": model,
                    "size": size,
                    "output_format": "png",
                },
                context_json={"principal_id": "chummer-media-factory"},
            )
            try:
                result = adapter.execute_image_generate(request, _tool_definition())
                break
            except ToolExecutionError as exc:
                errors.append(f"{model}:{size}:{str(exc)[:180]}")
        if result is not None:
            break
    if result is None:
        raise RuntimeError("media_factory:" + " || ".join(errors[:6]))
    asset_urls = list(result.output_json.get("asset_urls") or [])
    if not asset_urls:
        raise RuntimeError("media_factory:no_asset_urls")
    _download_asset(asset_urls[0], output_path)
    receipt_path = _write_receipt(
        render_id=render_id,
        prompt=prompt,
        output_path=output_path,
        width=width,
        height=height,
        backend_provider=backend_provider,
        quality=quality,
        result=result,
    )
    return {
        "render_id": render_id,
        "provider": "media_factory",
        "backend_provider": backend_provider,
        "output_path": str(output_path),
        "receipt_path": str(receipt_path),
        "asset_url": asset_urls[0],
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Media Factory bridge for Chummer guide image renders.")
    parser.add_argument("--prompt", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--width", type=int, required=True)
    parser.add_argument("--height", type=int, required=True)
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    payload = render_asset(
        prompt=str(args.prompt),
        output_path=Path(args.output),
        width=int(args.width),
        height=int(args.height),
        dry_run=bool(args.dry_run),
    )
    print(json.dumps(payload, ensure_ascii=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
