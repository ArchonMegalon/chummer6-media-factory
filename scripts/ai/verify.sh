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
test -f src/Chummer.Media.Contracts/Chummer.Media.Contracts.csproj
test -f src/Chummer.Media.Contracts/ContractsAssemblyMarker.cs
test -f src/Chummer.Media.Contracts/README.md
test -f docs/chummer-media-factory.design.v1.md
test -f docs/EXTRACT-008-DS-execution-evidence.md
test -f scripts/ai/contract-boundary-tests.sh
test -f scripts/render_guide_asset.py

python3 -m py_compile scripts/render_guide_asset.py
python3 scripts/render_guide_asset.py --prompt "media factory dry run" --output /tmp/chummer-media-factory-dry-run.png --width 1600 --height 900 --dry-run >/dev/null

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

echo "verify ok"
