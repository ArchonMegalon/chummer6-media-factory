# Chummer Media Factory Design v1

## Purpose

`chummer-media-factory` owns media rendering jobs, asset lifecycle, storage integration, and approval-state persistence for approved render payloads.

## In scope

- render-only contracts
- job queue and dedupe
- asset persistence and retention
- document, portrait, and video render pipelines
- signed asset access

## Out of scope

- narrative drafting
- rules evaluation
- session relay
- Spider policy
- provider routing

## Exit criteria for the bootstrap phase

- repo builds in isolation
- verification script runs
- render-only contract boundaries are documented
- no implementation dependency leaks from engine, presentation, play, or run-services

## Canonical contract plane

`Chummer.Media.Contracts` is the canonical package plane for DTOs in this repo.
It is intentionally limited to:

- render requests with deterministic template/version/input payloads
- render job queue state, dedupe ownership, and retry state
- asset manifests, catalog entries, previews, TTL/retention, approval state, persistence state, and lineage

It must not contain:

- narrative prompts, story briefs, or canon-authoring payloads
- approvals policy or delivery policy decisions
- session, campaign, or Spider routing context

## Asset kernel ownership notes

The bootstrap contract plane seeds the shared asset kernel with these boundaries:

- `MediaRenderRequest` carries only deterministic render input after orchestration is complete
- `RenderJobContract` owns queue placement, dedupe scope/key, retry timing, and terminal status
- `MediaAssetManifest` owns binary storage identity, preview linkage, TTL, persistence, and lineage
- `MediaAssetCatalogEntry` owns lookup metadata for assets without introducing delivery policy
