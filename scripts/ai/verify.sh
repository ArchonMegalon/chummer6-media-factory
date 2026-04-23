#!/usr/bin/env bash
set -euo pipefail

export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-/tmp/chummer-media-factory-dotnet}"
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_NOLOGO=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1

test -f README.md
test -f AGENTS.md
test -f WORKLIST.md
test -f Directory.Build.props
test -f Chummer.Media.Factory.slnx
test -f Chummer.Media.Factory.Runtime.Verify/Chummer.Media.Factory.Runtime.Verify.csproj
test -f tests/CampaignBriefingBundleSmoke/Chummer.Media.Factory.CampaignBriefingBundleSmoke.csproj
test -f tests/RunsiteOrientationBundleSmoke/Chummer.Media.Factory.RunsiteOrientationBundleSmoke.csproj
test -f tests/StructuredMediaRecipeSmoke/Chummer.Media.Factory.StructuredMediaRecipeSmoke.csproj
test -f tests/BuildExplainCompanionSmoke/Chummer.Media.Factory.BuildExplainCompanionSmoke.csproj
test -f tests/InstallAwareConciergeSmoke/Chummer.Media.Factory.InstallAwareConciergeSmoke.csproj
test -f tests/GmPrepPacketSmoke/Chummer.Media.Factory.GmPrepPacketSmoke.csproj
test -f src/Chummer.Media.Contracts/Chummer.Media.Contracts.csproj
test -f src/Chummer.Media.Contracts/ContractsAssemblyMarker.cs
test -f src/Chummer.Media.Contracts/README.md
test -f src/Chummer.Media.Factory.Runtime/Assets/AssetLifecycleService.cs
test -f src/Chummer.Media.Factory.Runtime/Assets/CampaignBriefingBundleService.cs
test -f src/Chummer.Media.Factory.Runtime/Assets/GmPrepPacketBundleService.cs
test -f src/Chummer.Media.Factory.Runtime/Assets/MediaRenderJobService.cs
test -f src/Chummer.Media.Factory.Runtime/Assets/StructuredMediaRecipeExecutionService.cs
test -f src/Chummer.Media.Factory.Runtime/Assets/BuildExplainCompanionRenderingService.cs
test -f src/Chummer.Media.Factory.Runtime/Assets/InstallAwareConciergeBundleService.cs
test -f docs/chummer-media-factory.design.v1.md
test -f docs/MEDIA_ADAPTER_MATRIX.md
test -f docs/MEDIA_CAPABILITY_SIGNOFF.md
test -f docs/MEDIA_FACTORY_RESTORE_RUNBOOK.md
test -f docs/EXTRACT-008-DS-execution-evidence.md
test -f docs/NEXT90_M108_CAMPAIGN_BRIEFING_PROOF_FLOOR.md
test -f docs/NEXT90_M110_RUNSITE_ORIENTATION_PROOF_FLOOR.md
test -f docs/NEXT90_M109_BUILD_EXPLAIN_COMPANION_PROOF_FLOOR.md
test -f docs/NEXT90_M111_INSTALL_AWARE_CONCIERGE_PROOF_FLOOR.md
test -f docs/NEXT90_M113_GM_PREP_PACKET_PROOF_FLOOR.md
test -f scripts/ai/contract-boundary-tests.sh
test -f scripts/ai/materialize_media_release_proof.py
test -f scripts/ai/verify_m109_build_explain_companion.sh
test -f scripts/ai/verify_m111_install_aware_concierge.sh
test -f scripts/render_guide_asset.py
test -f tests/test_m109_successor_package_authority.py
test -f tests/test_m111_successor_package_authority.py
test -f tests/test_m113_successor_package_authority.py

rg -n 'media_factory_state_backup_v1|Chummer\.Media\.Factory\.Runtime\.Verify|retention sweep' docs/MEDIA_FACTORY_RESTORE_RUNBOOK.md >/dev/null
rg -n 'CHUMMER_MEDIA_FACTORY_IMAGE_BACKEND|CHUMMER_MEDIA_FACTORY_ENABLE_IMAGE_EXECUTION|preview image|archive / retention storage|Receipts must record the actual selected backend' docs/MEDIA_ADAPTER_MATRIX.md >/dev/null
rg -n 'DocumentPdf|DocumentThumbnailImage|PortraitImageVariant|NarrativeBriefVideo|RouteCinemaResult|CampaignBriefingBundleReceipt|CampaignBriefingLocaleBundleReceipt|CampaignBriefingFallbackSiblingReceipt|CampaignColdOpen|CampaignMissionBriefing|CampaignCaption|CampaignPreview|RunsiteOrientationBundleReceipt|StructuredMediaRecipeBundleReceipt|StructuredRecipeVideo|StructuredRecipeAudio|StructuredRecipePreviewCard|StructuredRecipePacketBundle|RunsiteHostClip|RunsiteRoutePreview|AssetLifecyclePolicy|CHUMMER_MEDIA_FACTORY_IMAGE_BACKEND|CHUMMER_MEDIA_FACTORY_ENABLE_IMAGE_EXECUTION' docs/MEDIA_CAPABILITY_SIGNOFF.md >/dev/null
rg -n 'CampaignBriefingBundleReceipt|CampaignBriefingLocaleReceipt|CampaignBriefingFallbackSiblingReceipt|ColdOpenCaptionReceiptId|MissionBriefingCaptionReceiptId|ColdOpenPreviewReceiptId|MissionBriefingPreviewReceiptId|CampaignColdOpen|CampaignMissionBriefing|CampaignCaption|CampaignPreview' src docs/NEXT90_M108_CAMPAIGN_BRIEFING_PROOF_FLOOR.md >/dev/null
rg -n 'next90-m108-media-factory-campaign-briefing-renders|4459920059|campaign_briefing_bundle_rendering|campaign_artifact_receipts|verify_closed_package_only|proof floor commit|requested-locale ColdOpen|requested-locale MissionBriefing|requested locale as the primary sibling|fallback locales|slot-aware caption and preview sibling ids|length-prefixed locale|approval state, retention state, and storage class|normalized locale-bundle ordering|exactly one canonical queue row per mirror and exactly one registry task block' docs/NEXT90_M108_CAMPAIGN_BRIEFING_PROOF_FLOOR.md scripts/ai/materialize_media_release_proof.py >/dev/null
rg -n 'BuildExplainCompanionRenderRequest|BuildExplainCompanionRenderReceipt|BuildExplainCompanionReadyRef|BuildExplainCompanionRoleReceiptGroup|BuildExplainCompanionRefReceipt|BuildExplainCaptionRefReceipt|BuildExplainPreviewRefReceipt|BuildExplainCompanionVideo|BuildExplainCompanionAudio|BuildExplainCompanionPreviewCard|BuildExplainCompanionPacketCompanion' src docs/MEDIA_CAPABILITY_SIGNOFF.md >/dev/null
rg -n 'pre-session-orientation-only-not-tactical-truth|HostClipReceiptIds|RoutePreviewReceiptIds|RoutePreviewArtifactReceipts|RunsiteRoutePreviewArtifactReceipt|RunsiteOrientationArtifactReceipt' src docs/MEDIA_CAPABILITY_SIGNOFF.md >/dev/null
rg -n 'next90-m110-media-factory-runsite-bundles|5126560638|runsite_orientation_bundle|route_preview:artifact_receipts|pre-session-orientation-only-not-tactical-truth|length-prefixed|category, output format' docs/NEXT90_M110_RUNSITE_ORIENTATION_PROOF_FLOOR.md scripts/ai/materialize_media_release_proof.py >/dev/null
rg -n 'InstallAwareConciergeRenderRequest|InstallAwareConciergeBundleReceipt|InstallAwareConciergeCompanionReadyRef|InstallAwareConciergeRoleReceiptGroup|InstallAwareConciergeCompanionRefReceipt|InstallAwareConciergeCaptionRefReceipt|InstallAwareConciergePreviewRefReceipt|InstallAwareConciergeSiblingNoteReceipt|InstallAwareReleaseExplainerVideo|InstallAwareSupportClosureAudio|InstallAwarePublicConciergePreviewCard' src docs/MEDIA_CAPABILITY_SIGNOFF.md >/dev/null
rg -n 'next90-m111-media-factory-concierge-bundles|4132724850|release_explainer_artifacts|support_closure_artifacts|public_concierge_companions|install-aware concierge payloads must stay scoped|bounded sibling notes|length-prefixed caption, preview, and sibling-note ref segments' docs/NEXT90_M111_INSTALL_AWARE_CONCIERGE_PROOF_FLOOR.md scripts/ai/materialize_media_release_proof.py >/dev/null
rg -n 'GmPrepPacketRenderRequest|GmPrepPacketBundleReceipt|GmPrepPacketEntryReceipt|GmPrepPacketSubjectReceiptGroup|GmPrepOppositionPacket|GmPrepScenePreview|GmPrepLibraryBriefing' src docs/MEDIA_CAPABILITY_SIGNOFF.md >/dev/null
rg -n 'next90-m113-media-factory-gm-prep-packets|3813748639|gm_prep_packets|opposition_packet_artifacts|governed source pack id|optional briefing|length-prefixed subject-kind, artifact-role, and output-format' docs/NEXT90_M113_GM_PREP_PACKET_PROOF_FLOOR.md scripts/ai/materialize_media_release_proof.py >/dev/null
rg -n 'StructuredMediaRecipeExecutionService|PublicationRefs|PublicationReadyRefs|StructuredMediaRecipePublicationReadyRef|CaptionRefs|PreviewRefs|VideoReceiptIds|AudioReceiptIds|PreviewReceiptIds|PacketReceiptIds|JobIds|RoleReceiptGroups|StructuredMediaRecipeRoleReceiptGroup|PublicationRefReceipts|CaptionRefReceipts|PreviewRefReceipts|ArtifactReceipts|AssetUrl' src docs/MEDIA_CAPABILITY_SIGNOFF.md >/dev/null
rg -n 'artifact category, output format, and publication ref|duplicate publication refs|colliding caller dedupe keys|Different video output refs must not collapse onto one recipe job' docs/MEDIA_CAPABILITY_SIGNOFF.md tests/StructuredMediaRecipeSmoke/Program.cs >/dev/null
rg -n 'next90-m109-media-factory-build-explain-bundles|build_explain_companion_rendering|explain_artifact_receipts|BuildExplainCompanionRenderingService|BuildExplainCompanionRenderReceipt|build explain companion receipts stay render-only|approved explain packet id and explain packet revision id|duplicate companion refs inside one approved explain packet|stable when callers reorder build explain siblings|source or requested timestamp drift|source and requested timestamp metadata stay outside bundle-scoped dedupe and receipt identity|length-prefixed dedupe and receipt hashing' docs/NEXT90_M109_BUILD_EXPLAIN_COMPANION_PROOF_FLOOR.md docs/MEDIA_CAPABILITY_SIGNOFF.md src/Chummer.Media.Factory.Runtime/Assets/BuildExplainCompanionRenderingService.cs tests/BuildExplainCompanionSmoke/Program.cs >/dev/null
rg -n 'ProjectReference Include="\.\.\\Chummer\.Media\.Contracts\\Chummer\.Media\.Contracts\.csproj"' src/Chummer.Media.Factory.Runtime/Chummer.Media.Factory.Runtime.csproj >/dev/null
if rg -n 'ChummerCampaignContractsPackageId|ChummerCampaignContractsPackageVersion|ChummerLocalCampaignContractsProject' Directory.Build.props >/dev/null; then
  echo "verify failed: campaign-contract package wiring must not exist in media-factory"
  exit 1
fi

if rg -n 'Chummer\.Campaign\.Contracts|CreatorPublicationPlannerService|GovernedPrepPacketPlannerService|queue_review|share_public_publication|refresh_binding_posture|launch_governed_packet' src Chummer.Media.Factory.Runtime.Verify docs/MEDIA_CAPABILITY_SIGNOFF.md scripts/ai/materialize_media_release_proof.py -g '*.cs' -g '*.md' -g '*.py' >/dev/null; then
  echo "verify failed: non-render campaign/publication planning leaked into media-factory surfaces"
  exit 1
fi

if rg -n 'namespace Chummer\.Campaign\.Contracts' src Chummer.Media.Factory.Runtime.Verify -g '*.cs' >/dev/null; then
  echo "verify failed: media-factory must consume campaign contracts from the owner package/project, not redefine them"
  exit 1
fi

python3 -m py_compile scripts/render_guide_asset.py scripts/ai/materialize_media_release_proof.py
python3 -m py_compile scripts/ai/verify_design_mirror.py
python3 -m unittest discover -s tests
python3 scripts/ai/verify_design_mirror.py --repair >/dev/null
python3 scripts/ai/verify_design_mirror.py >/dev/null
python3 scripts/render_guide_asset.py --prompt "media factory dry run" --output /tmp/chummer-media-factory-dry-run.png --width 1600 --height 900 --dry-run | rg -n '"backend_selection_env": "CHUMMER_MEDIA_FACTORY_IMAGE_BACKEND"|"backend_enable_env": "CHUMMER_MEDIA_FACTORY_ENABLE_IMAGE_EXECUTION"|"backend_provider": "onemin"|"manager_allow_reserve": true|"manager_allow_reserve_env": "CHUMMER_MEDIA_FACTORY_ONEMIN_ALLOW_RESERVE"' >/dev/null
CHUMMER_MEDIA_FACTORY_ENABLE_IMAGE_EXECUTION=0 python3 scripts/render_guide_asset.py --prompt "media factory disabled dry run" --output /tmp/chummer-media-factory-disabled-dry-run.png --width 1600 --height 900 --dry-run | rg -n '"image_execution_enabled": false|"backend_provider": "disabled"' >/dev/null
if CHUMMER_MEDIA_FACTORY_IMAGE_BACKEND=bogus python3 scripts/render_guide_asset.py --prompt "media factory bogus backend" --output /tmp/chummer-media-factory-bogus-backend.png --width 1600 --height 900 >/tmp/chummer-media-factory-bogus.log 2>&1; then
  echo "verify failed: unsupported backend should fail fast" >&2
  exit 1
fi
rg -n 'media_factory:unsupported_backend:bogus' /tmp/chummer-media-factory-bogus.log >/dev/null
if CHUMMER_MEDIA_FACTORY_IMAGE_BACKEND=openai_edits OPENAI_API_KEY=dummy python3 scripts/render_guide_asset.py --prompt "media factory missing reference" --output /tmp/chummer-media-factory-openai-missing-ref.png --width 1600 --height 900 >/tmp/chummer-media-factory-openai-missing-ref.log 2>&1; then
  echo "verify failed: openai_edits should require a reference image" >&2
  exit 1
fi
rg -n 'media_factory:missing_reference_image' /tmp/chummer-media-factory-openai-missing-ref.log >/dev/null
if CHUMMER_MEDIA_FACTORY_IMAGE_BACKEND=openai_edits OPENAI_API_KEY=dummy python3 scripts/render_guide_asset.py --prompt "media factory invalid reference" --output /tmp/chummer-media-factory-openai-invalid-ref.png --width 1600 --height 900 --reference-image . >/tmp/chummer-media-factory-openai-invalid-ref.log 2>&1; then
  echo "verify failed: openai_edits should reject non-file reference images" >&2
  exit 1
fi
rg -n 'media_factory:invalid_reference_image:\.' /tmp/chummer-media-factory-openai-invalid-ref.log >/dev/null

health_state_dir="$(mktemp -d "${TMPDIR:-/tmp}/chummer-media-health.XXXXXX")"
printf '[]\n' >"${health_state_dir}/guide_provider_health.json"
CHUMMER_MEDIA_FACTORY_STATE_DIR="${health_state_dir}" python3 - <<'PY'
import importlib.util
from pathlib import Path

spec = importlib.util.spec_from_file_location("render_guide_asset", Path("scripts/render_guide_asset.py"))
module = importlib.util.module_from_spec(spec)
assert spec.loader is not None
spec.loader.exec_module(module)
registry = module._load_health_registry()
assert isinstance(registry, dict)
assert registry.get("providers") == {}
PY
rm -rf "${health_state_dir}"

bash scripts/ai/verify_m109_build_explain_companion.sh
bash scripts/ai/contract-boundary-tests.sh

run_contracts_csproj="/docker/chummercomplete/chummer.run-services/Chummer.Run.Contracts/Chummer.Run.Contracts.csproj"
if [[ -f "${run_contracts_csproj}" ]]; then
  # Warm the upstream contract graph once so transitive ref assemblies are ready
  # before this repo builds against the external run-services contract seam.
  dotnet build "${run_contracts_csproj}" --configuration Release --nologo --verbosity quiet
fi

dotnet restore Chummer.Media.Factory.slnx --nologo --verbosity quiet
dotnet build Chummer.Media.Factory.slnx --no-restore --configuration Release --nologo --verbosity quiet
pack_output_dir="$(mktemp -d "${TMPDIR:-/tmp}/chummer-media-contracts-pack.XXXXXX")"
trap 'rm -rf "$pack_output_dir"' EXIT

dotnet pack src/Chummer.Media.Contracts/Chummer.Media.Contracts.csproj \
  --no-restore \
  --configuration Release \
  --output "$pack_output_dir" \
  --nologo \
  --verbosity quiet

if ! find "$pack_output_dir" -maxdepth 1 -type f -name "*.nupkg" -print -quit | grep -q .; then
  echo "verify failed: dotnet pack produced no .nupkg artifact"
  exit 1
fi

dotnet run --project Chummer.Media.Factory.Runtime.Verify/Chummer.Media.Factory.Runtime.Verify.csproj --no-build --configuration Release --nologo --verbosity quiet
dotnet run --project tests/CampaignBriefingBundleSmoke/Chummer.Media.Factory.CampaignBriefingBundleSmoke.csproj --configuration Release --nologo --verbosity quiet
dotnet run --project tests/RunsiteOrientationBundleSmoke/Chummer.Media.Factory.RunsiteOrientationBundleSmoke.csproj --configuration Release --nologo --verbosity quiet
dotnet run --project tests/StructuredMediaRecipeSmoke/Chummer.Media.Factory.StructuredMediaRecipeSmoke.csproj --configuration Release --nologo --verbosity quiet
dotnet run --project tests/BuildExplainCompanionSmoke/Chummer.Media.Factory.BuildExplainCompanionSmoke.csproj --configuration Release --nologo --verbosity quiet
bash scripts/ai/verify_m111_install_aware_concierge.sh
dotnet run --project tests/GmPrepPacketSmoke/Chummer.Media.Factory.GmPrepPacketSmoke.csproj --configuration Release --nologo --verbosity quiet
python3 scripts/ai/materialize_media_release_proof.py --status passed

echo "verify ok"
