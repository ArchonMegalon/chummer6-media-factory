# EXTRACT-006 SEAM-03 Cutover Rehearsal Checklist

## Purpose

Execute and record seam rehearsal outcomes for submit/retry/approval/persist/reject/retention/signed-URL egress checks so cutover status is evidence-backed.

## Executable checks

| Check ID | Check | Expected outcome | Evidence source |
| --- | --- | --- | --- |
| SEAM-03-CHK-01 | Submit ingress uses `Chummer.Media.Contracts` render DTOs and stable idempotency keys. | pass | `docs/EXTRACT-006-SEAM-02-run-services-handoff-conformance-matrix.md` (`M-ING-01`, `M-ING-05`), `src/Chummer.Media.Contracts/Rendering/MediaRenderRequest.cs`, `src/Chummer.Media.Contracts/Kernel/RenderJobSubstrateContracts.cs` |
| SEAM-03-CHK-02 | Retry replay semantics stay media-factory-owned. | pass | `docs/EXTRACT-006-SEAM-02-run-services-handoff-conformance-matrix.md` (`M-ING-05`, `M-ING-08`), `src/Chummer.Media.Contracts/Jobs/RenderJobDedupeScope.cs` |
| SEAM-03-CHK-03 | Approval/persist/reject lifecycle transitions remain media-factory-owned. | pass | `docs/EXTRACT-006-SEAM-02-run-services-handoff-conformance-matrix.md` (`M-ING-06`), `src/Chummer.Media.Contracts/Assets/AssetApprovalStatus.cs`, `src/Chummer.Media.Contracts/Assets/MediaAssetLifecycleState.cs` |
| SEAM-03-CHK-04 | Retention sweep and purge-transition contracts are owned by media-factory and include seam-visible transition markers. | pass | `src/Chummer.Media.Contracts/Kernel/RetentionSweepContracts.cs`, `src/Chummer.Media.Contracts/Assets/MediaAssetLifecycleState.cs`, command evidence in latest run |
| SEAM-03-CHK-05 | Signed-URL/status egress remains projection-only in run-services. | pass | `docs/EXTRACT-006-SEAM-02-run-services-handoff-conformance-matrix.md` (`M-ING-07`) |

## Latest run

- Run ID: `SEAM-03B-2026-03-10T11:31:45Z`
- Run date (UTC): `2026-03-10`
- Owner: `media-factory`
- Scope: contract-plane seam rehearsal for run-services handoff window evidence

| Check ID | State | Notes |
| --- | --- | --- |
| SEAM-03-CHK-01 | pass | Ingress/idempotency contract ownership remains in media-factory DTO plane and seam matrix rules. |
| SEAM-03-CHK-02 | pass | Retry replay and dedupe semantics remain contract-defined in media-factory, with run-services forbidden from redefining queue semantics. |
| SEAM-03-CHK-03 | pass | Approval/persist/reject lifecycle coverage remains media-factory-owned with required lifecycle fields present. |
| SEAM-03-CHK-04 | pass | Retention/purge transition contract fields are present and boundary checks passed in this run (evidence commands below). |
| SEAM-03-CHK-05 | pass | Signed-URL/status egress remains projection-only per seam matrix. |

## Retention evidence commands (latest run)

```bash
date -u +"%Y-%m-%dT%H:%M:%SZ"
# 2026-03-10T11:31:45Z

bash scripts/ai/contract-boundary-tests.sh
# contract boundary tests ok

rg -n "RetentionSweep(Request|AssetTransition|Result)|IncludePurge|PurgedCount|PurgedAtUtc|ExpiresAtUtc|MarkedPurgeCandidateAtUtc" \
  src/Chummer.Media.Contracts/Kernel/RetentionSweepContracts.cs \
  src/Chummer.Media.Contracts/Assets/MediaAssetLifecycleState.cs \
  docs/EXTRACT-006-SEAM-02-run-services-handoff-conformance-matrix.md
```

Observed retention output:

- `src/Chummer.Media.Contracts/Kernel/RetentionSweepContracts.cs` includes `RetentionSweepRequest`, `IncludePurge`, `RetentionSweepAssetTransition`, `MarkedPurgeCandidateAtUtc`, `PurgedAtUtc`, `RetentionSweepResult`, `PurgedCount`.
- `src/Chummer.Media.Contracts/Assets/MediaAssetLifecycleState.cs` includes `ExpiresAtUtc` and `PurgedAtUtc`.

## Blocker capture format

If any check fails or becomes blocked in a future run, append one queue follow-on with:

- owner
- failed/blocked check ID
- concrete next action
- evidence target path
- date recorded (UTC)

## Blocker-derived queue follow-ons (latest run)

- None. `SEAM-03B-2026-03-10T11:31:45Z` had no `fail` or `blocked` checks.
