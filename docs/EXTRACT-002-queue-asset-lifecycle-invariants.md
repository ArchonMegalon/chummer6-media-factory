# EXTRACT-002 Queue and Asset Lifecycle Invariants

## Purpose

Define non-negotiable invariants for queue/job execution and asset lifecycle state so extraction work preserves ownership boundaries and terminal-state correctness.

## Scope and ownership boundaries

`chummer-media-factory` owns:
- render job queue execution state
- dedupe scope/key interpretation
- retry scheduling and replay safety
- asset lifecycle persistence for approval/persist/reject outcomes

`chummer.run-services` owns:
- decision that render work should exist
- approval policy (who can approve/reject and under what rules)
- delivery policy after approved asset metadata is returned

## Queue invariants

### Q1. Dedupe key authority

- `DedupeKey` on `RenderJobContract` is the dedupe identity consumed by factory queue workers.
- Upstream may provide idempotency identity, but factory is the authority for queue-level dedupe behavior.
- Dedupe decisions must not depend on UI/session/campaign tables.

### Q2. Dedupe scope authority

- `DedupeScope` must be one of `Request`, `TemplateVersion`, or `OutputAsset`.
- Scope interpretation is local to `chummer-media-factory` queue semantics and must remain deterministic for the same contract payload.
- The scope selected for a job must be sufficient to prevent duplicate heavy render execution in that scope.

### Q3. Job timing and retry fields

- `CreatedAtUtc` is immutable creation time.
- `AvailableAtUtc` is the earliest claim time.
- `LastAttemptedAtUtc` is written only after an execution attempt starts.
- `RetryAfterUtc` is set only when another attempt is expected; it must be null for terminal jobs.
- `AttemptCount` must be monotonic and never exceed `MaxAttemptCount`.

### Q4. Queue terminal states

`RenderJobStatus` terminal states are:
- `Succeeded`
- `Failed`
- `Cancelled`

Terminal expectations:
- `CompletedAtUtc` must be set for terminal jobs.
- Terminal jobs are never re-queued.
- `SupersededByRenderJobId` may be set for dedupe/supersession lineage and must never mutate output bytes.

## Asset lifecycle invariants

### A1. Approval status ownership

- `AssetApprovalStatus` values are `Pending`, `Approved`, and `Rejected`.
- Factory persists approval outcomes as state; it does not own approval policy.
- Approval status transitions must be auditable without campaign/session truth dependency.

### A2. Terminal-state expectations for approval/persist/reject

- `Pending` is non-terminal.
- `Rejected` is terminal for approval flow. `RejectedAtUtc` must be set and `PersistedAtUtc` must remain null.
- `Approved` is terminal for approval flow; persistence can still be pending.
- `PersistedAtUtc` set with `Approved` indicates persisted/canonical-ready asset state.

### A3. Timestamp coherence

- `CreatedAtUtc` is always present.
- `ApprovedAtUtc` is present only when status is `Approved`.
- `RejectedAtUtc` is present only when status is `Rejected`.
- `PersistedAtUtc` implies status `Approved`.
- `PurgedAtUtc` implies retention/purge completion and no further lifecycle mutation except audit annotation.

### A4. Byte immutability and lineage

- Lifecycle transitions (`Pending` -> `Approved`/`Rejected`, persist, expire, purge) mutate manifest state only.
- Rendered binaries remain immutable once written.
- Lineage links remain stable across retries, supersession, and approval/persist/reject transitions.

## Retry and lifecycle interaction invariants

- Retries operate on job execution state, not on post-render approval policy.
- A retry may create a successor job, but must preserve dedupe and lineage auditability.
- Approval/persist/reject transitions must be idempotent under replayed messages/events.
- No transition may require provider SDK types in contracts.

## Review checklist for this slice

- [ ] Dedupe key and scope ownership are explicitly described as factory-owned queue semantics.
- [ ] Retry timing field expectations are explicit and internally consistent.
- [ ] Terminal queue states are explicit and include completion timestamp rules.
- [ ] Approval/persist/reject terminal-state expectations are explicit and coherent with contract enums/fields.
- [ ] No narrative-authoring, policy-routing, or campaign/session ownership leakage is introduced.
