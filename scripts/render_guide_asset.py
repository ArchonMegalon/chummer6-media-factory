#!/usr/bin/env python3
from __future__ import annotations

import argparse
import hashlib
import json
import os
import sys
import urllib.error
import urllib.parse
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


def _bool_env(name: str, *, default: bool) -> bool:
    raw = str(os.environ.get(name) or "").strip().lower()
    if not raw:
        return default
    if raw in {"1", "true", "yes", "on", "allow", "allowed"}:
        return True
    if raw in {"0", "false", "no", "off", "deny", "denied", "forbid", "forbidden"}:
        return False
    return default


def _clip_prompt_text(value: object, *, limit: int) -> str:
    cleaned = " ".join(str(value or "").split()).strip()
    if len(cleaned) <= limit:
        return cleaned
    clipped = cleaned[: limit + 1]
    if " " in clipped:
        clipped = clipped.rsplit(" ", 1)[0]
    return clipped.rstrip(" ,;:-")


def _onemin_prompt_char_limit() -> int:
    raw = str(os.environ.get("CHUMMER_MEDIA_FACTORY_ONEMIN_PROMPT_CHAR_LIMIT") or "3800").strip()
    try:
        return max(512, min(3900, int(float(raw))))
    except Exception:
        return 3800


def _prepare_onemin_prompt(prompt: str) -> str:
    # 1min/OpenAI image routes hard-fail on overly long prompts. Normalize and
    # cap the payload locally so the worker can hand rich prompts to the bridge
    # without tripping the upstream 4000-character limit.
    return _clip_prompt_text(prompt, limit=_onemin_prompt_char_limit())


def _image_execution_enabled() -> bool:
    raw = str(os.environ.get("CHUMMER_MEDIA_FACTORY_ENABLE_IMAGE_EXECUTION", "1")).strip().lower()
    return raw not in {"0", "false", "no", "off", "disabled"}


def _manager_principal_id() -> str:
    return str(os.environ.get("CHUMMER_MEDIA_FACTORY_EA_PRINCIPAL_ID") or "chummer-media-factory").strip() or "chummer-media-factory"


def _manager_allow_reserve() -> bool:
    return _bool_env("CHUMMER_MEDIA_FACTORY_ONEMIN_ALLOW_RESERVE", default=True)


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


def _ea_local_base_url() -> str:
    return str(os.environ.get("CHUMMER6_EA_BASE_URL") or os.environ.get("EA_BASE_URL") or "http://127.0.0.1:8090").rstrip("/")


def _ea_local_headers(*, principal_id: str) -> dict[str, str]:
    headers = {
        "Accept": "application/json",
        "User-Agent": "Chummer-Media-Factory/1.0",
    }
    token = str(os.environ.get("EA_API_TOKEN") or "").strip()
    if token:
        headers["Authorization"] = f"Bearer {token}"
    if principal_id:
        headers["X-EA-Principal-ID"] = principal_id
    return headers


def _ea_local_json_request(
    method: str,
    path: str,
    *,
    principal_id: str,
    payload: dict[str, object] | None = None,
) -> object | None:
    body = None
    headers = _ea_local_headers(principal_id=principal_id)
    if payload is not None:
        body = json.dumps(payload).encode("utf-8")
        headers["Content-Type"] = "application/json"
    request = urllib.request.Request(
        f"{_ea_local_base_url()}{path}",
        headers=headers,
        data=body,
        method=str(method or "GET").upper(),
    )
    try:
        with urllib.request.urlopen(request, timeout=10) as response:
            text = response.read().decode("utf-8", errors="replace")
    except Exception:
        return None
    try:
        return json.loads(text)
    except Exception:
        return None


def _ea_local_json_post(path: str, *, principal_id: str, payload: dict[str, object]) -> object | None:
    return _ea_local_json_request("POST", path, principal_id=principal_id, payload=payload)


def _estimate_onemin_image_credits(*, width: int, height: int) -> int:
    raw = str(os.environ.get("CHUMMER6_ONEMIN_ESTIMATED_IMAGE_CREDITS") or "").strip()
    if raw:
        try:
            return max(0, int(float(raw)))
        except Exception:
            pass
    megapixels = max(1.0, (max(1, int(width)) * max(1, int(height))) / 1000000.0)
    return int(round(1200.0 * megapixels))


def _reserve_onemin_image_slot(
    *,
    width: int,
    height: int,
    principal_id: str,
    allow_reserve: bool,
) -> dict[str, object] | None:
    payload = _ea_local_json_post(
        "/v1/providers/onemin/reserve-image",
        principal_id=principal_id,
        payload={
            "request_id": f"media-factory-image-{int(datetime.now(timezone.utc).timestamp() * 1000)}-{width}x{height}",
            "estimated_credits": _estimate_onemin_image_credits(width=width, height=height),
            "allow_reserve": bool(allow_reserve),
        },
    )
    if not isinstance(payload, dict):
        return None
    if not str(payload.get("lease_id") or "").strip():
        return None
    return dict(payload)


def _reserve_onemin_image_slot_locally(
    *,
    width: int,
    height: int,
    principal_id: str,
    allow_reserve: bool,
    request_id: str,
) -> tuple[dict[str, object], object] | tuple[None, None]:
    try:
        from app.repositories.onemin_manager import build_onemin_manager_service_repo
        from app.services import responses_upstream as upstream
        from app.services.onemin_manager import OneminManagerService
        from app.settings import get_settings, settings_with_storage_backend
    except Exception:
        return None, None
    try:
        settings = settings_with_storage_backend(get_settings(), "memory")
        manager = OneminManagerService(repo=build_onemin_manager_service_repo(settings))
        provider_health = upstream._provider_health_report()
        estimated_credits = _estimate_onemin_image_credits(width=width, height=height)
        candidates = manager._candidates_from_provider_health(provider_health=provider_health)  # type: ignore[attr-defined]
        reserve_candidates = [
            candidate
            for candidate in candidates
            if str(candidate.get("slot_role") or "").strip().lower() == "reserve"
        ]
        candidate_pools = [reserve_candidates, candidates] if allow_reserve and reserve_candidates else [candidates]
        lease = None
        for candidate_pool in candidate_pools:
            if not candidate_pool:
                continue
            lease = manager.reserve_for_candidates(
                candidates=candidate_pool,
                lane="image",
                capability="image_generate",
                principal_id=principal_id,
                request_id=request_id,
                estimated_credits=estimated_credits,
                allow_reserve=allow_reserve,
            )
            if lease is not None:
                break
    except Exception:
        return None, None
    if not isinstance(lease, dict) or not str(lease.get("lease_id") or "").strip():
        return None, None
    return dict(lease), manager


def _release_onemin_image_slot(
    *,
    lease_id: str,
    principal_id: str,
    status: str,
    actual_credits_delta: int | None = None,
    error: str = "",
) -> None:
    normalized = str(lease_id or "").strip()
    if not normalized:
        return
    _ = _ea_local_json_post(
        f"/v1/providers/onemin/leases/{urllib.parse.quote(normalized, safe='')}/release",
        principal_id=principal_id,
        payload={
            "status": str(status or "released").strip() or "released",
            "actual_credits_delta": actual_credits_delta,
            "error": str(error or "").strip(),
        },
    )


def _release_onemin_image_slot_locally(
    *,
    manager: object | None,
    lease_id: str,
    status: str,
    actual_credits_delta: int | None = None,
    error: str = "",
) -> None:
    normalized = str(lease_id or "").strip()
    if not normalized or manager is None:
        return
    try:
        if actual_credits_delta is not None:
            manager.record_usage(lease_id=normalized, actual_credits_delta=actual_credits_delta, status=str(status or "released").strip() or "released")
        manager.release_lease(lease_id=normalized, status=str(status or "released").strip() or "released", error=str(error or "").strip())
    except Exception:
        return


def _onemin_endpoint() -> str:
    return str(os.environ.get("CHUMMER6_ONEMIN_ENDPOINT") or "https://api.1min.ai/api/features").strip() or "https://api.1min.ai/api/features"


def _onemin_timeout_seconds() -> int:
    raw = str(os.environ.get("CHUMMER_MEDIA_FACTORY_ONEMIN_TIMEOUT_SECONDS") or "180").strip()
    try:
        return max(30, min(600, int(float(raw))))
    except Exception:
        return 180


def _collect_asset_urls(value: object) -> list[str]:
    found: list[str] = []
    if isinstance(value, str):
        candidate = value.strip()
        lowered = candidate.lower()
        if candidate.startswith("http://") or candidate.startswith("https://"):
            found.append(candidate)
        elif candidate.startswith("/") and any(token in lowered for token in ("/asset/", "/image/", "/render/", "/download/")):
            found.append("https://api.1min.ai" + candidate)
    elif isinstance(value, dict):
        for key in ("url", "image_url", "download_url", "image", "imageUrl", "asset_url", "assetUrl"):
            if key in value:
                found.extend(_collect_asset_urls(value.get(key)))
        for nested in value.values():
            found.extend(_collect_asset_urls(nested))
    elif isinstance(value, (list, tuple, set)):
        for nested in value:
            found.extend(_collect_asset_urls(nested))
    deduped: list[str] = []
    seen: set[str] = set()
    for candidate in found:
        if candidate in seen:
            continue
        seen.add(candidate)
        deduped.append(candidate)
    return deduped


def _onemin_image_payload(*, prompt: str, model: str, size: str, aspect_ratio: str, quality: str) -> dict[str, object]:
    prompt_object: dict[str, object] = {
        "prompt": prompt,
        "n": 1,
        "quality": quality,
        "output_format": "png",
    }
    if size:
        prompt_object["size"] = size
    elif aspect_ratio:
        prompt_object["aspect_ratio"] = aspect_ratio
    return {
        "type": "IMAGE_GENERATOR",
        "model": model,
        "promptObject": prompt_object,
    }


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
    requested_prompt: str,
    submitted_prompt: str,
    output_path: Path,
    width: int,
    height: int,
    backend_provider: str,
    quality: str,
    manager_principal_id: str,
    manager_allow_reserve: bool,
    result_json: dict[str, object],
) -> Path:
    RECEIPTS_ROOT.mkdir(parents=True, exist_ok=True)
    receipt_path = RECEIPTS_ROOT / f"{render_id}.json"
    payload = {
        "render_id": render_id,
        "observed_at": _now_iso(),
        "requested_prompt_sha256": hashlib.sha256(requested_prompt.encode("utf-8")).hexdigest(),
        "submitted_prompt_sha256": hashlib.sha256(submitted_prompt.encode("utf-8")).hexdigest(),
        "requested_prompt_length": len(requested_prompt),
        "submitted_prompt_length": len(submitted_prompt),
        "submitted_prompt_char_limit": _onemin_prompt_char_limit(),
        "output_path": str(output_path),
        "width": width,
        "height": height,
        "provider": "media_factory",
        "backend_provider": backend_provider,
        "backend_selection_env": "CHUMMER_MEDIA_FACTORY_IMAGE_BACKEND",
        "backend_enable_env": "CHUMMER_MEDIA_FACTORY_ENABLE_IMAGE_EXECUTION",
        "manager_principal_id": manager_principal_id,
        "manager_allow_reserve": manager_allow_reserve,
        "manager_principal_env": "CHUMMER_MEDIA_FACTORY_EA_PRINCIPAL_ID",
        "manager_allow_reserve_env": "CHUMMER_MEDIA_FACTORY_ONEMIN_ALLOW_RESERVE",
        "image_execution_enabled": _image_execution_enabled(),
        "quality": quality,
        "requested_model_candidates": _model_candidates(),
        "tool_name": str(result_json.get("tool_name") or "provider.onemin.image_generate"),
        "action_kind": str(result_json.get("action_kind") or "image.generate"),
        "receipt_json": dict(result_json.get("receipt_json") or {}),
        "output_json": dict(result_json.get("output_json") or {}),
    }
    receipt_path.write_text(json.dumps(payload, indent=2, ensure_ascii=True) + "\n", encoding="utf-8")
    return receipt_path


def render_asset(*, prompt: str, output_path: Path, width: int, height: int, dry_run: bool = False) -> dict[str, object]:
    _seed_runtime_env()
    render_id = f"mf-{uuid.uuid4().hex}"
    backend_provider = _selected_backend()
    image_execution_enabled = _image_execution_enabled()
    manager_principal_id = _manager_principal_id()
    manager_allow_reserve = _manager_allow_reserve()
    quality = str(os.environ.get("CHUMMER6_ONEMIN_IMAGE_QUALITY") or "low").strip() or "low"
    submitted_prompt = _prepare_onemin_prompt(prompt)
    payload = {
        "prompt": submitted_prompt,
        "aspect_ratio": _aspect_ratio(width, height),
        "quality": quality,
        "model": _model_candidates()[0],
        "output_format": "png",
        "manager_allow_reserve": manager_allow_reserve,
        "requested_prompt_length": len(str(prompt or "")),
        "submitted_prompt_length": len(submitted_prompt),
        "submitted_prompt_char_limit": _onemin_prompt_char_limit(),
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
            "manager_principal_id": manager_principal_id,
            "manager_allow_reserve": manager_allow_reserve,
            "manager_principal_env": "CHUMMER_MEDIA_FACTORY_EA_PRINCIPAL_ID",
            "manager_allow_reserve_env": "CHUMMER_MEDIA_FACTORY_ONEMIN_ALLOW_RESERVE",
            "output_path": str(output_path),
            "payload_json": payload,
        }
    if not image_execution_enabled or backend_provider == "disabled":
        raise RuntimeError("media_factory:rendering_disabled")
    if backend_provider != "onemin":
        raise RuntimeError(f"media_factory:unsupported_backend:{backend_provider}")
    reservation_request_id = f"media-factory-image-{int(datetime.now(timezone.utc).timestamp() * 1000)}-{width}x{height}"
    reservation = _reserve_onemin_image_slot(
        width=width,
        height=height,
        principal_id=manager_principal_id,
        allow_reserve=manager_allow_reserve,
    )
    reservation_source = "ea_http"
    local_manager = None
    if reservation is None:
        reservation, local_manager = _reserve_onemin_image_slot_locally(
            width=width,
            height=height,
            principal_id=manager_principal_id,
            allow_reserve=manager_allow_reserve,
            request_id=reservation_request_id,
        )
        if reservation is not None:
            reservation_source = "ea_local_manager"
    if reservation is None:
        raise RuntimeError("media_factory:onemin_manager_capacity_unavailable")
    lease_id = str(reservation.get("lease_id") or "").strip()
    secret_env_name = str(reservation.get("secret_env_name") or "").strip()
    reserved_account_id = str(reservation.get("account_id") or reservation.get("account_name") or "").strip() or "onemin_unknown"
    reserved_slot_name = str(reservation.get("slot_name") or secret_env_name or "").strip() or "unknown"
    api_key = str(os.environ.get(secret_env_name) or "").strip()
    errors: list[str] = []
    if not api_key:
        _release_onemin_image_slot(
            lease_id=lease_id,
            principal_id=manager_principal_id,
            status="failed",
            error=f"reserved_slot_missing_local_key:{secret_env_name or 'unknown'}",
        )
        raise RuntimeError(f"media_factory:reserved_slot_missing_local_key:{secret_env_name or 'unknown'}")

    result_json: dict[str, object] | None = None
    try:
        for model in _model_candidates():
            for size in _size_candidates(model, width=width, height=height):
                payload = _onemin_image_payload(
                    prompt=submitted_prompt,
                    model=model,
                    size=size,
                    aspect_ratio=_aspect_ratio(width, height),
                    quality=quality,
                )
                request = urllib.request.Request(
                    _onemin_endpoint(),
                    headers={
                        "API-KEY": api_key,
                        "Content-Type": "application/json",
                        "User-Agent": "Chummer-Media-Factory/1.0",
                    },
                    data=json.dumps(payload).encode("utf-8"),
                    method="POST",
                )
                try:
                    with urllib.request.urlopen(request, timeout=_onemin_timeout_seconds()) as response:
                        data = response.read()
                        content_type = str(response.headers.get("Content-Type") or "").lower()
                except urllib.error.HTTPError as exc:
                    body = exc.read().decode("utf-8", errors="replace").strip()
                    errors.append(f"{model}:{size}:http_{exc.code}:{body[:180]}")
                    continue
                except urllib.error.URLError as exc:
                    errors.append(f"{model}:{size}:urlerror:{exc.reason}")
                    continue
                except TimeoutError:
                    errors.append(f"{model}:{size}:timeout")
                    continue

                asset_urls: list[str] = []
                preview_text = ""
                if data and content_type.startswith("image/"):
                    output_path.parent.mkdir(parents=True, exist_ok=True)
                    output_path.write_bytes(data)
                    preview_text = str(output_path)
                else:
                    decoded = data.decode("utf-8", errors="replace").strip()
                    preview_text = decoded[:280]
                    try:
                        body = json.loads(decoded)
                    except Exception:
                        errors.append(f"{model}:{size}:non_json_response:{decoded[:180]}")
                        continue
                    asset_urls = _collect_asset_urls(body)
                    if not asset_urls:
                        errors.append(f"{model}:{size}:no_asset_urls")
                        continue
                    _download_asset(asset_urls[0], output_path)
                result_json = {
                    "tool_name": "provider.onemin.image_generate",
                    "action_kind": "image.generate",
                    "receipt_json": {
                        "handler_key": "provider.onemin.image_generate",
                        "invocation_contract": "tool.v1",
                        "provider_key": "onemin",
                        "provider_backend": "1min",
                        "provider_account_name": reserved_account_id,
                        "provider_key_slot": reserved_slot_name,
                        "model": model,
                        "feature_type": "IMAGE_GENERATOR",
                        "tool_version": "v1",
                        "manager_lease_id": lease_id,
                    },
                    "output_json": {
                        "asset_urls": asset_urls,
                        "preview_text": preview_text,
                        "provider_account_name": reserved_account_id,
                        "provider_key_slot": reserved_slot_name,
                        "model": model,
                    },
                }
                break
            if result_json is not None:
                break
        if result_json is None:
            raise RuntimeError("media_factory:" + " || ".join(errors[:6]))
        _release_onemin_image_slot(
            lease_id=lease_id,
            principal_id=manager_principal_id,
            status="released",
            actual_credits_delta=_estimate_onemin_image_credits(width=width, height=height),
        )
        _release_onemin_image_slot_locally(
            manager=local_manager,
            lease_id=lease_id,
            status="released",
            actual_credits_delta=_estimate_onemin_image_credits(width=width, height=height),
        )
        lease_id = ""
        receipt_path = _write_receipt(
            render_id=render_id,
            requested_prompt=str(prompt or ""),
            submitted_prompt=submitted_prompt,
            output_path=output_path,
            width=width,
            height=height,
            backend_provider=backend_provider,
            quality=quality,
            manager_principal_id=manager_principal_id,
            manager_allow_reserve=manager_allow_reserve,
            result_json=result_json,
        )
        asset_urls = list((result_json.get("output_json") or {}).get("asset_urls") or [])
        return {
            "render_id": render_id,
            "provider": "media_factory",
            "backend_provider": backend_provider,
            "manager_principal_id": manager_principal_id,
            "manager_allow_reserve": manager_allow_reserve,
            "manager_reservation_source": reservation_source,
            "provider_account_name": reserved_account_id,
            "provider_key_slot": reserved_slot_name,
            "output_path": str(output_path),
            "receipt_path": str(receipt_path),
            "asset_url": asset_urls[0] if asset_urls else "",
        }
    finally:
        if lease_id:
            _release_onemin_image_slot(
                lease_id=lease_id,
                principal_id=manager_principal_id,
                status="failed",
                error=" || ".join(errors[:3]) if errors else "render_failed",
            )
            _release_onemin_image_slot_locally(
                manager=local_manager,
                lease_id=lease_id,
                status="failed",
                error=" || ".join(errors[:3]) if errors else "render_failed",
            )


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
