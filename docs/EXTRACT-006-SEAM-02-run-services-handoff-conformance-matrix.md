# EXTRACT-006 SEAM-02 Run-Services Handoff Conformance Matrix

## Purpose

Define the run-services to media-factory handoff contract at the seam so ingress payload requirements are explicit and run-services forbidden ownership paths are testable during cutover.

This matrix is normative for EXTRACT-006/SEAM-02 and must remain aligned with `Chummer.Media.Contracts`.

## Conformance matrix

| ID | Handoff path | Required ingress payload in run-services submission | Forbidden ownership path in run-services | Conformance rule |
| --- | --- | --- | --- | --- |
| M-ING-01 | Common render intake (`run-services` -> `media-factory`) | `MediaRenderRequest` with `renderKind`, `requestId`, `requestedAtUtc`, deterministic `idempotencyKey`, and render-only `inputs` map | run-services constructing provider-native request bodies or SDK payloads | run-services submits only `Chummer.Media.Contracts` request DTOs; provider payload expansion happens inside media-factory executors |
| M-ING-02 | Document render ingress | `renderKind=document` plus deterministic template/model references in `inputs` | run-services selecting provider adapter, model endpoint, or worker class for document execution | run-services may choose intent and business priority only; media-factory owns provider selection/execution for document jobs |
| M-ING-03 | Portrait render ingress | `renderKind=portrait` plus deterministic subject/style references in `inputs` | run-services issuing direct portrait provider calls or storing provider run IDs as source-of-truth state | portrait execution and provider-run ledger ownership is local to media-factory |
| M-ING-04 | Video render ingress | `renderKind=video` plus bounded script/shot-plan references in `inputs` | run-services orchestrating provider-side video job polling/retries or queue claims | video queue claim/retry/terminal transitions are media-factory-owned job semantics |
| M-ING-05 | Retry replay ingress | resubmission uses same `idempotencyKey` and stable render intent payload | run-services redefining dedupe scope/key meaning outside `RenderJobContract` fields | dedupe scope/key semantics are owned by media-factory contracts and queue substrate |
| M-ING-06 | Lifecycle-state write handoff | run-services sends approval outcome intent only (`approved`, `rejected`, `persisted`) keyed by asset/job identity | run-services mutating manifest lifecycle rows directly or defining lifecycle terminality rules | lifecycle mutation authority and terminality invariants live in media-factory asset lifecycle state machine |
| M-ING-07 | Metadata egress correlation | run-services receives receipt/status payload with job status, asset handles, preview handles, lineage ids, signed URL metadata | run-services treating its local projection as canonical queue/lifecycle state semantics | media-factory state is canonical; run-services stores downstream orchestration projection only |
| M-ING-08 | Failure envelope handoff | run-services consumes structured media-factory failure envelope and reason codes | run-services translating failures into alternate queue semantics or provider retry policy | failure interpretation cannot alter factory queue semantics; retries follow media-factory contract/state |

## Required payload checklist (ingress)

Run-services ingress payloads are conformant only when all are true:

- request DTO type is from `Chummer.Media.Contracts` render/job families only
- idempotency key is included and stable for retries/replays
- render intent includes only renderable deterministic references and bounded input values
- no campaign/session truth blobs, rules/canon payloads, Spider routing policy, or delivery policy fields are embedded
- no provider SDK types, provider credentials, worker routing hints, or storage implementation paths are embedded

## Forbidden ownership paths in run-services

Any item below is a seam failure:

- provider execution ownership
- direct provider SDK invocation for document, portrait, preview/thumbnail, or video rendering
- provider secret management required to execute render jobs
- provider run polling loops as canonical execution state

- lifecycle mutation authority
- direct mutation of media-factory manifest lifecycle rows
- defining approval/persist/reject terminality rules outside media-factory lifecycle contracts
- defining retention expiry/supersession semantics outside media-factory lifecycle contracts

- queue semantics ownership
- defining dedupe scope/key meaning outside `RenderJobDedupeScope` and job contracts
- defining retry cadence, claim behavior, terminal job states, or supersession resolution in run-services
- treating run-services projections as canonical queue/lifecycle truth

## Verification hooks for SEAM-03 rehearsal

- Submit one request per render kind (`document`, `portrait`, `video`) and validate payload conformance against this matrix before dispatch.
- Replay one request using identical idempotency key and verify dedupe behavior is governed by media-factory state.
- Execute approval, reject, and persist transitions through the handoff path and verify run-services cannot bypass media-factory lifecycle authority.
- Validate failure and status egress are consumed as projections only; no run-services logic may redefine queue semantics.

## Traceability

- `docs/EXTRACT-006-run-services-seam-cutover-backlog.md`
- `docs/MF-005-service-seams-and-handoffs.md`
- `docs/EXTRACT-005-scaffold-exit-seam-ownership-check.md`
- `src/Chummer.Media.Contracts/Rendering/MediaRenderRequest.cs`
- `src/Chummer.Media.Contracts/Jobs/RenderJobContract.cs`
- `src/Chummer.Media.Contracts/Jobs/RenderJobDedupeScope.cs`
