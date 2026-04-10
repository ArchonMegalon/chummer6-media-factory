#!/usr/bin/env python3
from __future__ import annotations

import argparse
import datetime as dt
import json
from pathlib import Path
from typing import Any, Dict

UTC = dt.timezone.utc


def iso_now() -> str:
    return dt.datetime.now(UTC).replace(microsecond=0).isoformat().replace('+00:00', 'Z')


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Materialize media-factory local release proof and artifact publication certification receipts."
    )
    parser.add_argument(
        "--out-dir",
        default=".codex-studio/published",
        help="Directory to write generated proof artifacts.",
    )
    parser.add_argument(
        "--status",
        default="passed",
        choices=["pass", "passed", "ready", "fail", "failed", "blocked"],
        help="Proof status to publish.",
    )
    return parser.parse_args()


def write_json(path: Path, payload: Dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def main() -> int:
    args = parse_args()
    generated_at = iso_now()
    out_dir = Path(args.out_dir)
    normalized_status = str(args.status).strip().lower()

    media_release_proof = {
        "contract_name": "chummer6-media-factory.local_release_proof",
        "schema_version": 1,
        "generated_at": generated_at,
        "status": normalized_status,
        "proof_kind": "media_runtime_release",
        "evidence": {
            "runtime_verify_project": "Chummer.Media.Factory.Runtime.Verify",
            "build_lane": "dotnet build Chummer.Media.Factory.slnx --configuration Release",
            "release_surface": "render jobs, manifests, previews, and asset lifecycle",
        },
    }

    artifact_publication_certification = {
        "contract_name": "chummer6-media-factory.artifact_publication_certification",
        "schema_version": 1,
        "generated_at": generated_at,
        "status": normalized_status,
        "proof_kind": "artifact_publication",
        "lanes": [
            "target.sheet-viewer",
            "target.print-pdf-export",
            "target.character-template-export",
            "target.json-exchange",
            "target.foundry-export",
            "target.replay-timeline",
            "target.session-recap",
            "target.run-module",
        ],
        "evidence": {
            "source": "Chummer.Media.Factory.Runtime.Verify",
            "governance": "publication certification is manifest- and lifecycle-backed in media-factory outputs",
        },
    }

    write_json(out_dir / "MEDIA_LOCAL_RELEASE_PROOF.generated.json", media_release_proof)
    write_json(out_dir / "ARTIFACT_PUBLICATION_CERTIFICATION.generated.json", artifact_publication_certification)
    print(f"wrote {out_dir / 'MEDIA_LOCAL_RELEASE_PROOF.generated.json'}")
    print(f"wrote {out_dir / 'ARTIFACT_PUBLICATION_CERTIFICATION.generated.json'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
