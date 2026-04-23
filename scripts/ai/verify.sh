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
test -f src/Chummer.Media.Contracts/Chummer.Media.Contracts.csproj
test -f src/Chummer.Media.Contracts/ContractsAssemblyMarker.cs
test -f src/Chummer.Media.Contracts/README.md
test -f src/Chummer.Media.Factory.Runtime/Assets/AssetLifecycleService.cs
test -f src/Chummer.Media.Factory.Runtime/Assets/MediaRenderJobService.cs
test -f docs/chummer-media-factory.design.v1.md
test -f docs/MEDIA_ADAPTER_MATRIX.md
test -f docs/MEDIA_CAPABILITY_SIGNOFF.md
test -f docs/MEDIA_FACTORY_RESTORE_RUNBOOK.md
test -f docs/EXTRACT-008-DS-execution-evidence.md
test -f scripts/ai/contract-boundary-tests.sh
test -f scripts/ai/materialize_media_release_proof.py
test -f scripts/render_guide_asset.py

rg -n 'media_factory_state_backup_v1|Chummer\.Media\.Factory\.Runtime\.Verify|retention sweep' docs/MEDIA_FACTORY_RESTORE_RUNBOOK.md >/dev/null
rg -n 'CHUMMER_MEDIA_FACTORY_IMAGE_BACKEND|CHUMMER_MEDIA_FACTORY_ENABLE_IMAGE_EXECUTION|preview image|archive / retention storage|Receipts must record the actual selected backend' docs/MEDIA_ADAPTER_MATRIX.md >/dev/null
rg -n 'DocumentPdf|DocumentThumbnailImage|PortraitImageVariant|NarrativeBriefVideo|RouteCinemaResult|AssetLifecyclePolicy|CHUMMER_MEDIA_FACTORY_IMAGE_BACKEND|CHUMMER_MEDIA_FACTORY_ENABLE_IMAGE_EXECUTION' docs/MEDIA_CAPABILITY_SIGNOFF.md >/dev/null
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
python3 scripts/ai/materialize_media_release_proof.py --status passed

echo "verify ok"
