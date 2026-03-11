# EXTRACT-006 Run-Services Seam Cutover Backlog

## Objective

Execute the scaffold-to-live seam cutover so `chummer-media-factory` becomes the effective owner of render-job and asset-lifecycle execution semantics while `chummer.run-services` is limited to orchestration ingress and result consumption.

## Runnable backlog

1. SEAM-01 Publish media-factory seam acceptance contract
- Define acceptance checks for intake idempotency, lifecycle terminality, retention transitions, and lineage lookup owned by `Chummer.Media.Contracts`.
- Record evidence locations and pass/fail criteria so run-services does not remain the semantic source of truth.

2. SEAM-02 Add run-services handoff conformance matrix
- Enumerate required upstream calls and payloads into media-factory for each media type class.
- Enumerate forbidden ownership paths in run-services: provider execution, lifecycle mutation authority, and queue semantic definition.
- Evidence: `docs/EXTRACT-006-SEAM-02-run-services-handoff-conformance-matrix.md`

3. SEAM-03 Add executable cutover rehearsal checklist
- Run a dry-run sequence covering submit, retry, approval/persist/reject, retention expiry, and signed-URL metadata egress.
- Capture blocker outputs as follow-on queue items if any seam check fails.

4. SEAM-04 Gate renderer move-in on seam evidence
- Require EXTRACT-006 evidence completion before any document/portrait/video execution migration is marked done.
- Keep renderer migration blocked if seam ownership checks regress.
- Evidence: `docs/EXTRACT-006-SEAM-04-renderer-move-in-gate.md`

## Runnable append (2026-03-10)

5. SEAM-01A Draft seam acceptance artifact with executable pass/fail table
- Create `docs/EXTRACT-006-SEAM-01-seam-acceptance-contract.md` with criteria rows for intake idempotency, lifecycle terminality, retention transitions, and lineage lookup ownership.
- Include evidence source links, owner, last-checked date, and current state (`pass`/`fail`/`blocked`).

6. SEAM-01B Wire seam acceptance status to queue follow-ons
- For each `fail` or `blocked` criterion in SEAM-01, append one owner-scoped runnable queue item with explicit next action and evidence target.
- Keep queue entries scoped to seam ownership only (no narrative/approval-policy expansion).

7. SEAM-03A Publish cutover rehearsal checklist artifact
- Create `docs/EXTRACT-006-SEAM-03-cutover-rehearsal-checklist.md` with executable checks for submit, retry replay, approval/persist/reject transitions, retention sweep visibility, and signed-URL egress projection.
- Capture expected evidence sources and blocker capture format for follow-on queue publication.

8. SEAM-03B Execute rehearsal once run-services window is available
- Run the SEAM-03 checklist end-to-end against the current ingress/egress seam.
- Record outcomes per check and append blocker-derived queue items for any failed or blocked step.

## Completion evidence

- Updated seam conformance artifact with explicit pass/fail outcomes.
- Queue entries for any failed criterion with owner and next action.
- Consolidated evidence-gated pass/fail decision in `docs/EXTRACT-006-SEAM-execution-evidence.md` (G1..G4).
- DTO boundary guardrail evidence remains green in `docs/EXTRACT-008-DS-execution-evidence.md` and `scripts/ai/verify.sh`.
- Worklist status moved from queued to completed only after all checks pass.
