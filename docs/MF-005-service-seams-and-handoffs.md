# MF-005 Service Seams and Integration Handoff Checklist

## Purpose

Define enforceable seam ownership for media render execution extraction and provide a checklist that can be used during implementation and review.

## Service seam ownership

### `chummer.run-services` (upstream orchestration owner)

Owns:
- campaign/session context gathering
- narrative drafting and script/text authoring
- approvals policy decisions (who may approve, when, and why)
- delivery policy decisions (where approved media is sent)
- submission of render-only requests into `media-factory`

Must not own:
- renderer execution workers
- render-job queue internals (claim/dedupe/retry/terminal execution state)
- media binary lifecycle transitions in object storage

### `chummer-media-factory` (this repo)

Owns:
- render request intake validation for render-only DTOs
- render job execution, dedupe, retry, provider-run tracking
- asset manifest persistence, preview linkage, retention state, lineage tracking
- approval-state persistence fields on rendered assets (`pending`, `approved`, `rejected`, `persisted`)
- signed URL issuance and render receipt/status projection

Must not own:
- campaign/session truth
- canon/rules/lore decisions
- approvals policy rules
- delivery policy
- Spider or general AI routing

### `chummer-hub-registry` (publication owner)

Owns:
- publication and reusable artifact metadata once an asset is promoted
- immutable registry records for reusable/public assets

Must not own:
- per-session render execution lifecycle
- provider render operations

### `chummer-presentation` and `chummer-play` (consumer surfaces)

Own only:
- read/display of upstream-approved asset handles, status DTOs, and previews

Must not own:
- direct render-provider calls
- provider secrets
- media lifecycle mutation logic

## Integration handoff checklist

Use this list when validating extraction PRs, test plans, and rollout readiness.

### A. Render request ingress handoff (`run-services` -> `media-factory`)

- [ ] Ingress payload uses only `Chummer.Media.Contracts` render/job/asset DTOs.
- [ ] No narrative-authoring fields, canon/rules payloads, session relay internals, or provider-routing policy fields cross the seam.
- [ ] Idempotency key ownership is explicit at ingress and mapped to dedupe scope/key in media-factory job records.
- [ ] Intake validation rejects non-render-only payloads with structured failure envelopes.
- [ ] Ownership line is explicit: `run-services` decides _that_ render work should exist; `media-factory` decides _how_ render work executes.

### B. Approval-state persistence handoff (`run-services` policy -> `media-factory` state)

- [ ] Approval policy remains upstream; only resulting state transitions are persisted in media-factory manifests.
- [ ] Asset lifecycle covers approval/persist/reject end-to-end with no missing terminal path.
- [ ] Manifest and catalog records preserve lineage when approval status changes (no binary mutation).
- [ ] Persist/reject transitions are auditable without pulling campaign/session tables into this repo.
- [ ] State changes are idempotent and safe under retries/replays.

### C. Asset metadata egress handoff (`media-factory` -> downstream consumers)

- [ ] Egress contracts contain asset metadata, lifecycle state, preview handles, and signed access data only.
- [ ] Egress excludes delivery policy, audience authorization policy, and narrative semantics.
- [ ] `hub-registry` ingestion path receives promotion-ready metadata without inheriting render-worker internals.
- [ ] `presentation`/`play` consumption path uses approved handles only and never provider credentials.
- [ ] Egress lineage identifiers are stable enough for trace/audit correlation across repos.

## Extraction verification evidence

A seam extraction is considered verified when all are true:

- checklist items A/B/C are complete in PR review
- `scripts/ai/verify.sh` passes in this repo
- `docs/EXTRACT-008-DS-execution-evidence.md` remains current for DTO boundary inventory/guardrail evidence
- no new contract in `Chummer.Media.Contracts` introduces narrative-authoring, campaign/session truth, rules logic, or UI/provider SDK coupling
