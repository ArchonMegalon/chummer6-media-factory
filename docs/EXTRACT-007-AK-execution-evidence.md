# EXTRACT-007 AK-01..AK-06 Execution Evidence

Date: 2026-03-10

This evidence file maps the shared asset-kernel execution backlog to implemented `Chummer.Media.Contracts` surfaces.

## AK-01 Manifest store wiring

- Implemented contracts:
  - `src/Chummer.Media.Contracts/Kernel/ManifestStoreContracts.cs`
- Evidence:
  - create/get/update lifecycle-only request/result DTOs exist with version checks and mutation reason fields.

## AK-02 Binary storage adapter validation

- Implemented contracts:
  - `src/Chummer.Media.Contracts/Storage/BinaryStorageContracts.cs`
- Evidence:
  - provider-neutral `BinaryLocator` plus write/read/delete request/result DTOs include content-length and hash verification flags.

## AK-03 Render-job substrate transitions

- Implemented contracts:
  - `src/Chummer.Media.Contracts/Kernel/RenderJobSubstrateContracts.cs`
- Evidence:
  - submit/claim/complete/retry/supersede transition DTOs include idempotency and dedupe-related fields with explicit rejection reasons.

## AK-04 Preview linkage

- Implemented contracts:
  - `src/Chummer.Media.Contracts/Kernel/PreviewLinkContracts.cs`
- Evidence:
  - deterministic source-to-preview link DTOs and preview-chain query/result contracts landed.

## AK-05 TTL/retention sweeps

- Implemented contracts:
  - `src/Chummer.Media.Contracts/Kernel/RetentionSweepContracts.cs`
- Evidence:
  - idempotent sweep key/watermark request fields and transition reporting DTOs landed with lifecycle-only mutation marker.

## AK-06 Lineage traversal

- Implemented contracts:
  - `src/Chummer.Media.Contracts/Lineage/AssetLineageContracts.cs`
- Evidence:
  - deterministic lineage query/result shape with explicit nodes and typed edges landed.

## Verification

- Run `scripts/ai/verify.sh` to validate namespace and render-only guardrails and compile the contract package.
