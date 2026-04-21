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

## Milestone coverage model (ETA/completion truth)

### M0 contract canon

* program mapping: `C1 media factory extraction`
* contract set: `media_execution_vnext`
* coverage sources: `docs/EXTRACT-001A-canonical-package-plane-evidence.md`, `src/Chummer.Media.Contracts/Chummer.Media.Contracts.csproj`, `scripts/ai/contract-boundary-tests.sh`
* completion gates: `CP-01`..`CP-03` (canonical package identity, render-only boundary checks, verify/pack path)
* ETA/completion basis: complete; evidence and verification lane landed in the 2026-03-10 extraction wave
* status: complete

### M1 asset/job kernel

* program mapping: `C1 media factory extraction`
* contract set: `media_execution_vnext`
* coverage sources: `docs/EXTRACT-007-AK-execution-evidence.md`, `docs/EXTRACT-007-asset-kernel-implementation-backlog.md`
* completion gates: `AK-01`..`AK-06` (manifest store, binary storage, render-job substrate, preview linkage, retention sweeps, lineage traversal)
* ETA/completion basis: complete; AK execution evidence and contract surfaces are published and verified
* status: complete

### M2 document rendering

* program mapping: `C1c media-side external adapters`
* contract set: `media_execution_vnext`
* coverage sources: `docs/MEDIA_ADAPTER_MATRIX.md`, `docs/MEDIA_CAPABILITY_SIGNOFF.md`
* completion gates: document adapter ownership is media-factory-private, provider choice remains switchable/kill-switchable, capability signoff captured
* ETA/completion basis: complete for current scope; `C1c` is complete in canonical program milestones and adapter/signoff evidence is published
* status: complete

### M3 portrait forge

* program mapping: `E4 media plane complete`
* contract set: `media_execution_vnext`
* coverage sources: `docs/MEDIA_CAPABILITY_SIGNOFF.md`, `docs/MEDIA_ADAPTER_MATRIX.md`
* completion gates: portrait capability is stable, approval-aware, provenance-preserving, and operator-verifiable
* ETA/completion basis: complete for current scope; E4-aligned portrait capability signoff is recorded
* status: complete

### M4 bounded video

* program mapping: `E4 media plane complete`
* contract set: `media_execution_vnext`
* coverage sources: `docs/MEDIA_CAPABILITY_SIGNOFF.md`, `docs/MEDIA_ADAPTER_MATRIX.md`
* completion gates: bounded video capability is stable, approval-aware, provenance-preserving, and operator-verifiable
* ETA/completion basis: complete for current scope; E4-aligned bounded-video capability signoff is recorded
* status: complete

### M5 template/style integration

* program mapping: `E4 media plane complete`
* contract set: `media_execution_vnext`
* coverage sources: `docs/MEDIA_CAPABILITY_SIGNOFF.md`, `src/Chummer.Media.Contracts/Requests/MediaRenderRequest.cs`
* completion gates: template/style versioning is carried in render-only request contracts and covered by capability signoff posture
* ETA/completion basis: complete for current scope; render request contract and signoff surfaces provide the completion basis
* status: complete

### M6 run-services cutover

* program mapping: `C1 media factory extraction`
* contract set: `media_execution_vnext`
* coverage sources: `docs/EXTRACT-006-SEAM-execution-evidence.md`, `docs/EXTRACT-006-run-services-seam-cutover-backlog.md`, `docs/EXTRACT-006-SEAM-04-renderer-move-in-gate.md`
* completion gates: `SEAM-01`..`SEAM-04` all pass; effective render/asset lifecycle ownership is media-factory
* ETA/completion basis: complete; seam conformance matrix, rehearsal checklist, and move-in gate all pass
* status: complete

### M7 storage/DR/scale

* program mapping: `F1 observability, DR, replay safety`
* contract set: `media_execution_vnext`
* coverage sources: `docs/MEDIA_FACTORY_RESTORE_RUNBOOK.md`, `docs/MEDIA_CAPABILITY_SIGNOFF.md`
* completion gates: restore runbook coverage, replay-safety posture, and retention policy execution evidence are present
* ETA/completion basis: complete for current scope; restore/retention evidence is published and referenced in signoff
* status: complete

### M8 finished media plant

* program mapping: `E4 media plane complete`
* contract set: `media_execution_vnext`
* coverage sources: `docs/EXTRACT-008-DS-execution-evidence.md`, `docs/MEDIA_CAPABILITY_SIGNOFF.md`, `docs/MEDIA_ADAPTER_MATRIX.md`
* completion gates: `DS-01`..`DS-05` pass for render-only DTO boundaries; documents/portraits/bounded video lanes are stable and signed off
* ETA/completion basis: complete for current scope; DTO split execution evidence plus capability signoff close the aggregate media-plane milestone
* status: complete

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
