#!/usr/bin/env python3
"""Verify or repair the bounded media-factory design mirror bundle."""

from __future__ import annotations

import filecmp
import shutil
import sys
from pathlib import Path


SOURCE_ROOT = Path("/docker/chummercomplete/chummer-design/products/chummer")
MIRROR_ROOT = Path(".codex-design/product")

MIRRORED_FILES = (
    "README.md",
    "VISION.md",
    "USER_JOURNEYS.md",
    "NEXT_90_DAY_PRODUCT_ADVANCE_REGISTRY.yaml",
    "NEXT_90_DAY_QUEUE_STAGING.generated.yaml",
    "PUBLIC_LANDING_POLICY.md",
    "PUBLIC_DOWNLOADS_POLICY.md",
    "PUBLIC_LANDING_MANIFEST.yaml",
)


def main() -> int:
    repair = "--repair" in sys.argv[1:]
    unexpected_args = [arg for arg in sys.argv[1:] if arg != "--repair"]
    if unexpected_args:
        print(f"usage: {Path(sys.argv[0]).name} [--repair]", file=sys.stderr)
        return 2

    failures: list[str] = []
    repaired: list[str] = []

    for relative_path in MIRRORED_FILES:
        source = SOURCE_ROOT / relative_path
        mirror = MIRROR_ROOT / relative_path

        if not source.is_file():
            failures.append(f"missing canonical source: {source}")
            continue

        if not mirror.is_file():
            if repair:
                mirror.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(source, mirror)
                repaired.append(str(mirror))
                continue
            else:
                failures.append(f"missing local mirror: {mirror}")
                continue

        if not filecmp.cmp(source, mirror, shallow=False):
            if repair:
                shutil.copy2(source, mirror)
                repaired.append(str(mirror))
            else:
                failures.append(f"stale local mirror: {mirror} differs from {source}")

    if failures:
        print("design mirror verification failed:", file=sys.stderr)
        for failure in failures:
            print(f"- {failure}", file=sys.stderr)
        return 1

    if repaired:
        print("design mirror repaired:")
        for path in repaired:
            print(f"- {path}")

    print("design mirror verification ok")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
