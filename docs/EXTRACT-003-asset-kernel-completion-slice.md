# EXTRACT-003 Asset Kernel Completion Slice

## Purpose

Materialize executable backlog for completing the shared asset kernel before renderer-specific move-in work.

This slice does not move document/portrait/video execution yet. It defines concrete, verifiable work items for the remaining kernel seams.

## Ownership boundary

`chummer-media-factory` owns:
- manifest persistence wiring for render-only asset state
- binary storage adapter seam for immutable output bytes
- preview linkage between canonical assets and derived previews
- TTL/retention sweep contracts and lifecycle transition signals
- lineage traversal contracts for supersession and derived-asset trees

`chummer.run-services` remains owner of:
- approvals policy decisions
- delivery policy
- campaign/session orchestration

## Executable backlog statements

### K1. Manifest persistence wiring

Implement a manifest persistence contract set that can upsert, read, and update render-only lifecycle metadata without provider SDK leakage.

Acceptance checks:
- [ ] Contract plane includes request/response DTOs for manifest write and fetch operations.
- [ ] Persistence DTOs operate on `MediaAssetManifest` identity and lifecycle fields only.
- [ ] No campaign/session, delivery, or narrative DTO fields appear in persistence contracts.

### K2. Binary storage adapter seam

Define adapter contracts that represent binary object write/read/delete intent through storage-agnostic descriptors.

Acceptance checks:
- [ ] Contract plane includes binary object location and write descriptor DTOs.
- [ ] Storage seam is provider-agnostic (no SDK types, no filesystem path coupling).
- [ ] Content-hash and content-length invariants are represented at the seam.

### K3. Preview linkage

Define explicit linkage contracts between primary assets and preview/thumbnail derivatives so preview lookup does not depend on UI policy.

Acceptance checks:
- [ ] Contract plane includes preview link create/update/read DTOs.
- [ ] Linkage identifies source asset id and preview asset id with deterministic relationship type.
- [ ] Preview linkage keeps delivery and visibility policy out of media contracts.

### K4. TTL/retention sweep contract

Define retention sweep trigger/result contracts for expiration, purge eligibility, and lifecycle transition reporting.

Acceptance checks:
- [ ] Contract plane includes retention sweep request/result DTOs with UTC watermark inputs.
- [ ] Contract fields cover `ExpiresAtUtc`, purge candidate identity, and terminal purge reporting.
- [ ] Sweep contracts are idempotent and policy-free (no user-role or approval-rule semantics).

### K5. Lineage traversal contract

Define lineage traversal query/result contracts so supersession and derivation chains can be fetched deterministically.

Acceptance checks:
- [ ] Contract plane includes lineage traversal request/result DTOs.
- [ ] Traversal can resolve parent, children, and supersession relationships without mutating assets.
- [ ] Contracts preserve render-only ownership and avoid upstream orchestration payloads.

## Exit condition for EXTRACT-003

This slice is complete when backlog for K1-K5 is explicitly present and executable in repo-local planning artifacts, with acceptance checks that can be implemented without ambiguity.

## Follow-on dependency note

EXTRACT-004 (renderer move-in sequence) depends on K1-K5 backlog completion and should not start until asset-kernel seam contracts above are landed.
