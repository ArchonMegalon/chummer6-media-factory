#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"

python3 -m unittest \
  tests.test_m115_successor_package_authority \
  tests.test_m115_replay_exchange_preview_proof \
  tests.test_replay_exchange_preview_rendering

dotnet run \
  --project tests/ReplayExchangePreviewSmoke/Chummer.Media.Factory.ReplayExchangePreviewSmoke.csproj \
  --configuration Release \
  --nologo \
  --verbosity quiet
