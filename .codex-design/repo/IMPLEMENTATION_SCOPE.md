# Media factory implementation scope

## Mission

`chummer6-media-factory` owns render execution, render jobs, previews, manifests, asset lifecycle, provider adapters, and signed asset access for Chummer media workloads.

## Owns

* `Chummer.Media.Contracts`
* render job intake and state
* previews and thumbnails
* manifests and asset receipts
* asset lifecycle, retention, pinning, supersession
* provider adapters for document/image/video execution
* signed asset access and media storage discipline

## Must not own

* campaign or session truth
* rules math
* approvals policy
* publication/moderation workflows
* play/client UX
* general AI orchestration
* service identity or relay

## Current focus

* keep media capability signoff explicit
* preserve provider-private adapter control
* widen provider depth only as additive follow-through
* keep mirror coverage current from `chummer6-design`

## Milestone spine

* M0 contract canon
* M1 asset/job kernel
* M2 document rendering
* M3 portrait forge
* M4 bounded video
* M5 template/style integration
* M6 run-services cutover
* M7 storage/DR/scale
* M8 finished media plant

## Milestone coverage model (explicit ETA/completion truth)

All rows map to contract set `media_execution_vnext` from `.codex-design/product/CONTRACT_SETS.yaml`.
Completion is evidence-gated by repo-local artifacts and `scripts/ai/verify.sh`; no row closes on prose-only claims.

| Milestone | Program mapping | Coverage sources | Completion gate | ETA/completion basis | Status |
| --- | --- | --- | --- | --- | --- |
| M0 contract canon | C1 media factory extraction | `docs/EXTRACT-001A-canonical-package-plane-evidence.md`, `docs/EXTRACT-001A-canonical-package-plane-runtime-backlog.md` | `CP-01`..`CP-03` pass | Package plane exists, canonical package identity is stable, boundary checks and pack verification pass | complete |
| M1 asset/job kernel | C1 media factory extraction | `docs/EXTRACT-007-AK-execution-evidence.md`, `docs/EXTRACT-007-asset-kernel-implementation-backlog.md` | `AK-01`..`AK-06` pass | Kernel contracts for manifests, binary storage, jobs, previews, retention, and lineage are implemented and verified | complete |
| M2 document rendering | C1c media-side external adapters, E4 media plane complete | `docs/MEDIA_CAPABILITY_SIGNOFF.md`, `docs/MEDIA_ADAPTER_MATRIX.md` | Document capability family remains owner-repo, provider-private, and lifecycle-governed | Completion follows capability signoff plus adapter-matrix ownership evidence | complete |
| M3 portrait forge | C1c media-side external adapters, E4 media plane complete | `docs/MEDIA_CAPABILITY_SIGNOFF.md`, `docs/MEDIA_ADAPTER_MATRIX.md` | Portrait capability family remains owner-repo and lifecycle-governed | Completion follows capability signoff and adapter-family ownership evidence | complete |
| M4 bounded video | C1c media-side external adapters, E4 media plane complete | `docs/MEDIA_CAPABILITY_SIGNOFF.md`, `docs/MEDIA_ADAPTER_MATRIX.md` | Bounded-video and route-video capability families remain owner-repo and lifecycle-governed | Completion follows capability signoff and adapter-family ownership evidence | complete |
| M5 template/style integration | C1c media-side external adapters | `docs/MEDIA_CAPABILITY_SIGNOFF.md`, `docs/MEDIA_ADAPTER_MATRIX.md` | Packet/document/preview artifact families are stable and provider choice remains switchable/kill-switchable in media-factory surfaces | Completion follows stable capability-family evidence and backend-control guardrails | complete |
| M6 run-services cutover | C1 media factory extraction | `docs/EXTRACT-006-SEAM-execution-evidence.md`, `docs/EXTRACT-006-run-services-seam-cutover-backlog.md` | `SEAM-01`..`SEAM-04` pass (`G1`..`G4` pass) | Seam ownership is explicit: media-factory owns render/asset semantics, run-services is orchestration ingress/egress only | complete |
| M7 storage/DR/scale | F1 observability, DR, replay safety | `docs/MEDIA_FACTORY_RESTORE_RUNBOOK.md`, `Chummer.Media.Factory.Runtime.Verify/` | Restore/retention/replay-safe checks pass in runbook + runtime verify path | Completion follows restore-drill contract and replay-safe verification evidence | complete |
| M8 finished media plant | E4 media plane complete | `docs/EXTRACT-008-DS-execution-evidence.md`, `docs/MEDIA_CAPABILITY_SIGNOFF.md`, `docs/EXTRACT-006-SEAM-execution-evidence.md` | `DS-01`..`DS-05` pass and no seam/ownership regressions | Aggregate completion closes when DTO boundaries, capability families, and seam ownership all remain green | complete |

## Worker rule

If the feature is about rendering, previews, manifests, or asset lifecycle, it belongs here.
If it is about campaign meaning, approvals, delivery, or rules truth, it does not.


## External media integrations scope

`chummer6-media-factory` is the only repo allowed to own media/render/archive adapters.

### Owns

* `IDocumentRenderAdapter`
* `IPreviewRenderAdapter`
* `IImageRenderAdapter`
* `IVideoRenderAdapter`
* `IRouteRenderAdapter`
* `IArchiveAdapter`
* media provider receipts
* media provider provenance
* media safety/moderation result capture
* media archive execution
* media retention/archive policy execution

### Initial vendor mapping

* MarkupGo - document-render adapter
* PeekShot - preview/thumbnail/share-card adapter
* Mootion - bounded video adapter
* AvoMap - route-render adapter
* Internxt - cold-archive adapter
* optional 1min.AI / AI Magicx image assistance only when wrapped behind media-factory adapters and governed by provenance rules

### Must not own

* campaign/session meaning
* approval policy
* canon policy
* registry publication
* client UX
* general AI orchestration

### Required design rules

* every media job produces a Chummer manifest
* provider outputs are never the canonical asset record alone
* previews and thumbnails are linked assets
* archive providers are never the hot path
* provider choice is adapter-private and switchable

## Current reality

`C1c` and `E4` are now treated as complete for the current release scope.

That means:

* document, preview, route, portrait, bounded-video, and archive lanes are explicit owner families
* preview backend choice remains switchable and kill-switchable inside media-factory-owned surfaces
* lifecycle, restore, provenance, and operator signoff are explicit in `MEDIA_ADAPTER_MATRIX.md`, `MEDIA_CAPABILITY_SIGNOFF.md`, and `MEDIA_FACTORY_RESTORE_RUNBOOK.md`
