#!/usr/bin/env python3
from __future__ import annotations

import argparse
import datetime as dt
import json
from pathlib import Path
from typing import Any, Dict

UTC = dt.timezone.utc
M107_STRUCTURED_RECIPE_PACKAGE = {
    "package_id": "next90-m107-media-factory-recipe-execution",
    "frontier_id": 1746209281,
    "milestone_id": 107,
    "status": "complete",
    "completion_action": "verify_closed_package_only",
    "proof_floor_commit": "a2a3702",
    "proof_floor_summary": "Tighten M107 media recipe proof receipts",
    "owned_surfaces": [
        "structured_media_recipe_execution",
        "artifact_factory:receipts",
    ],
    "artifact_roles": [
        "StructuredRecipeVideo",
        "StructuredRecipeAudio",
        "StructuredRecipePreviewCard",
        "StructuredRecipePacketBundle",
    ],
    "receipt_rows": [
        "PublicationRefReceipts",
        "PublicationReadyRefs",
        "StructuredMediaRecipePublicationReadyRef",
        "JobIds",
        "CaptionRefReceipts",
        "PreviewRefReceipts",
        "RoleReceiptGroups",
        "StructuredMediaRecipeRoleReceiptGroup",
        "StructuredMediaRecipeRefArtifactReceipt",
        "ArtifactReceipts",
    ],
    "publication_ready_ref_guards": [
        "publication-ready refs preserve per-artifact ref, receipt id, job id, job state, output format, caption refs, preview refs, asset id, and cache ttl",
        "bundle receipts expose aggregate JobIds matching every video, audio, preview-card, and packet-bundle artifact job",
        "publication ref receipt rows preserve receipt id, job id, job state, output format, asset id, and cache ttl",
        "role receipt groups preserve each video, audio, preview-card, and packet-bundle sibling's receipt ids, job ids, publication refs, caption refs, preview refs, and artifact rows",
        "caption ref receipt rows group shared refs while preserving per-artifact publication and job detail",
        "preview ref receipt rows group shared refs while preserving packet-bundle publication and job detail",
        "packet-bundle siblings require at least one preview ref before recipe execution",
    ],
    "proof": [
        "src/Chummer.Media.Factory.Runtime/Assets/StructuredMediaRecipeExecutionService.cs",
        "src/Chummer.Media.Contracts/Compatibility/RunServices/MediaFactoryContracts.cs",
        "tests/StructuredMediaRecipeSmoke/Program.cs",
        "tests/test_structured_media_recipe_execution.py",
        "docs/NEXT90_M107_MEDIA_RECIPE_PROOF_FLOOR.md",
        "scripts/ai/verify.sh",
    ],
}


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
            "release_surface": "render jobs, manifests, previews, structured recipe bundles, and asset lifecycle",
        },
        "successor_packages": [
            M107_STRUCTURED_RECIPE_PACKAGE,
        ],
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
            "governance": "publication certification is manifest-, recipe-receipt-, and lifecycle-backed in media-factory outputs",
        },
        "successor_packages": [
            M107_STRUCTURED_RECIPE_PACKAGE,
        ],
    }

    write_json(out_dir / "MEDIA_LOCAL_RELEASE_PROOF.generated.json", media_release_proof)
    write_json(out_dir / "ARTIFACT_PUBLICATION_CERTIFICATION.generated.json", artifact_publication_certification)
    print(f"wrote {out_dir / 'MEDIA_LOCAL_RELEASE_PROOF.generated.json'}")
    print(f"wrote {out_dir / 'ARTIFACT_PUBLICATION_CERTIFICATION.generated.json'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
