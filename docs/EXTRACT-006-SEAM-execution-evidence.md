# EXTRACT-006 Seam Cutover Execution Evidence

## Scope

Prove `chummer-media-factory` is the effective owner of render-job and asset-lifecycle semantics while `chummer.run-services` is constrained to orchestration ingress/egress projection.

## Evidence-gated outcome matrix

| Gate | Required artifact | Outcome | Last checked (UTC) | Evidence |
| --- | --- | --- | --- | --- |
| G1 | SEAM-01 acceptance contract has criterion-level pass/fail coverage for idempotency, lifecycle terminality, retention transitions, and lineage ownership. | pass | 2026-03-10 | `docs/EXTRACT-006-SEAM-01-seam-acceptance-contract.md` (4/4 criteria passing; `SEAM-01-C1`..`SEAM-01-C4`) |
| G2 | SEAM-02 conformance matrix defines required ingress payloads and forbidden run-services ownership paths. | pass | 2026-03-10 | `docs/EXTRACT-006-SEAM-02-run-services-handoff-conformance-matrix.md` (`M-ING-01`..`M-ING-08`) |
| G3 | SEAM-03 checklist has a completed rehearsal run for submit/retry/approval/persist/reject/retention/signed-URL egress with blocker capture. | pass | 2026-03-10 | `docs/EXTRACT-006-SEAM-03-cutover-rehearsal-checklist.md` (run `SEAM-03B-2026-03-10T11:31:45Z`; checks `SEAM-03-CHK-01`..`SEAM-03-CHK-05` all pass) |
| G4 | SEAM-04 renderer move-in gate blocks completion if seam evidence fails or regresses. | pass | 2026-03-10 | `docs/EXTRACT-006-SEAM-04-renderer-move-in-gate.md` (gate rule + non-regression policy) |

## Consolidated pass/fail summary

- Passing gates: 4/4 (`G1`, `G2`, `G3`, `G4`)
- Failing gates: 0/4
- Blocked gates: 0/4
- Overall EXTRACT-006 status: pass

## Conformance decision

- Effective seam owner for render-job and asset-lifecycle semantics: `chummer-media-factory` (pass).
- `chummer.run-services` scope at seam: orchestration ingress and egress projection only (pass).
- Blocker-derived follow-on queue items required from latest rehearsal run: none.
