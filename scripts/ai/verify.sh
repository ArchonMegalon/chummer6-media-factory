#!/usr/bin/env bash
set -euo pipefail

export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-/tmp/chummer-media-factory-dotnet}"
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_NOLOGO=1

test -f README.md
test -f AGENTS.md
test -f WORKLIST.md
test -f Directory.Build.props
test -f Chummer.Media.Factory.slnx
test -f Chummer.Media.Factory.Runtime.Verify/Chummer.Media.Factory.Runtime.Verify.csproj
test -f src/Chummer.Media.Contracts/Chummer.Media.Contracts.csproj
test -f src/Chummer.Media.Contracts/ContractsAssemblyMarker.cs
test -f src/Chummer.Media.Contracts/README.md
test -f docs/chummer-media-factory.design.v1.md
test -f docs/MEDIA_ADAPTER_MATRIX.md
test -f docs/MEDIA_FACTORY_RESTORE_RUNBOOK.md
test -f docs/EXTRACT-008-DS-execution-evidence.md
test -f scripts/ai/contract-boundary-tests.sh
test -f scripts/render_guide_asset.py

rg -n 'media_factory_state_backup_v1|Chummer\.Media\.Factory\.Runtime\.Verify|retention sweep' docs/MEDIA_FACTORY_RESTORE_RUNBOOK.md >/dev/null
rg -n 'CHUMMER_MEDIA_FACTORY_IMAGE_BACKEND|CHUMMER_MEDIA_FACTORY_ENABLE_IMAGE_EXECUTION|preview image|archive / retention storage' docs/MEDIA_ADAPTER_MATRIX.md >/dev/null

python3 -m py_compile scripts/render_guide_asset.py
python3 scripts/render_guide_asset.py --prompt "media factory dry run" --output /tmp/chummer-media-factory-dry-run.png --width 1600 --height 900 --dry-run >/dev/null
CHUMMER_MEDIA_FACTORY_ENABLE_IMAGE_EXECUTION=0 python3 scripts/render_guide_asset.py --prompt "media factory disabled dry run" --output /tmp/chummer-media-factory-disabled-dry-run.png --width 1600 --height 900 --dry-run | rg -n '"image_execution_enabled": false|"backend_provider": "disabled"' >/dev/null

bash scripts/ai/contract-boundary-tests.sh

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

echo "verify ok"
