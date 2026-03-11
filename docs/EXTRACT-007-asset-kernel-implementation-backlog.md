# EXTRACT-007 Asset Kernel Implementation Backlog

## Objective

Execute the shared asset kernel implementation backlog so renderer move-in work remains blocked until manifests, binary storage, render jobs, previews, TTL/retention, and lineage are all live behind `Chummer.Media.Contracts`.

## Runnable backlog

1. AK-01 Manifest store implementation seam
- Implement manifest write/read/update paths for immutable asset metadata and mutable lifecycle state.
- Enforce render-only manifest fields: asset identity, content hash, binary locator, preview refs, TTL/retention fields, and lineage refs.
- Evidence: passing contract tests for create/get/update manifest flows and lifecycle-only mutations.

2. AK-02 Binary storage adapter + checksum enforcement
- Implement storage-agnostic binary write/read/delete adapter contracts with provider-neutral locators.
- Enforce content-length and content-hash validation at ingest and retrieval boundaries.
- Evidence: adapter conformance tests validating hash mismatch rejection and deterministic locator projection.

3. AK-03 Render-job substrate completion
- Implement queued render-job submission/claim/retry transitions with idempotency key and dedupe scope enforcement.
- Ensure terminal lifecycle coverage includes approval/persist/reject-aligned job outcomes without policy ownership leakage.
- Evidence: state-transition test matrix covering new, claimed, failed-retryable, failed-terminal, completed, superseded.

4. AK-04 Preview and thumbnail linkage
- Implement explicit source-to-preview asset linkage records and retrieval contracts.
- Ensure previews are first-class assets with lineage and retention participation.
- Evidence: contract + persistence tests for source asset lookup returning deterministic preview chain.

5. AK-05 TTL and retention sweep execution
- Implement idempotent sweep execution keyed by UTC watermark and retention policy hints from upstream orchestration.
- Support expiry, purge-candidate marking, and purge completion reporting with audit-safe history.
- Evidence: sweep simulation tests proving repeated runs are stable and do not mutate immutable bytes.

6. AK-06 Lineage traversal APIs
- Implement parent/child/supersession traversal for asset history and derived artifacts.
- Ensure lineage query shape is deterministic and does not require orchestration-side joins.
- Evidence: traversal tests for rerender/supersession trees and preview-derived chains.

## Gate to renderer move-in

Do not start renderer-specific migration completion (documents, portraits, video) until AK-01 through AK-06 show passing evidence in this repo.

## Completion signal

This slice is complete when:
- runnable AK-01..AK-06 tasks are tracked in queue/worklist artifacts,
- each task has mapped verification evidence,
- and queue status can move from queued to completed without adding narrative/delivery/session policy into media-factory contracts.

Execution evidence is tracked in `docs/EXTRACT-007-AK-execution-evidence.md`.
