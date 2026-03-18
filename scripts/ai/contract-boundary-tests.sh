#!/usr/bin/env bash
set -euo pipefail

contracts_root="src/Chummer.Media.Contracts"
csproj_path="${contracts_root}/Chummer.Media.Contracts.csproj"

namespace_drift="$(
  rg -n --glob '*.cs' '^namespace ' "${contracts_root}" \
    | rg -v 'Chummer\.Media\.Contracts(\.|;)' || true
)"
if [[ -n "${namespace_drift}" ]]; then
  echo "namespace policy violation:"
  echo "${namespace_drift}"
  exit 1
fi

forbidden_dependency_drift="$(
  rg -n --glob '*.cs' '^using (Chummer\.(Engine|Core\.Engine|Play|Presentation|Ui\.Kit|Run\.Services)|OpenAI|Azure\.AI|Google\.Cloud|Amazon\.|AWSSDK)\b' "${contracts_root}" || true
)"
if [[ -n "${forbidden_dependency_drift}" ]]; then
  echo "forbidden dependency using-directive found:"
  echo "${forbidden_dependency_drift}"
  exit 1
fi

forbidden_package_drift="$(
  rg -n '<(PackageReference|ProjectReference)\s+Include="(Chummer\.(Engine|Core\.Engine|Play|Presentation|Ui\.Kit|Run\.Services)|OpenAI|Azure\.AI|Google\.Cloud|Amazon\.|AWSSDK)' "${csproj_path}" || true
)"
if [[ -n "${forbidden_package_drift}" ]]; then
  echo "forbidden project/package dependency found:"
  echo "${forbidden_package_drift}"
  exit 1
fi

forbidden_public_identifier_drift="$(
  rg -n --glob '*.cs' --glob '!**/Compatibility/**' '^\s*public\s+.*\b(Campaign(Id|Key|Context)?|Session(Id|Key|Context)?|Narrative[A-Za-z0-9_]*|Story[A-Za-z0-9_]*|Lore[A-Za-z0-9_]*|Canon[A-Za-z0-9_]*|Delivery(Policy|Channel|Route|Target|Audience|Decision)?[A-Za-z0-9_]*|Spider[A-Za-z0-9_]*|RuntimeLock[A-Za-z0-9_]*|ProviderRouting[A-Za-z0-9_]*|ProviderRoute[A-Za-z0-9_]*|Rule(Set|s)?[A-Za-z0-9_]*|Relay[A-Za-z0-9_]*)\b' "${contracts_root}" || true
)"
if [[ -n "${forbidden_public_identifier_drift}" ]]; then
  echo "render-only boundary violation in public declarations:"
  echo "${forbidden_public_identifier_drift}"
  exit 1
fi

forbidden_field_identifier_drift="$(
  rg -n --glob '*.cs' --glob '!**/Compatibility/**' '^\s*[A-Za-z_][A-Za-z0-9_<>,\.\?\[\]]*\s+(Campaign(Id|Key|Context)?|Session(Id|Key|Context)?|Narrative[A-Za-z0-9_]*|Story[A-Za-z0-9_]*|Lore[A-Za-z0-9_]*|Canon[A-Za-z0-9_]*|Delivery(Policy|Channel|Route|Target|Audience|Decision)?[A-Za-z0-9_]*|Spider[A-Za-z0-9_]*|RuntimeLock[A-Za-z0-9_]*|ProviderRouting[A-Za-z0-9_]*|ProviderRoute[A-Za-z0-9_]*|Rule(Set|s)?[A-Za-z0-9_]*|Relay[A-Za-z0-9_]*)\s*[,)]' "${contracts_root}" || true
)"
if [[ -n "${forbidden_field_identifier_drift}" ]]; then
  echo "render-only boundary violation in contract fields:"
  echo "${forbidden_field_identifier_drift}"
  exit 1
fi

for required_status in Pending Approved Rejected; do
  if ! rg -q --glob '*.cs' "^\s*${required_status}\s*=" "${contracts_root}/Assets/AssetApprovalStatus.cs"; then
    echo "lifecycle coverage violation: AssetApprovalStatus missing ${required_status}"
    exit 1
  fi
done

for required_field in ApprovalStatus ApprovedAtUtc RejectedAtUtc PersistedAtUtc; do
  if ! rg -q --glob '*.cs' "^\s*[A-Za-z0-9_<>,\\.\\?]+\\s+${required_field}\\s*[,)]" "${contracts_root}/Assets/MediaAssetLifecycleState.cs"; then
    echo "lifecycle coverage violation: MediaAssetLifecycleState missing ${required_field}"
    exit 1
  fi
done

echo "contract boundary tests ok"
