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
    "proof_floor_commit": "398f756",
    "proof_floor_summary": "Pin M107 structured recipe receipt hardening with asset urls, stable replay ordering, and length-prefixed ref hashing",
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
        "AssetUrl",
        "CaptionRefReceipts",
        "PreviewRefReceipts",
        "RoleReceiptGroups",
        "StructuredMediaRecipeRoleReceiptGroup",
        "StructuredMediaRecipeRefArtifactReceipt",
        "ArtifactReceipts",
        "ApprovalState",
        "RetentionState",
        "StorageClass",
    ],
    "publication_ready_ref_guards": [
        "recipe execution waits for completed media jobs before emitting publication-ready receipt refs",
        "publication-ready refs preserve per-artifact ref, receipt id, job id, job state, output format, caption refs, preview refs, asset id, cache ttl, approval state, retention state, and storage class",
        "artifact, publication-ready, publication-ref, caption-ref, and preview-ref receipt rows preserve direct asset urls so publication shelves do not need a follow-up asset lookup",
        "bundle receipts expose aggregate JobIds matching every video, audio, preview-card, and packet-bundle artifact job",
        "publication ref receipt rows preserve receipt id, job id, job state, output format, asset id, cache ttl, approval state, retention state, and storage class",
        "role receipt groups preserve each video, audio, preview-card, and packet-bundle sibling's receipt ids, job ids, publication refs, caption refs, preview refs, lifecycle truth, and artifact rows",
        "caption ref receipt rows group shared refs while preserving per-artifact publication, job, and lifecycle detail",
        "preview ref receipt rows group shared refs while preserving packet-bundle publication, job, and lifecycle detail",
        "packet-bundle siblings require at least one preview ref before recipe execution",
        "publication refs are unique per recipe bundle so publication-ready receipt rows remain unambiguous",
        "role, caption, and preview receipt groups sort their receipt ids and refs explicitly so replayed bundles keep stable publication evidence ordering",
        "job dedupe includes artifact category, output format, and publication ref so colliding caller dedupe keys cannot collapse different recipe outputs",
        "job dedupe uses length-prefixed hashing so delimiter-heavy category, output format, publication ref, and caller dedupe values cannot collapse different recipe outputs",
        "receipt hashes include caption and preview refs so publication-ready refs remain tied to their emitted caption and preview surfaces",
        "receipt hashes use length-prefixed caption and preview ref segments so delimiter-heavy refs cannot collapse distinct publication-ready outputs onto one receipt id",
    ],
    "proof": [
        "src/Chummer.Media.Factory.Runtime/Assets/StructuredMediaRecipeExecutionService.cs",
        "src/Chummer.Media.Contracts/Compatibility/RunServices/MediaFactoryContracts.cs",
        "tests/StructuredMediaRecipeSmoke/Program.cs",
        "tests/test_structured_media_recipe_execution.py",
        "tests/test_m107_successor_closure_authority.py",
        "docs/NEXT90_M107_MEDIA_RECIPE_PROOF_FLOOR.md",
        "scripts/ai/materialize_media_release_proof.py",
        "scripts/ai/verify.sh",
    ],
}

M110_RUNSITE_ORIENTATION_PACKAGE = {
    "package_id": "next90-m110-media-factory-runsite-bundles",
    "frontier_id": 5126560638,
    "milestone_id": 110,
    "status": "complete",
    "completion_action": "verify_closed_package_only",
    "proof_floor_commit": "worktree-local",
    "proof_floor_summary": "Pin M110 runsite orientation bundle proof with host clips, route-linked preview receipts, and preview-only posture",
    "owned_surfaces": [
        "runsite_orientation_bundle",
        "route_preview:artifact_receipts",
    ],
    "artifact_roles": [
        "RunsiteHostClip",
        "RunsiteRoutePreview",
        "RunsiteAudioCompanion",
        "RunsiteTourSibling",
    ],
    "receipt_rows": [
        "HostClipReceiptIds",
        "RoutePreviewReceiptIds",
        "RoutePreviewArtifactReceipts",
        "RunsiteRoutePreviewArtifactReceipt",
        "AudioCompanionReceiptIds",
        "TourSiblingReceiptIds",
        "Artifacts",
        "JobId",
        "CacheTtl",
    ],
    "orientation_guards": [
        "runsite orientation bundles require at least one host clip before rendering",
        "runsite orientation bundles require at least one route preview before rendering",
        "preview truth posture stays pre-session-orientation-only-not-tactical-truth",
        "route preview receipt rows preserve route segment ids plus receipt and media job identity",
        "bundle-scoped dedupe keys include approved runsite pack, route summary, bundle id, role, route segment, category, output format, and caller dedupe",
        "artifact receipts expose media-factory job ids for every host clip, route preview, audio companion, and optional tour sibling",
        "orientation job dedupe and receipt hashing use length-prefixed segments so delimiter-heavy variants cannot collapse onto one media job or receipt id",
    ],
    "proof": [
        "src/Chummer.Media.Factory.Runtime/Assets/RunsiteOrientationBundleService.cs",
        "src/Chummer.Media.Contracts/Compatibility/RunServices/MediaFactoryContracts.cs",
        "tests/RunsiteOrientationBundleSmoke/Program.cs",
        "tests/test_runsite_orientation_bundle_contracts.py",
        "tests/test_m110_successor_closure_authority.py",
        "docs/NEXT90_M110_RUNSITE_ORIENTATION_PROOF_FLOOR.md",
        "scripts/ai/materialize_media_release_proof.py",
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
            "release_surface": "render jobs, manifests, previews, runsite orientation bundles, structured recipe bundles, and asset lifecycle",
        },
        "successor_packages": [
            M107_STRUCTURED_RECIPE_PACKAGE,
            M110_RUNSITE_ORIENTATION_PACKAGE,
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
            M110_RUNSITE_ORIENTATION_PACKAGE,
        ],
    }

    write_json(out_dir / "MEDIA_LOCAL_RELEASE_PROOF.generated.json", media_release_proof)
    write_json(out_dir / "ARTIFACT_PUBLICATION_CERTIFICATION.generated.json", artifact_publication_certification)
    print(f"wrote {out_dir / 'MEDIA_LOCAL_RELEASE_PROOF.generated.json'}")
    print(f"wrote {out_dir / 'ARTIFACT_PUBLICATION_CERTIFICATION.generated.json'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
