#!/usr/bin/env python3
from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import shutil
import subprocess
import time
from pathlib import Path

import requests
from playwright.sync_api import TimeoutError as PlaywrightTimeoutError
from playwright.sync_api import sync_playwright


ROOT = Path(__file__).resolve().parents[2]
HOME_URL = "https://magicfit.pushowl.com/home"
VIDEO_URL = "https://magicfit.pushowl.com/agents/generate?mode=video"
VIDEO_URL_RE = re.compile(
    r"https://(?:cdn\.pushowl\.com|media\.powlcdn\.com)/magicfit/"
    r"[^\"'\s<>]+?\.(?:mp4|webm)(?:[^\"'\s<>]*)?"
)
NEGATIVE = ", ".join(
    (
        "no storyboard",
        "no slideshow",
        "no unrelated montage",
        "no character redesign",
        "no abrupt cut",
        "no cartoon",
        "no visible text",
        "no logo",
        "no watermark",
        "no broken geometry",
    )
)


def load_env_file(path: Path) -> None:
    if not path.is_file():
        return
    for raw in path.read_text(encoding="utf-8").splitlines():
        line = raw.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        key, value = line.split("=", 1)
        os.environ.setdefault(key.strip(), value.strip().strip("'").strip('"'))


def load_env() -> None:
    load_env_file(ROOT / ".env")
    configured = str(
        os.environ.get("CHUMMER_MEDIA_FACTORY_MAGICFIT_ENV_FILE") or ""
    ).strip()
    if configured:
        load_env_file(Path(configured).expanduser())


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Render one selected Origin Dossier scene with MagicFit."
    )
    parser.add_argument("--prompt", required=True)
    parser.add_argument("--out", required=True)
    parser.add_argument("--duration", type=int, default=10)
    parser.add_argument("--aspect-label", default="Landscape (16:9)")
    parser.add_argument("--timeout-minutes", type=int, default=18)
    parser.add_argument("--model-label", default="")
    parser.add_argument(
        "--first-frame",
        default="",
        help="Optional continuity frame from the immediately preceding shot.",
    )
    parser.add_argument("--state-json", required=True)
    return parser.parse_args()


def provider_duration(seconds: int) -> int:
    allowed = (4, 6, 8, 10, 12, 15)
    return min(allowed, key=lambda candidate: (abs(candidate - seconds), -candidate))


def collect_urls(text: str) -> list[str]:
    return list(
        dict.fromkeys(
            url.replace("\\u0026", "&").rstrip("),]")
            for url in VIDEO_URL_RE.findall(text or "")
        )
    )


def url_timestamp(url: str) -> int:
    match = re.search(r"/magicfit/(\d+)-", url)
    return int(match.group(1)) if match else 0


def choose_newest(
    urls: set[str], baseline: set[str], submitted_at_ms: int
) -> str:
    candidates: list[tuple[int, str]] = []
    for url in urls:
        if url in baseline or "/ik-thumbnail." in url:
            continue
        timestamp = url_timestamp(url)
        if timestamp and timestamp < submitted_at_ms - 120_000:
            continue
        candidates.append((timestamp, url))
    candidates.sort(key=lambda item: item[0], reverse=True)
    return candidates[0][1] if candidates else ""


def download(url: str, output: Path) -> None:
    response = requests.get(url, timeout=120, stream=True)
    response.raise_for_status()
    output.parent.mkdir(parents=True, exist_ok=True)
    with output.open("wb") as handle:
        for chunk in response.iter_content(chunk_size=128 * 1024):
            if chunk:
                handle.write(chunk)


def probe_duration(path: Path) -> float | None:
    ffprobe = shutil.which("ffprobe")
    if not ffprobe:
        return None
    try:
        completed = subprocess.run(
            [
                ffprobe,
                "-v",
                "error",
                "-show_entries",
                "format=duration",
                "-of",
                "default=noprint_wrappers=1:nokey=1",
                str(path),
            ],
            capture_output=True,
            text=True,
            timeout=30,
            check=False,
        )
        duration = (
            float(completed.stdout.strip()) if completed.returncode == 0 else 0.0
        )
    except (OSError, subprocess.SubprocessError, ValueError):
        return None
    return round(duration, 3) if duration > 0 else None


def maybe_login(page) -> None:
    page.goto(HOME_URL, wait_until="domcontentloaded", timeout=120_000)
    page.wait_for_timeout(4_000)
    body = page.locator("body").inner_text(timeout=10_000)
    if not re.search(r"login|sign in|email|password", body, re.I):
        return
    email = str(
        os.environ.get("CHUMMER_MEDIA_FACTORY_MAGICFIT_EMAIL")
        or os.environ.get("CHUMMER_EA_MAGICFIT_EMAIL")
        or os.environ.get("MAGICFIT_EMAIL")
        or ""
    ).strip()
    password = str(
        os.environ.get("CHUMMER_MEDIA_FACTORY_MAGICFIT_PASSWORD")
        or os.environ.get("CHUMMER_EA_MAGICFIT_PASSWORD")
        or os.environ.get("MAGICFIT_PASSWORD")
        or ""
    ).strip()
    if not email or not password:
        raise RuntimeError("origin_dossier_media_magicfit_credentials_missing")
    email_field = page.locator(
        "input[type=email], input[name*=email i], input[placeholder*=email i]"
    ).first
    if email_field.count():
        email_field.fill(email)
    password_field = page.locator("input[type=password]").first
    if password_field.count():
        password_field.fill(password)
    submit = page.get_by_role(
        "button", name=re.compile(r"sign in|login|continue|submit", re.I)
    ).first
    if not submit.count():
        raise RuntimeError("origin_dossier_media_magicfit_login_submit_missing")
    submit.click()
    page.wait_for_load_state("domcontentloaded")
    page.wait_for_timeout(8_000)


def select_button(page, current_text: str | re.Pattern[str], option_text: str) -> None:
    try:
        page.get_by_role("button", name=current_text).last.click(timeout=10_000)
        page.wait_for_timeout(500)
        page.get_by_text(option_text, exact=True).last.click(timeout=10_000)
        page.wait_for_timeout(500)
    except PlaywrightTimeoutError:
        return


def file_sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(128 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def attach_first_frame(page, first_frame: Path) -> None:
    target = page.get_by_role(
        "button", name=re.compile(r"^\s*First Frame\s*$", re.I)
    ).first
    target.wait_for(timeout=15_000)
    target.click(timeout=15_000)
    dialog = page.get_by_role("dialog", name=re.compile(r"Select Image", re.I))
    dialog.wait_for(timeout=15_000)
    upload_target = dialog.get_by_role(
        "button",
        name=re.compile(r"Click, drag, or paste.*upload", re.I),
    ).first
    with page.expect_file_chooser(timeout=15_000) as chooser_info:
        upload_target.click(timeout=15_000)
    chooser_info.value.set_files(str(first_frame))
    page.wait_for_timeout(8_000)
    if dialog.is_visible():
        uploaded_image = dialog.locator("img").last
        if not uploaded_image.count():
            raise RuntimeError(
                "origin_dossier_media_magicfit_first_frame_preview_missing"
            )
        uploaded_image.click(timeout=15_000)
        page.wait_for_timeout(3_000)
    if page.get_by_role("dialog", name=re.compile(r"Select Image", re.I)).is_visible():
        raise RuntimeError(
            "origin_dossier_media_magicfit_first_frame_selection_incomplete"
        )


def fill_prompt(page, prompt: str) -> None:
    box = page.locator('[contenteditable="true"][role="textbox"]').first
    box.wait_for(timeout=10_000)
    box.evaluate(
        """(node) => {
            node.scrollIntoView({ block: 'center', inline: 'nearest' });
            node.focus();
            node.textContent = '';
        }"""
    )
    page.wait_for_timeout(200)
    box.click(timeout=10_000, force=True)
    page.keyboard.type(prompt, delay=1)
    page.wait_for_timeout(800)


def visible_urls(page) -> set[str]:
    urls = set(collect_urls(page.content()))
    try:
        video_urls = page.locator("video").evaluate_all(
            "(nodes) => nodes.map((v) => v.currentSrc || v.src).filter(Boolean)"
        )
    except Exception:
        video_urls = []
    urls.update(str(url) for url in video_urls if "magicfit" in str(url))
    return urls


def run() -> int:
    load_env()
    args = parse_args()
    output = Path(args.out).resolve()
    state = Path(args.state_json).resolve()
    first_frame = (
        Path(args.first_frame).resolve()
        if str(args.first_frame or "").strip()
        else None
    )
    if first_frame is not None and not first_frame.is_file():
        raise RuntimeError(
            "origin_dossier_media_magicfit_first_frame_missing"
        )
    first_frame_sha256 = (
        file_sha256(first_frame) if first_frame is not None else None
    )
    selected_duration = provider_duration(int(args.duration or 10))
    prompt = f"{args.prompt.strip()} Global constraints: {NEGATIVE}."
    with sync_playwright() as playwright:
        browser = playwright.chromium.launch(
            headless=True, args=["--no-sandbox"]
        )
        context = browser.new_context(
            viewport={"width": 1440, "height": 1100}, accept_downloads=True
        )
        page = context.new_page()
        try:
            maybe_login(page)
            page.goto(
                VIDEO_URL, wait_until="domcontentloaded", timeout=120_000
            )
            page.wait_for_timeout(5_000)
            baseline = visible_urls(page)
            if first_frame is not None:
                attach_first_frame(page, first_frame)
            select_button(page, "9:16", args.aspect_label)
            select_button(
                page,
                re.compile(r"^\s*\d+\s*s\s*$", re.I),
                f"{selected_duration}s",
            )
            if args.model_label:
                select_button(
                    page,
                    re.compile(r"Veo|Seedance|Kling|Hailuo|Sora", re.I),
                    args.model_label,
                )
            fill_prompt(page, prompt)
            events: list[dict[str, object]] = []
            observed_urls: set[str] = set()

            def handle_response(response) -> None:
                url = response.url
                if "magicfit" not in url and "pushowl" not in url:
                    return
                content_type = response.headers.get("content-type", "")
                events.append(
                    {
                        "method": response.request.method,
                        "status": response.status,
                        "url": url,
                        "contentType": content_type,
                    }
                )
                if re.search(
                    r"(?:cdn\.pushowl\.com|media\.powlcdn\.com)/magicfit/"
                    r".*\.(mp4|webm)(?:$|\?)",
                    url,
                ):
                    observed_urls.add(url)
                try:
                    body = (
                        response.text()
                        if re.search(r"json|script|text", content_type, re.I)
                        else ""
                    )
                except Exception:
                    body = ""
                observed_urls.update(collect_urls(body))

            page.on("response", handle_response)
            submitted_at_ms = int(time.time() * 1000)
            page.locator("form button").last.click(timeout=30_000)
            page.wait_for_timeout(3_000)
            deadline = time.time() + max(int(args.timeout_minutes), 1) * 60
            result_url = ""
            while time.time() < deadline and not result_url:
                page.wait_for_timeout(10_000)
                observed_urls.update(visible_urls(page))
                result_url = choose_newest(
                    observed_urls, baseline, submitted_at_ms
                )
            if not result_url:
                raise RuntimeError(
                    "origin_dossier_media_magicfit_video_url_not_found"
                )
            download(result_url, output)
            observed_duration = probe_duration(output)
            payload = {
                "contractVersion": "chummer.origin_dossier_magicfit_private.v1",
                "provider": "MagicFit",
                "videoOutputUrl": result_url,
                "outputFile": str(output),
                "durationSecondsRequested": int(args.duration or 10),
                "durationSecondsProviderRequested": selected_duration,
                "durationSecondsObserved": observed_duration,
                "durationObservationStatus": (
                    "ffprobe_verified"
                    if observed_duration is not None
                    else "unverified"
                ),
                "aspectLabel": args.aspect_label,
                "firstFrameApplied": first_frame is not None,
                "firstFrameSha256": first_frame_sha256,
                "prompt": prompt,
                "pageUrl": page.url,
                "eventsTail": events[-80:],
                "generatedAt": time.strftime(
                    "%Y-%m-%dT%H:%M:%SZ", time.gmtime()
                ),
            }
            state.parent.mkdir(parents=True, exist_ok=True)
            temporary = state.with_suffix(state.suffix + ".tmp")
            temporary.write_text(
                json.dumps(payload, indent=2) + "\n", encoding="utf-8"
            )
            temporary.replace(state)
            print(
                json.dumps(
                    {
                        "status": "succeeded",
                        "provider": "MagicFit",
                        "outputFile": str(output),
                        "durationSecondsObserved": observed_duration,
                    }
                )
            )
            return 0
        finally:
            context.close()
            browser.close()


if __name__ == "__main__":
    raise SystemExit(run())
