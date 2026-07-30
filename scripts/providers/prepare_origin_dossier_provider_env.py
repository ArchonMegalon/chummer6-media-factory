#!/usr/bin/env python3
"""Create a least-privilege local env file for the Origin Dossier worker."""

from __future__ import annotations

import argparse
import json
import os
import re
import tempfile
import urllib.parse
import urllib.request
from pathlib import Path


EXACT_KEYS = {
    "CHUMMER_EA_MAGICFIT_EMAIL",
    "CHUMMER_EA_MAGICFIT_PASSWORD",
    "CHUMMER_MEDIA_FACTORY_MAGICFIT_EMAIL",
    "CHUMMER_MEDIA_FACTORY_MAGICFIT_PASSWORD",
    "MAGICFIT_EMAIL",
    "MAGICFIT_PASSWORD",
    "CHUMMER_MEDIA_FACTORY_UNMIXR_API_KEY",
    "UNMIXR_API_KEY",
    "UNMIXR_API_KEYS",
    "UNMIXR_SPEAKING_RATE",
    "UNMIXR_SPEAKING_PITCH",
    "UNMIXR_SPEAKING_VOLUME",
}
FALLBACK_KEY = re.compile(
    r"^(?:CHUMMER_MEDIA_FACTORY_)?UNMIXR_API_KEY_FALLBACK_[0-9]+$"
)
VOICE_LABELS = {
    "voice-noir": "Gavin",
    "voice-noir-01": "Gavin",
    "voice-wire": "Cora",
    "voice-street": "Adam",
    "voice-street-02": "Adam",
    "voice-warm-03": "Samuel",
}


def parse_env(path: Path) -> dict[str, str]:
    values: dict[str, str] = {}
    for raw_line in path.read_text(encoding="utf-8").splitlines():
        line = raw_line.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        key, value = line.split("=", 1)
        key = key.strip()
        value = value.strip()
        if key in EXACT_KEYS or FALLBACK_KEY.fullmatch(key):
            normalized = value.strip("'").strip('"')
            if normalized:
                values[key] = normalized
    return values


def has_magicfit(values: dict[str, str]) -> bool:
    email = any(
        key in values
        for key in (
            "CHUMMER_MEDIA_FACTORY_MAGICFIT_EMAIL",
            "CHUMMER_EA_MAGICFIT_EMAIL",
            "MAGICFIT_EMAIL",
        )
    )
    password = any(
        key in values
        for key in (
            "CHUMMER_MEDIA_FACTORY_MAGICFIT_PASSWORD",
            "CHUMMER_EA_MAGICFIT_PASSWORD",
            "MAGICFIT_PASSWORD",
        )
    )
    return email and password


def has_unmixr(values: dict[str, str]) -> bool:
    return any(
        key in values
        for key in (
            "CHUMMER_MEDIA_FACTORY_UNMIXR_API_KEY",
            "UNMIXR_API_KEY",
            "UNMIXR_API_KEYS",
        )
    ) or any(FALLBACK_KEY.fullmatch(key) for key in values)


def discover_voice_map(values: dict[str, str]) -> dict[str, str]:
    api_key = (
        values.get("CHUMMER_MEDIA_FACTORY_UNMIXR_API_KEY")
        or values.get("UNMIXR_API_KEY")
        or ""
    )
    if not api_key:
        raise SystemExit("origin_dossier_provider_env_primary_unmixr_key_missing")
    query = urllib.parse.urlencode(
        {
            "page_size": "100",
            "fields": "uuid,character,language,is_available",
            "c": "audiobook-voices",
        }
    )
    request = urllib.request.Request(
        f"https://unmixr.com/api/v1/voice-list/?{query}",
        headers={
            "Authorization": f"Bearer {api_key}",
            "Accept": "application/json",
        },
        method="GET",
    )
    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            payload = json.loads(response.read().decode("utf-8"))
    except Exception as error:
        raise SystemExit("origin_dossier_provider_env_voice_discovery_failed") from error
    rows = payload.get("results") if isinstance(payload, dict) else None
    if not isinstance(rows, list):
        raise SystemExit("origin_dossier_provider_env_voice_catalog_invalid")

    by_label: dict[str, str] = {}
    for row in rows:
        if not isinstance(row, dict) or row.get("is_available") is False:
            continue
        label = str(row.get("character") or "").strip()
        voice_id = str(row.get("uuid") or "").strip()
        language = str(row.get("language") or "").strip().lower()
        if label and voice_id and language == "en-us":
            by_label.setdefault(label, voice_id)
    resolved = {
        alias: by_label[label]
        for alias, label in VOICE_LABELS.items()
        if label in by_label
    }
    if set(resolved) != set(VOICE_LABELS):
        raise SystemExit("origin_dossier_provider_env_voice_map_incomplete")
    return resolved


def write_private_env(path: Path, values: dict[str, str]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
    file_descriptor, temporary_name = tempfile.mkstemp(
        prefix=path.name + ".",
        suffix=".tmp",
        dir=path.parent,
        text=True,
    )
    try:
        os.fchmod(file_descriptor, 0o600)
        with os.fdopen(file_descriptor, "w", encoding="utf-8") as stream:
            for key in sorted(values):
                stream.write(f"{key}={values[key]}\n")
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary_name, path)
        os.chmod(path, 0o600)
    except BaseException:
        try:
            os.unlink(temporary_name)
        except FileNotFoundError:
            pass
        raise


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    arguments = parser.parse_args()

    source = arguments.source.expanduser().resolve()
    output = arguments.output.expanduser().resolve()
    if not source.is_file():
        raise SystemExit("origin_dossier_provider_env_source_missing")
    values = parse_env(source)
    if not has_magicfit(values):
        raise SystemExit("origin_dossier_provider_env_magicfit_missing")
    if not has_unmixr(values):
        raise SystemExit("origin_dossier_provider_env_unmixr_missing")
    values["CHUMMER_MEDIA_FACTORY_UNMIXR_VOICE_MAP_JSON"] = json.dumps(
        discover_voice_map(values),
        separators=(",", ":"),
        sort_keys=True,
    )
    write_private_env(output, values)
    print(
        json.dumps(
            {
                "contractVersion": "chummer.origin_dossier_provider_env_prepare.v1",
                "status": "ready",
                "output": str(output),
                "credentialNames": sorted(values),
                "credentialCount": len(values),
            },
            sort_keys=True,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
