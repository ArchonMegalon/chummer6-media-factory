# EXTRACT-005 Scaffold Exit Seam Ownership Check

## Purpose

Define scaffold-stage exit criteria that proves `chummer.run-services` no longer owns the effective media seam for render-only jobs and asset lifecycle.

This check is a boundary-proof gate, not a renderer feature-delivery gate.

## Ownership assertion under test

At scaffold exit:
- `chummer-media-factory` is the canonical owner of render-job and asset-lifecycle contract semantics via `Chummer.Media.Contracts`.
- `chummer.run-services` may submit work and consume results, but does not define queue/lifecycle semantics or execute render-provider ownership paths.

## Required evidence set

All criteria below must pass.

### E1. Contract-plane authority is local and render-only

Pass checks:
- `Chummer.Media.Contracts` contains queue/job and asset lifecycle DTOs needed for render execution semantics.
- DTOs remain render-only and exclude narrative-authoring, canon-generation, rules/session relay, and delivery policy concerns.
- Contract families do not depend on engine implementation, play implementation, UI-kit, provider SDK, or storage implementation types.

Fail if:
- run-services-local DTOs are still the source of truth for media queue/lifecycle meaning.
- media contracts in this repo add orchestration policy or campaign/session truth fields.

### E2. Queue/lifecycle invariants are factory-owned

Pass checks:
- Queue ownership rules for dedupe key/scope, retry timing, and terminal job states are documented in local extraction artifacts and align with contract fields.
- Asset lifecycle invariants include approval/persist/reject coverage with coherent timestamp/terminal-state rules.
- No invariant requires run-services internals to interpret factory job or asset terminality.

Fail if:
- queue dedupe/retry semantics are defined as run-services policy.
- lifecycle terminality cannot be evaluated from factory-owned contract state.

### E3. Handoff seam is one-way orchestration, not execution ownership

Pass checks:
- run-services role is limited to render-intent submission and downstream attachment/delivery after outcomes.
- media-factory role is explicit for render execution state, manifest state, binary lineage metadata, and retention lifecycle transitions.
- integration checklist clearly separates ingress, lifecycle-state persistence handoff, and metadata egress responsibilities.

Fail if:
- run-services owns direct render execution semantics or asset lifecycle mutation authority.
- seam documentation leaves ownership ambiguous for approval-state persistence or lifecycle transitions.

### E4. Kernel-first sequencing blocks premature renderer ownership drift

Pass checks:
- asset-kernel completion backlog (manifest persistence, storage seam, preview linkage, retention sweep contract, lineage traversal) exists before renderer move-in.
- renderer move-in order and gates are documented as document -> portrait -> video after kernel preconditions.
- stage gate language preserves the render-only boundary and blocks policy/scope leakage.

Fail if:
- renderer move-in can proceed without kernel seam completion.
- stage gates allow run-services policy ownership to re-enter media execution contracts.

## Scaffold exit decision rule

Scaffold-stage extraction exit for seam ownership is approved only when:
- E1-E4 pass with no fail conditions, and
- no P1 boundary violations are open under repo review guidance.

If any criterion fails, keep slice open and publish the failed criterion as executable queue work before advancing split stages.

## Traceability

- `docs/MF-005-service-seams-and-handoffs.md`
- `docs/EXTRACT-002-queue-asset-lifecycle-invariants.md`
- `docs/EXTRACT-003-asset-kernel-completion-slice.md`
- `docs/EXTRACT-004-renderer-move-in-sequence.md`
- `WORKLIST.md`
