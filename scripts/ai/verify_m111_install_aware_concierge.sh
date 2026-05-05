#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"

python3 -m unittest \
  tests.test_m111_successor_package_authority \
  tests.test_m111_install_aware_concierge_proof \
  tests.test_install_aware_concierge_rendering

dotnet run \
  --project tests/InstallAwareConciergeSmoke/Chummer.Media.Factory.InstallAwareConciergeSmoke.csproj \
  --configuration Release \
  --nologo \
  --verbosity quiet
