# EXTRACT-006 SEAM-04 Renderer Move-In Gate Enforcement

## Purpose

Enforce a hard gate: document, portrait, and video migration cannot be marked complete while seam ownership evidence is missing, failing, or regressed.

This keeps renderer move-in subordinate to seam proof that `chummer-media-factory` is the effective render-job and asset-lifecycle owner.

## Gate rule

Renderer migration completion is blocked unless all conditions below are true:

1. `SEAM-01` acceptance contract exists with explicit pass/fail outcomes for idempotency, lifecycle terminality, retention transitions, and lineage lookup ownership.
2. `SEAM-02` handoff matrix remains conformant with no open forbidden-ownership violations in run-services.
3. `SEAM-03` rehearsal checklist has a latest run with pass outcomes for submit, retry, approval, persist, reject, retention, and signed-URL egress checks.
4. A non-regression check confirms no previously passing seam criterion has moved back to fail or unknown.

If any condition is false, all renderer stages (`R1` document, `R2` portrait, `R3` video) stay in `blocked` state for move-in completion.

## Evidence contract

The gate evaluates these artifacts:

- `docs/EXTRACT-006-SEAM-01-seam-acceptance-contract.md`
- `docs/EXTRACT-006-SEAM-02-run-services-handoff-conformance-matrix.md`
- `docs/EXTRACT-006-SEAM-03-cutover-rehearsal-checklist.md`
- `WORKLIST.md` milestone status lines for `EXTRACT-006` and renderer move-in references

Missing artifact, stale status, or unresolved seam blocker is a gate failure.

## Non-regression policy

- Record the latest seam evidence date for each criterion.
- Compare against the immediately previous recorded run.
- Any pass-to-fail transition is a regression and blocks renderer move-in completion.
- Any pass-to-unknown transition caused by missing evidence is treated as a regression until corrected.

## Enforcement mapping

- `EXTRACT-004` renderer sequence is execution order guidance only; completion authority is gated by this SEAM-04 policy.
- `EXTRACT-006` remains queued/open until `SEAM-01` through `SEAM-03` are passing and this gate reports non-regressed status.
- Queue entries for renderer migration completion must not be closed while SEAM-04 gate status is failing.

## Gate decision table

| Condition | Status | Move-in completion allowed? |
| --- | --- | --- |
| All seam conditions passing and non-regressed | pass | yes |
| Any seam condition failing | fail | no |
| Evidence missing/stale for any required artifact | fail | no |
| Prior pass regressed to fail/unknown | fail | no |

## Traceability

- `docs/EXTRACT-006-run-services-seam-cutover-backlog.md`
- `docs/EXTRACT-004-renderer-move-in-sequence.md`
- `docs/EXTRACT-005-scaffold-exit-seam-ownership-check.md`
- `docs/EXTRACT-006-SEAM-02-run-services-handoff-conformance-matrix.md`
