# EXTRACT-006 SEAM-01 Seam Acceptance Contract

## Purpose

Establish criterion-level seam acceptance status proving `chummer-media-factory` owns render-job and asset-lifecycle semantics through `Chummer.Media.Contracts`, while `chummer.run-services` remains orchestration-only.

## Criterion Status

| Criterion ID | Criterion | State | Owner | Last checked (UTC) | Evidence | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| SEAM-01-C1 | Intake idempotency is contract-owned by media-factory. | pass | media-factory | 2026-03-10 | `src/Chummer.Media.Contracts/Kernel/RenderJobSubstrateContracts.cs` (`SubmitRenderJobRequest.IdempotencyKey`, `SubmitRenderJobResult.ReplayedFromIdempotencyKey`); `docs/EXTRACT-002-queue-asset-lifecycle-invariants.md` (Q1/Q2 + replay/idempotency invariants); `docs/EXTRACT-006-SEAM-02-run-services-handoff-conformance-matrix.md` (M-ING-01, M-ING-05) | Contract surface and seam matrix both bind idempotency and dedupe semantics to media-factory contracts. |
| SEAM-01-C2 | Lifecycle terminality (approval/persist/reject and queue terminal states) is factory-owned and testable from contract state. | pass | media-factory | 2026-03-10 | `src/Chummer.Media.Contracts/Jobs/RenderJobStatus.cs`; `src/Chummer.Media.Contracts/Jobs/RenderJobContract.cs` (`CompletedAtUtc`); `src/Chummer.Media.Contracts/Assets/MediaAssetLifecycleState.cs`; `docs/EXTRACT-002-queue-asset-lifecycle-invariants.md` (Q4, A1-A3); `docs/EXTRACT-006-SEAM-02-run-services-handoff-conformance-matrix.md` (M-ING-06) | Terminal-state semantics and mutation authority are defined in repo-local contracts/invariants, not run-services policy. |
| SEAM-01-C3 | Retention transitions are media-factory-owned and exercised at seam without run-services semantic override. | pass | media-factory | 2026-03-10 | `src/Chummer.Media.Contracts/Kernel/RetentionSweepContracts.cs`; `src/Chummer.Media.Contracts/Assets/MediaAssetLifecycleState.cs` (`ExpiresAtUtc`, `PurgedAtUtc`); `docs/EXTRACT-006-SEAM-02-run-services-handoff-conformance-matrix.md` (`M-ING-06`, retention ownership constraints); `docs/EXTRACT-006-SEAM-03-cutover-rehearsal-checklist.md` (`SEAM-03B-2026-03-10T11:31:45Z`, `SEAM-03-CHK-04`) | Retention sweep + purge-transition rehearsal evidence is recorded in SEAM-03 with pass outcome for retention checks. |
| SEAM-01-C4 | Lineage lookup ownership is in `Chummer.Media.Contracts` and remains media-factory canonical. | pass | media-factory | 2026-03-10 | `src/Chummer.Media.Contracts/Lineage/AssetLineageContracts.cs`; `src/Chummer.Media.Contracts/README.md` (deterministic lineage traversal contracts); `.codex-design/repo/IMPLEMENTATION_SCOPE.md`; `.codex-design/product/OWNERSHIP_MATRIX.md` | Lineage query/result and relation contracts are local to media-factory contract plane and mapped to repo ownership guidance. |

## Acceptance Summary

- Passing criteria: 4/4 (`SEAM-01-C1`, `SEAM-01-C2`, `SEAM-01-C3`, `SEAM-01-C4`)
- Failing criteria: 0/4
- Blocked criteria: 0/4
- Overall SEAM-01 status: pass.

## Follow-on Wiring

`SEAM-01-C3` follow-on `EXTRACT-006/SEAM-01B/C3` is satisfied by recorded retention rehearsal evidence:
- evidence run: `SEAM-03B-2026-03-10T11:31:45Z`
- evidence target: `docs/EXTRACT-006-SEAM-03-cutover-rehearsal-checklist.md` (`SEAM-03-CHK-04`)
- criterion state: pass

SEAM-01B recheck (2026-03-10 UTC): current criterion states contain zero `fail` and zero `blocked` entries, so no additional owner-scoped follow-on queue items were appended.
