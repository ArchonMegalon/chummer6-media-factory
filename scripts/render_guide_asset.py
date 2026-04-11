#!/usr/bin/env python3
from __future__ import annotations

import argparse
import base64
import hashlib
import importlib.util
import json
import os
import re
import subprocess
import sys
import urllib.error
import urllib.parse
import urllib.request
import uuid
from datetime import datetime, timezone
from math import gcd
from pathlib import Path
from time import monotonic


MEDIA_FACTORY_ROOT = Path(__file__).resolve().parents[1]
EA_ROOT = Path("/docker/EA")
EA_APP_ROOT = EA_ROOT / "ea"
EA_SCRIPTS_ROOT = EA_ROOT / "scripts"
STATE_ROOT = Path(os.environ.get("CHUMMER_MEDIA_FACTORY_STATE_DIR", "/docker/fleet/state/chummer6/media-factory"))
RECEIPTS_ROOT = STATE_ROOT / "receipts"
ATTEMPTS_ROOT = STATE_ROOT / "attempts"
HEALTH_OUT = STATE_ROOT / "guide_provider_health.json"

for root in (EA_APP_ROOT, EA_SCRIPTS_ROOT):
    if str(root) not in sys.path:
        sys.path.insert(0, str(root))

from chummer6_runtime_config import load_local_env, load_runtime_overrides  # type: ignore  # noqa: E402


_ONEMIN_SLOT_HINTS_CACHE: dict[str, dict[str, object]] | None = None


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


def _asset_family_for_output_path(output_path: Path) -> str:
    normalized = str(output_path).replace("\\", "/")
    marker = "/Chummer6/"
    if marker in normalized:
        normalized = normalized.split(marker, 1)[1]
    if "/assets/hero/" in normalized or normalized.startswith("assets/hero/"):
        return "hero"
    if "/assets/pages/" in normalized or normalized.startswith("assets/pages/"):
        return "page"
    if "/assets/horizons/" in normalized or normalized.startswith("assets/horizons/"):
        return "horizon"
    if "/assets/parts/" in normalized or normalized.startswith("assets/parts/"):
        return "part"
    return "general"


def _render_watchdog_seconds(backend: str) -> int:
    raw = str(os.environ.get("CHUMMER_MEDIA_FACTORY_RENDER_WATCHDOG_SECONDS") or "").strip()
    try:
        if raw:
            return max(30, min(900, int(float(raw))))
    except Exception:
        pass
    normalized = str(backend or "").strip().lower()
    if normalized == "openai_edits":
        return 180
    return 150


def _load_health_registry() -> dict[str, object]:
    try:
        loaded = json.loads(HEALTH_OUT.read_text(encoding="utf-8")) if HEALTH_OUT.exists() else {}
    except Exception:
        loaded = {}
    providers = loaded.get("providers")
    if not isinstance(providers, dict):
        loaded["providers"] = {}
    return loaded


def _health_outcome(detail: str, *, ok: bool) -> str:
    cleaned = str(detail or "").strip().lower()
    if ok:
        return "success"
    if "watchdog" in cleaned:
        return "no_output_watchdog"
    if "timeout" in cleaned:
        return "timeout"
    if "http_429" in cleaned or "retry_after" in cleaned:
        return "rate_limit"
    if "capacity_unavailable" in cleaned:
        return "capacity_unavailable"
    if "no_asset_urls" in cleaned or "empty_asset" in cleaned:
        return "empty_output"
    return "failure"


def _record_health_attempt(*, backend: str, family: str, detail: str, ok: bool) -> None:
    registry = _load_health_registry()
    providers = registry.get("providers") if isinstance(registry.get("providers"), dict) else {}
    backend_key = str(backend or "").strip().lower() or "unknown"
    provider_entry = dict(providers.get(backend_key) or {})
    families = provider_entry.get("families") if isinstance(provider_entry.get("families"), dict) else {}
    family_entry = dict(families.get(family) or {})
    attempts = family_entry.get("recent_attempts")
    if not isinstance(attempts, list):
        attempts = []
    attempts.append(
        {
            "outcome": _health_outcome(detail, ok=ok),
            "detail": str(detail or "").strip()[:240],
            "ok": bool(ok),
            "observed_at": _now_iso(),
        }
    )
    family_entry["recent_attempts"] = [dict(entry) for entry in attempts if isinstance(entry, dict)][-16:]
    family_entry["success_count"] = int(family_entry.get("success_count") or 0) + (1 if ok else 0)
    family_entry["failure_count"] = int(family_entry.get("failure_count") or 0) + (0 if ok else 1)
    family_entry["updated_at"] = _now_iso()
    families[family] = family_entry
    provider_entry["families"] = families
    provider_entry["updated_at"] = _now_iso()
    providers[backend_key] = provider_entry
    registry["providers"] = providers
    HEALTH_OUT.parent.mkdir(parents=True, exist_ok=True)
    HEALTH_OUT.write_text(json.dumps(registry, indent=2, ensure_ascii=True) + "\n", encoding="utf-8")


def _write_attempt_status(
    *,
    render_id: str,
    phase: str,
    backend_provider: str,
    output_path: Path,
    family: str,
    requested_prompt: str,
    submitted_prompt: str,
    width: int,
    height: int,
    model: str = "",
    size: str = "",
    detail: str = "",
    reference_image: Path | None = None,
) -> Path:
    ATTEMPTS_ROOT.mkdir(parents=True, exist_ok=True)
    attempt_path = ATTEMPTS_ROOT / f"{render_id}.json"
    payload = {
        "render_id": render_id,
        "phase": str(phase or "").strip(),
        "observed_at": _now_iso(),
        "backend_provider": str(backend_provider or "").strip(),
        "family": str(family or "").strip(),
        "output_path": str(output_path),
        "width": int(width),
        "height": int(height),
        "requested_prompt_length": len(str(requested_prompt or "")),
        "submitted_prompt_length": len(str(submitted_prompt or "")),
        "model": str(model or "").strip(),
        "size": str(size or "").strip(),
        "detail": str(detail or "").strip()[:400],
        "reference_image_path": str(reference_image) if isinstance(reference_image, Path) else "",
    }
    attempt_path.write_text(json.dumps(payload, indent=2, ensure_ascii=True) + "\n", encoding="utf-8")
    return attempt_path


def _prompt_looks_flagship(prompt: str | None = None) -> bool:
    lowered = str(prompt or "").strip().lower()
    if not lowered:
        return False
    markers = (
        "flagship",
        "cover-grade",
        "cover grade",
        "cover-poster",
        "promo-poster",
        "public guide banner",
        "poster energy",
        "first-contact",
        "first contact",
    )
    return any(marker in lowered for marker in markers)


def _model_candidates(prompt: str | None = None) -> list[str]:
    values: list[str] = []
    configured_candidates: list[str] = []
    for raw in (
        os.environ.get("CHUMMER6_ONEMIN_MODEL"),
        os.environ.get("CHUMMER_MEDIA_FACTORY_ONEMIN_MODEL"),
        os.environ.get("EA_ONEMIN_TOOL_IMAGE_MODEL"),
    ):
        for candidate in str(raw or "").split(","):
            cleaned = str(candidate or "").strip()
            if cleaned and cleaned not in configured_candidates:
                configured_candidates.append(cleaned)
    for candidate in (
        *configured_candidates,
        "gpt-image-1",
        "black-forest-labs/flux-schnell",
    ):
        cleaned = str(candidate or "").strip()
        if cleaned.lower() == "dall-e-3":
            continue
        if cleaned.lower() == "gpt-image-1-mini" and cleaned not in configured_candidates:
            continue
        if cleaned and cleaned not in values:
            values.append(cleaned)
    if not _prompt_looks_flagship(prompt):
        return values
    if configured_candidates:
        explicit_front = [model for model in configured_candidates if model in values]
        remainder = [model for model in values if model not in explicit_front]
        return [*explicit_front, *remainder]
    preferred = [
        model
        for model in ("gpt-image-1", "black-forest-labs/flux-schnell")
        if model in values
    ]
    remainder = [model for model in values if model not in preferred]
    return [*preferred, *remainder]


def _normalized_onemin_model(model: str | None = None) -> str:
    return str(model or "").strip().lower()


def _is_flux_schnell_model(model: str | None = None) -> bool:
    return _normalized_onemin_model(model) == "black-forest-labs/flux-schnell"


def _configured_onemin_slots() -> list[dict[str, str]]:
    slots: list[dict[str, str]] = []
    seen_keys: set[str] = set()
    seen_env_names: set[str] = set()
    fallback_env_names = sorted(
        (
            env_name
            for env_name in os.environ
            if env_name.startswith("ONEMIN_AI_API_KEY_FALLBACK_") and env_name.rsplit("_", 1)[-1].isdigit()
        ),
        key=lambda env_name: int(env_name.rsplit("_", 1)[-1]),
    )
    for env_name in ("ONEMIN_AI_API_KEY", *fallback_env_names):
        key = str(os.environ.get(env_name) or "").strip()
        if not key or env_name in seen_env_names or key in seen_keys:
            continue
        seen_env_names.add(env_name)
        seen_keys.add(key)
        slots.append({"env_name": env_name, "key": key})
    resolve_script = EA_ROOT / "scripts" / "resolve_onemin_ai_key.sh"
    if resolve_script.exists():
        try:
            output = subprocess.check_output(
                ["bash", str(resolve_script), "--all"],
                text=True,
            )
        except Exception:
            output = ""
        synthetic_index = 0
        for raw in output.splitlines():
            key = str(raw or "").strip()
            if not key or key in seen_keys:
                continue
            seen_keys.add(key)
            synthetic_index += 1
            slots.append({"env_name": f"ONEMIN_RESOLVED_SLOT_{synthetic_index}", "key": key})
    health_hints = _ea_onemin_slot_health_hints()
    if health_hints:
        slots.sort(key=lambda slot: _slot_health_rank(slot=slot, health_hints=health_hints), reverse=True)
    return slots


def _ea_onemin_slot_health_hints() -> dict[str, dict[str, object]]:
    global _ONEMIN_SLOT_HINTS_CACHE
    if isinstance(_ONEMIN_SLOT_HINTS_CACHE, dict):
        return _ONEMIN_SLOT_HINTS_CACHE
    worker_path = EA_ROOT / "scripts" / "chummer6_guide_media_worker.py"
    if not worker_path.exists():
        _ONEMIN_SLOT_HINTS_CACHE = {}
        return _ONEMIN_SLOT_HINTS_CACHE
    try:
        spec = importlib.util.spec_from_file_location("ea_chummer6_slot_hints", worker_path)
        if spec is None or spec.loader is None:
            _ONEMIN_SLOT_HINTS_CACHE = {}
            return _ONEMIN_SLOT_HINTS_CACHE
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        loader = getattr(module, "_onemin_slot_health_hints", None)
        hints = loader() if callable(loader) else {}
        _ONEMIN_SLOT_HINTS_CACHE = hints if isinstance(hints, dict) else {}
    except Exception:
        _ONEMIN_SLOT_HINTS_CACHE = {}
    return _ONEMIN_SLOT_HINTS_CACHE


def _slot_health_rank(*, slot: dict[str, str], health_hints: dict[str, dict[str, object]]) -> tuple[int, float, int, str]:
    env_name = str(slot.get("env_name") or "").strip()
    hint = health_hints.get(env_name) if isinstance(health_hints, dict) else None
    if not isinstance(hint, dict):
        return (0, -1.0, 0, env_name)
    state = str(hint.get("state") or "").strip().lower()
    slot_role = str(hint.get("slot_role") or "").strip().lower()
    credits = -1.0
    for key in ("billing_remaining_credits", "estimated_remaining_credits", "remaining_credits"):
        value = hint.get(key)
        if value in (None, ""):
            continue
        try:
            credits = float(value)
            break
        except Exception:
            continue
    state_rank = {
        "ready": 4,
        "active": 3,
        "cooldown": 2,
        "unknown": 1,
        "quarantine": 0,
    }.get(state, 1)
    role_rank = 1 if slot_role != "reserve" else 0
    return (state_rank, credits, role_rank, env_name)


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


def _split_prompt_sentences(prompt: str) -> list[str]:
    cleaned = " ".join(str(prompt or "").split()).strip()
    if not cleaned:
        return []
    raw_sentences = re.split(r"(?<=[.!?])\s+|(?<=:)\s+(?=[A-Z])", cleaned)
    sentences: list[str] = []
    seen: set[str] = set()
    for raw in raw_sentences:
        sentence = " ".join(str(raw or "").split()).strip(" ,;")
        if len(sentence) < 12:
            continue
        lowered = sentence.lower()
        if lowered in seen:
            continue
        seen.add(lowered)
        sentences.append(sentence)
    return sentences


def _sentence_priority(sentence: str) -> int:
    lowered = sentence.lower()
    score = 0
    if any(token in lowered for token in ("wide", "ultra-wide", "establishing shot", "camera several meters back", "room", "environment", "apparatus", "frame")):
        score += 6
    if any(token in lowered for token in ("shadowrun", "runner-life", "cyberpunk-fantasy", "cover-grade", "poster", "flagship")):
        score += 5
    if any(token in lowered for token in ("one clear focal subject", "set the scene", "show this happening", "core visual metaphor", "keep the whole")):
        score += 4
    if any(token in lowered for token in ("no readable", "do not print text", "never render", "unreadable", "avoid")):
        score += 3
    if any(token in lowered for token in ("overlay", "diagnostic", "approval", "provenance", "rollback", "attribute rail")):
        score += 2
    return score


def _onemin_prompt_char_limit() -> int:
    raw = str(os.environ.get("CHUMMER_MEDIA_FACTORY_ONEMIN_PROMPT_CHAR_LIMIT") or "2200").strip()
    try:
        return max(512, min(3900, int(float(raw))))
    except Exception:
        return 2200


def _prepare_onemin_prompt(prompt: str) -> str:
    # Keep the strongest scene instructions and drop repeated policy prose
    # before we hit upstream prompt-length and comprehension ceilings.
    limit = _onemin_prompt_char_limit()
    cleaned = " ".join(str(prompt or "").split()).strip()
    if len(cleaned) <= limit:
        return cleaned
    sentences = _split_prompt_sentences(cleaned)
    if not sentences:
        return _clip_prompt_text(cleaned, limit=limit)
    selected: list[str] = []
    used: set[str] = set()

    def _try_add(sentence: str) -> None:
        normalized = sentence.strip()
        lowered = normalized.lower()
        if not normalized or lowered in used:
            return
        candidate = " ".join([*selected, normalized]).strip()
        if len(candidate) > limit:
            return
        used.add(lowered)
        selected.append(normalized)

    _try_add(sentences[0])
    ranked = sorted(
        enumerate(sentences[1:], start=1),
        key=lambda item: (-_sentence_priority(item[1]), item[0]),
    )
    for _, sentence in ranked:
        _try_add(sentence)
    condensed = " ".join(selected).strip()
    if condensed:
        return condensed
    return _clip_prompt_text(cleaned, limit=limit)


def _default_quality(*, prompt: str, model: str) -> str:
    explicit_factory = str(os.environ.get("CHUMMER_MEDIA_FACTORY_ONEMIN_IMAGE_QUALITY") or "").strip().lower()
    if explicit_factory:
        return explicit_factory
    explicit_generic = str(os.environ.get("CHUMMER6_ONEMIN_IMAGE_QUALITY") or "").strip().lower()
    normalized = _normalized_onemin_model(model)
    if explicit_generic and not _prompt_looks_flagship(prompt):
        return explicit_generic
    if explicit_generic and explicit_generic not in {"low", "auto"}:
        return explicit_generic
    if _prompt_looks_flagship(prompt):
        if normalized.startswith("gpt-image-") or normalized.startswith("dall-e-"):
            return "high"
        return "medium"
    if normalized.startswith("gpt-image-1-mini"):
        return "medium"
    if normalized.startswith("gpt-image-") or normalized.startswith("dall-e-"):
        return "high"
    return "medium"


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
    if backend in {"openai_edits", "openai_edit", "openai"}:
        return "openai_edits"
    if backend in {"disabled", "off", "none"}:
        return "disabled"
    return backend


def _openai_api_key() -> str:
    return (
        str(os.environ.get("CHUMMER_MEDIA_FACTORY_OPENAI_API_KEY") or "").strip()
        or str(os.environ.get("OPENAI_API_KEY") or "").strip()
        or str(os.environ.get("EA_OPENAI_API_KEY") or "").strip()
    )


def _openai_timeout_seconds() -> int:
    raw = str(os.environ.get("CHUMMER_MEDIA_FACTORY_OPENAI_TIMEOUT_SECONDS") or "180").strip()
    try:
        return max(30, min(600, int(float(raw))))
    except Exception:
        return 180


def _multipart_formdata(
    *,
    fields: list[tuple[str, str]],
    files: list[tuple[str, str, bytes, str]],
) -> tuple[bytes, str]:
    boundary = f"----ChummerMediaFactory{uuid.uuid4().hex}"
    chunks: list[bytes] = []
    for name, value in fields:
        chunks.extend(
            [
                f"--{boundary}\r\n".encode("utf-8"),
                f'Content-Disposition: form-data; name="{name}"\r\n\r\n'.encode("utf-8"),
                str(value).encode("utf-8"),
                b"\r\n",
            ]
        )
    for name, filename, content, content_type in files:
        chunks.extend(
            [
                f"--{boundary}\r\n".encode("utf-8"),
                f'Content-Disposition: form-data; name="{name}"; filename="{filename}"\r\n'.encode("utf-8"),
                f"Content-Type: {content_type}\r\n\r\n".encode("utf-8"),
                content,
                b"\r\n",
            ]
        )
    chunks.append(f"--{boundary}--\r\n".encode("utf-8"))
    return b"".join(chunks), boundary


def _openai_size(width: int, height: int) -> str:
    if width == height:
        return "1024x1024"
    return "1536x1024" if width >= height else "1024x1536"


def _render_with_openai_edits(
    *,
    prompt: str,
    output_path: Path,
    width: int,
    height: int,
    reference_image: Path,
) -> dict[str, object]:
    api_key = _openai_api_key()
    if not api_key:
        raise RuntimeError("media_factory:openai_edits_not_configured")
    if not reference_image.exists():
        raise RuntimeError(f"media_factory:missing_reference_image:{reference_image}")
    fields = [
        ("model", str(os.environ.get("CHUMMER_MEDIA_FACTORY_OPENAI_EDIT_MODEL") or "gpt-image-1").strip() or "gpt-image-1"),
        ("prompt", str(prompt or "").strip()),
        ("size", _openai_size(width, height)),
        ("quality", str(os.environ.get("CHUMMER_MEDIA_FACTORY_OPENAI_EDIT_QUALITY") or "high").strip() or "high"),
        ("background", "opaque"),
    ]
    payload, boundary = _multipart_formdata(
        fields=fields,
        files=[("image[]", reference_image.name, reference_image.read_bytes(), "image/png")],
    )
    request = urllib.request.Request(
        "https://api.openai.com/v1/images/edits",
        headers={
            "Authorization": f"Bearer {api_key}",
            "Content-Type": f"multipart/form-data; boundary={boundary}",
            "User-Agent": "Chummer-Media-Factory/1.0",
        },
        data=payload,
        method="POST",
    )
    try:
        with urllib.request.urlopen(request, timeout=_openai_timeout_seconds()) as response:
            data = response.read().decode("utf-8", errors="replace")
    except urllib.error.HTTPError as exc:
        body = exc.read().decode("utf-8", errors="replace").strip()
        raise RuntimeError(f"media_factory:openai_edits:http_{exc.code}:{body[:180]}") from exc
    except urllib.error.URLError as exc:
        raise RuntimeError(f"media_factory:openai_edits:urlerror:{exc.reason}") from exc
    except TimeoutError as exc:
        raise RuntimeError("media_factory:openai_edits:timeout") from exc
    try:
        body = json.loads(data)
    except Exception as exc:
        raise RuntimeError(f"media_factory:openai_edits:non_json_response:{data[:180]}") from exc
    first = dict(((body.get("data") or [None])[0]) or {})
    b64_json = str(first.get("b64_json") or "").strip()
    if b64_json:
        output_path.parent.mkdir(parents=True, exist_ok=True)
        output_path.write_bytes(base64.b64decode(b64_json))
        return {
            "tool_name": "provider.openai.image_edit",
            "action_kind": "image.edit",
            "receipt_json": {
                "provider_key": "openai_edits",
                "provider_backend": "openai",
                "model": str(fields[0][1]),
            },
            "output_json": {"asset_urls": [], "preview_text": "b64_json"},
        }
    asset_urls = _collect_asset_urls(body)
    if asset_urls:
        _download_asset(asset_urls[0], output_path)
        return {
            "tool_name": "provider.openai.image_edit",
            "action_kind": "image.edit",
            "receipt_json": {
                "provider_key": "openai_edits",
                "provider_backend": "openai",
                "model": str(fields[0][1]),
            },
            "output_json": {"asset_urls": asset_urls, "preview_text": asset_urls[0]},
        }
    raise RuntimeError("media_factory:openai_edits:no_asset_urls")


def _size_candidates(model: str, *, width: int, height: int) -> list[str]:
    configured = str(os.environ.get("CHUMMER6_ONEMIN_IMAGE_SIZE") or "").strip().lower()
    if configured and configured != "auto":
        return [configured]
    landscape = width > height
    square = width == height
    if square:
        return ["1024x1024"]
    normalized = _normalized_onemin_model(model)
    if _is_flux_schnell_model(model):
        return [_aspect_ratio(width, height)]
    if normalized.startswith("gpt-image-") or normalized.startswith("dall-e-"):
        return ["auto", "1536x1024", "1024x1024"] if landscape else ["auto", "1024x1536", "1024x1024"]
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


def _estimate_onemin_image_credits(*, width: int, height: int, model: str | None = None) -> int:
    raw = str(os.environ.get("CHUMMER6_ONEMIN_ESTIMATED_IMAGE_CREDITS") or "").strip()
    if raw:
        try:
            return max(0, int(float(raw)))
        except Exception:
            pass
    selected_model = _normalized_onemin_model(model or (_model_candidates()[0] if _model_candidates() else ""))
    if selected_model == "black-forest-labs/flux-schnell":
        return 9000
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
    def _candidate_has_known_budget(candidate: dict[str, object]) -> bool:
        for key in ("billing_remaining_credits", "estimated_remaining_credits", "remaining_credits"):
            value = candidate.get(key)
            if value not in (None, ""):
                return True
        return False

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
            if lease is None and not any(_candidate_has_known_budget(candidate) for candidate in candidate_pool):
                # Freshly seeded local manager runs often see ready slots before
                # any balance probe snapshots exist. In that state every
                # candidate looks like zero-credit capacity, even when the pool
                # is healthy. Retry with a zero estimated budget so unknown but
                # ready slots remain usable; the upstream 1min call still
                # enforces actual credit limits.
                lease = manager.reserve_for_candidates(
                    candidates=candidate_pool,
                    lane="image",
                    capability="image_generate",
                    principal_id=principal_id,
                    request_id=request_id,
                    estimated_credits=0,
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
    if _is_flux_schnell_model(model):
        prompt_object: dict[str, object] = {
            "prompt": prompt,
            "aspect_ratio": str(os.environ.get("CHUMMER6_ONEMIN_ASPECT_RATIO") or aspect_ratio or "1:1").strip() or "1:1",
            "num_inference_steps": int(str(os.environ.get("CHUMMER6_ONEMIN_FLUX_SCHNELL_STEPS") or "4").strip() or "4"),
            "go_fast": str(os.environ.get("CHUMMER6_ONEMIN_FLUX_SCHNELL_GO_FAST") or "1").strip().lower() not in {"0", "false", "no", "off"},
            "megapixels": str(os.environ.get("CHUMMER6_ONEMIN_FLUX_SCHNELL_MEGAPIXELS") or "1").strip() or "1",
            "output_quality": int(str(os.environ.get("CHUMMER6_ONEMIN_FLUX_SCHNELL_OUTPUT_QUALITY") or "80").strip() or "80"),
        }
        return {
            "type": "IMAGE_GENERATOR",
            "model": model,
            "promptObject": prompt_object,
        }
    prompt_object: dict[str, object] = {
        "prompt": prompt,
        "n": 1,
        "quality": quality,
        "output_format": "png",
        "background": "opaque",
    }
    if _normalized_onemin_model(model).startswith("gpt-image-"):
        prompt_object["style"] = str(os.environ.get("CHUMMER_MEDIA_FACTORY_ONEMIN_IMAGE_STYLE") or "natural").strip() or "natural"
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
    model_candidates: list[str],
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
        "requested_model_candidates": list(model_candidates),
        "tool_name": str(result_json.get("tool_name") or "provider.onemin.image_generate"),
        "action_kind": str(result_json.get("action_kind") or "image.generate"),
        "receipt_json": dict(result_json.get("receipt_json") or {}),
        "output_json": dict(result_json.get("output_json") or {}),
    }
    receipt_path.write_text(json.dumps(payload, indent=2, ensure_ascii=True) + "\n", encoding="utf-8")
    return receipt_path


def render_asset(
    *,
    prompt: str,
    output_path: Path,
    width: int,
    height: int,
    dry_run: bool = False,
    reference_image: Path | None = None,
) -> dict[str, object]:
    _seed_runtime_env()
    render_id = f"mf-{uuid.uuid4().hex}"
    backend_provider = _selected_backend()
    image_execution_enabled = _image_execution_enabled()
    manager_principal_id = _manager_principal_id()
    manager_allow_reserve = _manager_allow_reserve()
    family = _asset_family_for_output_path(output_path)
    watchdog_seconds = _render_watchdog_seconds(backend_provider)
    started_at = monotonic()
    submitted_prompt = _prepare_onemin_prompt(prompt)
    model_candidates = _model_candidates(submitted_prompt)
    selected_model = model_candidates[0] if model_candidates else ""
    quality = _default_quality(prompt=submitted_prompt, model=selected_model)
    payload = {
        "prompt": submitted_prompt,
        "aspect_ratio": _aspect_ratio(width, height),
        "quality": quality,
        "model": selected_model,
        "output_format": "png",
        "manager_allow_reserve": manager_allow_reserve,
        "requested_prompt_length": len(str(prompt or "")),
        "submitted_prompt_length": len(submitted_prompt),
        "submitted_prompt_char_limit": _onemin_prompt_char_limit(),
        "reference_image_path": str(reference_image) if isinstance(reference_image, Path) else "",
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
            "reference_image_path": str(reference_image) if isinstance(reference_image, Path) else "",
            "payload_json": payload,
        }
    if not image_execution_enabled or backend_provider == "disabled":
        raise RuntimeError("media_factory:rendering_disabled")

    _write_attempt_status(
        render_id=render_id,
        phase="starting",
        backend_provider=backend_provider,
        output_path=output_path,
        family=family,
        requested_prompt=str(prompt or ""),
        submitted_prompt=submitted_prompt,
        width=width,
        height=height,
        detail="render_started",
        reference_image=reference_image,
    )

    if backend_provider == "openai_edits":
        try:
            result_json = _render_with_openai_edits(
                prompt=submitted_prompt,
                output_path=output_path,
                width=width,
                height=height,
                reference_image=reference_image or Path(""),
            )
            receipt_path = _write_receipt(
                render_id=render_id,
                requested_prompt=str(prompt or ""),
                submitted_prompt=submitted_prompt,
                output_path=output_path,
                width=width,
                height=height,
                backend_provider=backend_provider,
                quality=quality,
                model_candidates=model_candidates,
                manager_principal_id=manager_principal_id,
                manager_allow_reserve=manager_allow_reserve,
                result_json=result_json,
            )
            _write_attempt_status(
                render_id=render_id,
                phase="completed",
                backend_provider=backend_provider,
                output_path=output_path,
                family=family,
                requested_prompt=str(prompt or ""),
                submitted_prompt=submitted_prompt,
                width=width,
                height=height,
                model=str((result_json.get("receipt_json") or {}).get("model") or ""),
                detail="rendered",
                reference_image=reference_image,
            )
            _record_health_attempt(backend=backend_provider, family=family, detail="rendered", ok=True)
            asset_urls = list((result_json.get("output_json") or {}).get("asset_urls") or [])
            return {
                "render_id": render_id,
                "provider": "media_factory",
                "backend_provider": backend_provider,
                "manager_principal_id": manager_principal_id,
                "manager_allow_reserve": manager_allow_reserve,
                "manager_reservation_source": "direct_openai",
                "provider_account_name": "openai",
                "provider_key_slot": "OPENAI_API_KEY",
                "output_path": str(output_path),
                "receipt_path": str(receipt_path),
                "asset_url": asset_urls[0] if asset_urls else "",
            }
        except Exception as exc:
            detail = str(exc)
            _write_attempt_status(
                render_id=render_id,
                phase="failed",
                backend_provider=backend_provider,
                output_path=output_path,
                family=family,
                requested_prompt=str(prompt or ""),
                submitted_prompt=submitted_prompt,
                width=width,
                height=height,
                detail=detail,
                reference_image=reference_image,
            )
            _record_health_attempt(backend=backend_provider, family=family, detail=detail, ok=False)
            raise

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
        detail = "media_factory:onemin_manager_capacity_unavailable"
        _write_attempt_status(
            render_id=render_id,
            phase="failed",
            backend_provider=backend_provider,
            output_path=output_path,
            family=family,
            requested_prompt=str(prompt or ""),
            submitted_prompt=submitted_prompt,
            width=width,
            height=height,
            detail=detail,
            reference_image=reference_image,
        )
        _record_health_attempt(backend=backend_provider, family=family, detail=detail, ok=False)
        raise RuntimeError(detail)

    lease_id = str(reservation.get("lease_id") or "").strip()
    secret_env_name = str(reservation.get("secret_env_name") or "").strip()
    reserved_account_id = str(reservation.get("account_id") or reservation.get("account_name") or "").strip() or "onemin_unknown"
    reserved_slot_name = str(reservation.get("slot_name") or secret_env_name or "").strip() or "unknown"
    api_key = str(os.environ.get(secret_env_name) or "").strip()
    errors: list[str] = []
    if not api_key:
        detail = f"media_factory:reserved_slot_missing_local_key:{secret_env_name or 'unknown'}"
        _release_onemin_image_slot(
            lease_id=lease_id,
            principal_id=manager_principal_id,
            status="failed",
            error=f"reserved_slot_missing_local_key:{secret_env_name or 'unknown'}",
        )
        _write_attempt_status(
            render_id=render_id,
            phase="failed",
            backend_provider=backend_provider,
            output_path=output_path,
            family=family,
            requested_prompt=str(prompt or ""),
            submitted_prompt=submitted_prompt,
            width=width,
            height=height,
            detail=detail,
            reference_image=reference_image,
        )
        _record_health_attempt(backend=backend_provider, family=family, detail=detail, ok=False)
        raise RuntimeError(detail)

    slot_candidates: list[dict[str, str]] = [
        {
            "env_name": secret_env_name,
            "key": api_key,
            "provider_account_name": reserved_account_id,
            "provider_key_slot": reserved_slot_name,
            "reserved": "1",
        }
    ]
    for slot in _configured_onemin_slots():
        env_name = str(slot.get("env_name") or "").strip()
        key = str(slot.get("key") or "").strip()
        if not env_name or not key:
            continue
        if env_name == secret_env_name or key == api_key:
            continue
        slot_candidates.append(
            {
                "env_name": env_name,
                "key": key,
                "provider_account_name": env_name,
                "provider_key_slot": env_name,
                "reserved": "",
            }
        )

    result_json: dict[str, object] | None = None
    try:
        for slot_candidate in slot_candidates:
            if monotonic() - started_at > float(watchdog_seconds):
                errors.append(f"watchdog:{watchdog_seconds}s")
                break
            current_api_key = str(slot_candidate.get("key") or "").strip()
            current_account_name = str(slot_candidate.get("provider_account_name") or "").strip() or reserved_account_id
            current_slot_name = str(slot_candidate.get("provider_key_slot") or "").strip() or reserved_slot_name
            current_is_reserved = str(slot_candidate.get("reserved") or "").strip() == "1"
            for model in model_candidates:
                if monotonic() - started_at > float(watchdog_seconds):
                    errors.append(f"watchdog:{watchdog_seconds}s")
                    break
                for size in _size_candidates(model, width=width, height=height):
                    if monotonic() - started_at > float(watchdog_seconds):
                        errors.append(f"watchdog:{watchdog_seconds}s")
                        break
                    _write_attempt_status(
                        render_id=render_id,
                        phase="requesting",
                        backend_provider=backend_provider,
                        output_path=output_path,
                        family=family,
                        requested_prompt=str(prompt or ""),
                        submitted_prompt=submitted_prompt,
                        width=width,
                        height=height,
                        model=model,
                        size=size,
                        detail=f"{current_slot_name}:requesting",
                        reference_image=reference_image,
                    )
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
                            "API-KEY": current_api_key,
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
                        errors.append(f"{current_slot_name}:{model}:{size}:http_{exc.code}:{body[:180]}")
                        continue
                    except urllib.error.URLError as exc:
                        errors.append(f"{current_slot_name}:{model}:{size}:urlerror:{exc.reason}")
                        continue
                    except TimeoutError:
                        errors.append(f"{current_slot_name}:{model}:{size}:timeout")
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
                            errors.append(f"{current_slot_name}:{model}:{size}:non_json_response:{decoded[:180]}")
                            continue
                        asset_urls = _collect_asset_urls(body)
                        if not asset_urls:
                            errors.append(f"{current_slot_name}:{model}:{size}:no_asset_urls")
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
                            "provider_account_name": current_account_name,
                            "provider_key_slot": current_slot_name,
                            "model": model,
                            "feature_type": "IMAGE_GENERATOR",
                            "tool_version": "v1",
                            "manager_lease_id": lease_id if current_is_reserved else "",
                        },
                        "output_json": {
                            "asset_urls": asset_urls,
                            "preview_text": preview_text,
                            "provider_account_name": current_account_name,
                            "provider_key_slot": current_slot_name,
                            "model": model,
                        },
                    }
                    break
                if result_json is not None:
                    break
            if result_json is not None:
                break
            if current_is_reserved and lease_id:
                _release_onemin_image_slot(
                    lease_id=lease_id,
                    principal_id=manager_principal_id,
                    status="failed",
                    error="reserved_slot_insufficient_or_failed",
                )
                _release_onemin_image_slot_locally(
                    manager=local_manager,
                    lease_id=lease_id,
                    status="failed",
                    error="reserved_slot_insufficient_or_failed",
                )
                lease_id = ""

        if result_json is None:
            detail = "media_factory:" + " || ".join(errors[:6])
            _write_attempt_status(
                render_id=render_id,
                phase="failed",
                backend_provider=backend_provider,
                output_path=output_path,
                family=family,
                requested_prompt=str(prompt or ""),
                submitted_prompt=submitted_prompt,
                width=width,
                height=height,
                detail=detail,
                reference_image=reference_image,
            )
            _record_health_attempt(backend=backend_provider, family=family, detail=detail, ok=False)
            raise RuntimeError(detail)

        if lease_id:
            _release_onemin_image_slot(
                lease_id=lease_id,
                principal_id=manager_principal_id,
                status="released",
                actual_credits_delta=_estimate_onemin_image_credits(
                    width=width,
                    height=height,
                    model=str((result_json.get("receipt_json") or {}).get("model") or ""),
                ),
            )
            _release_onemin_image_slot_locally(
                manager=local_manager,
                lease_id=lease_id,
                status="released",
                actual_credits_delta=_estimate_onemin_image_credits(
                    width=width,
                    height=height,
                    model=str((result_json.get("receipt_json") or {}).get("model") or ""),
                ),
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
            model_candidates=model_candidates,
            manager_principal_id=manager_principal_id,
            manager_allow_reserve=manager_allow_reserve,
            result_json=result_json,
        )
        asset_urls = list((result_json.get("output_json") or {}).get("asset_urls") or [])
        _write_attempt_status(
            render_id=render_id,
            phase="completed",
            backend_provider=backend_provider,
            output_path=output_path,
            family=family,
            requested_prompt=str(prompt or ""),
            submitted_prompt=submitted_prompt,
            width=width,
            height=height,
            model=str((result_json.get("receipt_json") or {}).get("model") or ""),
            detail="rendered",
            reference_image=reference_image,
        )
        _record_health_attempt(backend=backend_provider, family=family, detail="rendered", ok=True)
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
    parser.add_argument("--reference-image")
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    payload = render_asset(
        prompt=str(args.prompt),
        output_path=Path(args.output),
        width=int(args.width),
        height=int(args.height),
        dry_run=bool(args.dry_run),
        reference_image=Path(args.reference_image) if str(args.reference_image or "").strip() else None,
    )
    print(json.dumps(payload, ensure_ascii=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
