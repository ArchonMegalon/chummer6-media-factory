#!/usr/bin/env python3
from __future__ import annotations

import argparse
import datetime as dt
import json
from pathlib import Path
from typing import Any, Dict

UTC = dt.timezone.utc
M108_CAMPAIGN_BRIEFING_PACKAGE = {
    "package_id": "next90-m108-media-factory-campaign-briefing-renders",
    "frontier_id": 4459920059,
    "milestone_id": 108,
    "status": "complete",
    "completion_action": "verify_closed_package_only",
    "proof_floor_commit": "ef3f006",
    "proof_floor_summary": "Pin M108 campaign briefing bundle closure with locale-bundled caption and preview siblings, bounded fallback locales, and length-prefixed receipt hashing",
    "owned_surfaces": [
        "campaign_briefing_bundle_rendering",
        "campaign_artifact_receipts",
    ],
    "artifact_roles": [
        "CampaignColdOpen",
        "CampaignMissionBriefing",
        "CampaignCaption",
        "CampaignPreview",
    ],
    "receipt_rows": [
        "LocaleReceipts",
        "LocaleBundleReceipts",
        "CampaignBriefingLocaleBundleReceipt",
        "ArtifactReceipts",
        "CampaignBriefingLocaleReceipt",
        "CampaignBriefingArtifactReceipt",
        "CampaignBriefingFallbackSiblingReceipt",
        "ColdOpenCaptionReceiptId",
        "MissionBriefingCaptionReceiptId",
        "ColdOpenPreviewReceiptId",
        "MissionBriefingPreviewReceiptId",
        "RequestedLocaleBundleReceiptId",
        "FallbackLocales",
        "FallbackLocaleBundleReceiptIds",
        "ColdOpenReceiptIds",
        "MissionBriefingReceiptIds",
        "CaptionReceiptIds",
        "PreviewReceiptIds",
        "FallbackSiblingReceipts",
        "JobIds",
        "AssetUrl",
        "ApprovalState",
        "RetentionState",
        "StorageClass",
    ],
    "campaign_briefing_guards": [
        "campaign briefing bundles require a requested-locale ColdOpen entry before rendering",
        "campaign briefing bundles require a requested-locale MissionBriefing entry before rendering",
        "campaign briefing bundles render media, caption, and preview siblings per locale entry",
        "campaign briefing bundles keep the requested locale as the primary sibling and require every other locale to be a fallback sibling",
        "campaign briefing bundles require locale-matched cold-open and mission briefing siblings for every requested and fallback locale bundle",
        "campaign briefing bundles allow at most two fallback locales",
        "campaign briefing receipts preserve asset urls, locale receipt ids, locale bundle receipt ids, and per-entry job ids",
        "campaign briefing bundle receipts also preserve first-class requested-locale and fallback bundle summary fields",
        "campaign briefing locale bundle and fallback sibling receipts preserve slot-aware caption and preview sibling ids",
        "campaign briefing artifact receipts preserve approval state, retention state, and storage class alongside asset urls",
        "campaign briefing dedupe keys include requested locale, slot, entry locale, fallback posture, category, output format, and caller dedupe",
        "campaign briefing receipt hashes use length-prefixed locale, artifact-kind, and output-format segments so delimiter-heavy locale variants cannot collapse onto one receipt id",
        "campaign briefing normalized locale-bundle ordering keeps locale receipts, locale bundle receipts, fallback sibling receipts, and summary job ids stable when callers reorder the same bundle entries",
        "campaign briefing package authority requires exactly one canonical queue row per mirror and exactly one registry task block",
    ],
    "proof": [
        "src/Chummer.Media.Factory.Runtime/Assets/CampaignBriefingBundleService.cs",
        "src/Chummer.Media.Contracts/Compatibility/RunServices/MediaFactoryContracts.cs",
        "tests/CampaignBriefingBundleSmoke/Program.cs",
        "tests/test_campaign_briefing_bundle_contracts.py",
        "tests/test_m108_campaign_briefing_proof.py",
        "tests/test_m108_successor_package_authority.py",
        "docs/NEXT90_M108_CAMPAIGN_BRIEFING_PROOF_FLOOR.md",
        "scripts/ai/materialize_media_release_proof.py",
        "scripts/ai/verify.sh",
    ],
}

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
        "publication ref receipt rows preserve receipt id, job id, job state, output format, caption refs, preview refs, asset id, cache ttl, approval state, retention state, and storage class",
        "role receipt groups preserve each video, audio, preview-card, and packet-bundle sibling's receipt ids, job ids, publication refs, caption refs, preview refs, lifecycle truth, and artifact rows",
        "caption ref receipt rows group shared refs while preserving per-artifact publication, job, and lifecycle detail",
        "caption ref receipt rows expose aggregate job ids and publication refs so shared caption evidence stays first-class without traversing nested artifact rows",
        "preview ref receipt rows group shared refs while preserving packet-bundle publication, job, and lifecycle detail",
        "preview ref receipt rows expose aggregate job ids and publication refs so shared preview evidence stays first-class without traversing nested artifact rows",
        "grouped caption-ref and preview-ref artifact rows preserve per-artifact caption refs and preview refs so downstream shelves do not need to join back to the raw artifact receipt list",
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

M109_BUILD_EXPLAIN_COMPANION_PACKAGE = {
    "package_id": "next90-m109-media-factory-build-explain-bundles",
    "frontier_id": 4037265286,
    "milestone_id": 109,
    "status": "in_progress",
    "completion_action": "implementation_only",
    "proof_floor_commit": "unlanded",
    "proof_floor_summary": "Track M109 build explain companion bundle implementation with approved explain packets, stable sibling ordering, and first-class companion receipt refs",
    "owned_surfaces": [
        "build_explain_companion_rendering",
        "explain_artifact_receipts",
    ],
    "artifact_roles": [
        "BuildExplainCompanionVideo",
        "BuildExplainCompanionAudio",
        "BuildExplainCompanionPreviewCard",
        "BuildExplainCompanionPacketCompanion",
    ],
    "receipt_rows": [
        "VideoReceiptIds",
        "AudioReceiptIds",
        "PreviewCardReceiptIds",
        "PacketCompanionReceiptIds",
        "JobIds",
        "CompanionRefs",
        "CompanionReadyRefs",
        "BuildExplainCompanionReadyRef",
        "RoleReceiptGroups",
        "BuildExplainCompanionRoleReceiptGroup",
        "CompanionRefReceipts",
        "BuildExplainCompanionRefReceipt",
        "CaptionRefReceipts",
        "BuildExplainCaptionRefReceipt",
        "PreviewRefReceipts",
        "BuildExplainPreviewRefReceipt",
        "ArtifactReceipts",
        "AssetUrl",
        "ApprovalState",
        "RetentionState",
        "StorageClass",
    ],
    "build_explain_guards": [
        "sibling payloads must stay scoped to the approved explain packet id and explain packet revision id before media jobs enqueue, and JSON payloads must match those scope fields exactly instead of passing on substring mentions alone",
        "non-JSON scope fallback requires exact keyed values or delimited scope tokens so near-match packet and revision ids cannot slip through by substring collision",
        "build explain companion rendering requires one video, one audio, one preview-card, and one packet companion before the bundle can render",
        "build explain video and audio siblings require caption refs while video, preview-card, and packet companions require preview refs",
        "companion refs are unique per approved explain packet so downstream shelves cannot confuse sibling outputs",
        "bundle-scoped dedupe keys include approved explain packet id, explain packet revision id, rendering id, sibling role, category, output format, companion ref, and caller dedupe key",
        "receipt hashes include caption and preview refs so companion receipts stay tied to their emitted explain siblings",
        "receipt hashes use length-prefixed caption and preview ref segments so delimiter-heavy refs cannot collapse distinct companion outputs onto one receipt id",
        "normalized sibling ordering keeps receipt ids, companion refs, ready refs, and grouped role, caption, and preview receipt rows stable when callers reorder the same approved explain packet artifacts",
        "source and requested timestamp metadata stay outside bundle-scoped dedupe and receipt identity so replayed approved explain packet renders cannot fork stable job ids, receipt ids, ready refs, or grouped role receipts",
        "companion ready refs preserve per-artifact ref, receipt id, job id, job state, output format, caption refs, preview refs, asset id, asset url, cache ttl, approval state, retention state, and storage class",
        "role, caption, and preview receipt groups preserve aggregate job ids, grouped companion refs, and grouped artifact rows so downstream shelves do not need to reconstruct explain evidence from raw artifact receipts",
        "rendered timestamps resolve from completed media jobs so later deduped retries cannot rewrite bundle render time with a newer request timestamp",
    ],
    "proof": [
        "src/Chummer.Media.Factory.Runtime/Assets/BuildExplainCompanionRenderingService.cs",
        "src/Chummer.Media.Contracts/Compatibility/RunServices/MediaFactoryContracts.cs",
        "tests/BuildExplainCompanionSmoke/Program.cs",
        "tests/test_m109_successor_package_authority.py",
        "tests/test_build_explain_companion_rendering.py",
        "tests/test_m109_build_explain_proof.py",
        "docs/NEXT90_M109_BUILD_EXPLAIN_COMPANION_PROOF_FLOOR.md",
        "scripts/ai/materialize_media_release_proof.py",
        "scripts/ai/verify_m109_build_explain_companion.sh",
    ],
}

M110_RUNSITE_ORIENTATION_PACKAGE = {
    "package_id": "next90-m110-media-factory-runsite-bundles",
    "frontier_id": 5126560638,
    "milestone_id": 110,
    "status": "complete",
    "completion_action": "verify_closed_package_only",
    "proof_floor_commit": "3accc50",
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

M111_INSTALL_AWARE_CONCIERGE_PACKAGE = {
    "package_id": "next90-m111-media-factory-concierge-bundles",
    "frontier_id": 4132724850,
    "milestone_id": 111,
    "status": "in_progress",
    "completion_action": "implementation_only",
    "proof_floor_commit": "unlanded",
    "proof_floor_summary": "Track M111 install-aware concierge bundle implementation with scoped install-aware payloads, first-class companion refs, and bounded sibling notes",
    "owned_surfaces": [
        "release_explainer_artifacts",
        "support_closure_artifacts",
        "public_concierge_companions",
    ],
    "artifact_roles": [
        "InstallAwareReleaseExplainerVideo",
        "InstallAwareReleaseExplainerAudio",
        "InstallAwareReleaseExplainerPreviewCard",
        "InstallAwareSupportClosureVideo",
        "InstallAwareSupportClosureAudio",
        "InstallAwareSupportClosurePreviewCard",
        "InstallAwarePublicConciergeVideo",
        "InstallAwarePublicConciergeAudio",
        "InstallAwarePublicConciergePreviewCard",
    ],
    "receipt_rows": [
        "ReleaseExplainerReceiptIds",
        "SupportClosureReceiptIds",
        "PublicConciergeReceiptIds",
        "JobIds",
        "CompanionRefs",
        "CompanionReadyRefs",
        "InstallAwareConciergeCompanionReadyRef",
        "RoleReceiptGroups",
        "InstallAwareConciergeRoleReceiptGroup",
        "CompanionRefReceipts",
        "InstallAwareConciergeCompanionRefReceipt",
        "CaptionRefReceipts",
        "InstallAwareConciergeCaptionRefReceipt",
        "PreviewRefReceipts",
        "InstallAwareConciergePreviewRefReceipt",
        "SiblingNoteReceipts",
        "InstallAwareConciergeSiblingNoteReceipt",
        "ArtifactReceipts",
        "AssetUrl",
        "ApprovalState",
        "RetentionState",
        "StorageClass",
    ],
    "install_aware_concierge_guards": [
        "install-aware concierge payloads must stay scoped to the install-aware packet id, installed build receipt id, and artifact identity id before media jobs enqueue, and JSON payloads must match those scope fields exactly instead of passing on substring mentions alone",
        "request-level rendering id, install-aware packet id, installed build receipt id, artifact identity id, and source values normalize surrounding whitespace before scope enforcement so valid concierge requests do not fail on padded caller input",
        "install-aware concierge bundles require release explainer, support closure, and public concierge siblings to each emit video, audio, and preview-card artifacts before the bundle can render",
        "install-aware concierge video and audio companions require caption refs while video and preview-card companions require preview refs",
        "install-aware concierge artifacts require at least one sibling note ref and keep sibling notes bounded to at most two refs per artifact",
        "companion refs are unique per install-aware packet so downstream shelves cannot confuse release, support, and public concierge outputs",
        "bundle-scoped dedupe keys include install-aware packet id, installed build receipt id, artifact identity id, rendering id, bundle kind, sibling role, category, output format, companion ref, and caller dedupe key",
        "receipt hashes include caption, preview, and sibling-note refs so concierge receipts stay tied to their emitted release, support, and public siblings",
        "receipt hashes use length-prefixed caption, preview, and sibling-note ref segments so delimiter-heavy refs cannot collapse distinct concierge outputs onto one receipt id",
        "companion ready refs preserve per-artifact ref, receipt id, job id, job state, output format, caption refs, preview refs, sibling note refs, asset id, asset url, cache ttl, approval state, retention state, and storage class",
        "role, caption, preview, and sibling-note receipt groups preserve aggregate job ids, grouped companion refs, grouped bundle kinds, and grouped artifact rows so downstream shelves do not need to reconstruct concierge evidence from raw artifact receipts",
        "rendered timestamps resolve from completed media jobs so later deduped retries cannot rewrite bundle render time with a newer request timestamp",
    ],
    "proof": [
        "src/Chummer.Media.Factory.Runtime/Assets/InstallAwareConciergeBundleService.cs",
        "src/Chummer.Media.Contracts/Compatibility/RunServices/MediaFactoryContracts.cs",
        "tests/InstallAwareConciergeSmoke/Program.cs",
        "tests/test_install_aware_concierge_rendering.py",
        "tests/test_m111_successor_package_authority.py",
        "tests/test_m111_install_aware_concierge_proof.py",
        "docs/NEXT90_M111_INSTALL_AWARE_CONCIERGE_PROOF_FLOOR.md",
        "scripts/ai/materialize_media_release_proof.py",
        "scripts/ai/verify_m111_install_aware_concierge.sh",
        "scripts/ai/verify.sh",
    ],
}

M113_GM_PREP_PACKET_PACKAGE = {
    "package_id": "next90-m113-media-factory-gm-prep-packets",
    "frontier_id": 3813748639,
    "milestone_id": 113,
    "status": "in_progress",
    "completion_action": "implementation_only",
    "proof_floor_commit": "unlanded",
    "proof_floor_summary": "Track M113 governed GM prep packet rendering with opposition-required entries, optional briefing siblings, and first-class subject receipt groups",
    "owned_surfaces": [
        "gm_prep_packets",
        "opposition_packet_artifacts",
    ],
    "artifact_roles": [
        "GmPrepOppositionPacket",
        "GmPrepOppositionPreview",
        "GmPrepOppositionBriefing",
        "GmPrepScenePacket",
        "GmPrepScenePreview",
        "GmPrepSceneBriefing",
        "GmPrepLibraryPacket",
        "GmPrepLibraryPreview",
        "GmPrepLibraryBriefing",
    ],
    "receipt_rows": [
        "EntryReceipts",
        "GmPrepPacketEntryReceipt",
        "SubjectReceiptGroups",
        "GmPrepPacketSubjectReceiptGroup",
        "PacketReceiptIds",
        "PreviewReceiptIds",
        "BriefingReceiptIds",
        "OppositionPacketReceiptIds",
        "ScenePacketReceiptIds",
        "PrepLibraryPacketReceiptIds",
        "PacketRefs",
        "JobIds",
        "ArtifactReceipts",
        "AssetUrl",
        "ApprovalState",
        "RetentionState",
        "StorageClass",
    ],
    "gm_prep_packet_guards": [
        "GM prep packet rendering stays render-only by requiring a governed source pack id and source pack revision id plus sibling-only payloads before media jobs enqueue, and JSON payloads must match those scope fields exactly instead of passing on substring mentions alone",
        "GM prep packet rendering requires at least one opposition entry and keeps scene and prep-library entries optional within the same governed render request",
        "GM prep packet entries require packet and preview artifacts while briefing artifacts stay optional per governed entry",
        "GM prep packet rendering rejects duplicate source entries and duplicate packet refs inside one governed render request",
        "bundle-scoped dedupe keys include governed source pack id, source pack revision id, rendering id, subject kind, source entry id, packet ref, artifact role, category, output format, and caller dedupe key",
        "receipt hashes use length-prefixed subject-kind, artifact-role, and output-format segments so delimiter-heavy GM prep variants cannot collapse distinct outputs onto one receipt id",
        "entry receipt ids and subject receipt group ids stay scoped to governed source pack id, source pack revision id, and rendering id so reused packet refs cannot alias grouped evidence across governed packs",
        "subject receipt groups preserve grouped entry ids, packet refs, packet receipt ids, preview receipt ids, optional briefing receipt ids, aggregate job ids, and grouped artifact rows so downstream shelves do not need to reconstruct governed prep evidence from raw artifact receipts",
        "GM prep packet artifact receipts preserve asset urls, approval state, retention state, and storage class alongside packet, preview, and optional briefing outputs",
        "rendered timestamps resolve from completed media jobs so later deduped retries cannot rewrite bundle render time with a newer request timestamp",
    ],
    "proof": [
        "src/Chummer.Media.Factory.Runtime/Assets/GmPrepPacketBundleService.cs",
        "src/Chummer.Media.Contracts/Compatibility/RunServices/MediaFactoryContracts.cs",
        "tests/GmPrepPacketSmoke/Program.cs",
        "tests/test_gm_prep_packet_rendering.py",
        "tests/test_m113_gm_prep_packet_proof.py",
        "tests/test_m113_successor_package_authority.py",
        "docs/NEXT90_M113_GM_PREP_PACKET_PROOF_FLOOR.md",
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
            "release_surface": "render jobs, manifests, previews, campaign briefing bundles, runsite orientation bundles, structured recipe bundles, build explain companion bundles, install-aware concierge bundles, GM prep packet bundles, and asset lifecycle",
        },
        "successor_packages": [
            M108_CAMPAIGN_BRIEFING_PACKAGE,
            M107_STRUCTURED_RECIPE_PACKAGE,
            M109_BUILD_EXPLAIN_COMPANION_PACKAGE,
            M110_RUNSITE_ORIENTATION_PACKAGE,
            M111_INSTALL_AWARE_CONCIERGE_PACKAGE,
            M113_GM_PREP_PACKET_PACKAGE,
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
            M108_CAMPAIGN_BRIEFING_PACKAGE,
            M107_STRUCTURED_RECIPE_PACKAGE,
            M109_BUILD_EXPLAIN_COMPANION_PACKAGE,
            M110_RUNSITE_ORIENTATION_PACKAGE,
            M111_INSTALL_AWARE_CONCIERGE_PACKAGE,
            M113_GM_PREP_PACKET_PACKAGE,
        ],
    }

    write_json(out_dir / "MEDIA_LOCAL_RELEASE_PROOF.generated.json", media_release_proof)
    write_json(out_dir / "ARTIFACT_PUBLICATION_CERTIFICATION.generated.json", artifact_publication_certification)
    print(f"wrote {out_dir / 'MEDIA_LOCAL_RELEASE_PROOF.generated.json'}")
    print(f"wrote {out_dir / 'ARTIFACT_PUBLICATION_CERTIFICATION.generated.json'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
