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

## Worker rule

If the feature is about rendering, previews, manifests, or asset lifecycle, it belongs here.
If it is about campaign meaning, approvals, delivery, or rules truth, it does not.

## Milestone coverage model (`M0`..`M8`)

Snapshot date: 2026-03-31

| Milestone | Program mapping | Contract set | Coverage sources | Completion gates | ETA/completion basis | Status |
| --- | --- | --- | --- | --- | --- | --- |
| `M0 contract canon` | `C1 media factory extraction` | `media_execution_vnext` | `docs/EXTRACT-001A-canonical-package-plane-evidence.md` | `CP-01`..`CP-03` pass | canonical `Chummer.Media.Contracts` package identity, packability, and render-only contract boundary checks in verify path | completed (100%, ETA 2026-03-10 met) |
| `M1 asset/job kernel` | `C1 media factory extraction` | `media_execution_vnext` | `docs/EXTRACT-007-AK-execution-evidence.md`, `docs/EXTRACT-007-asset-kernel-implementation-backlog.md` | `AK-01`..`AK-06` pass | manifests, binary storage, job substrate transitions, preview linkage, retention sweeps, and lineage contracts are implemented and verified | completed (100%, ETA 2026-03-10 met) |
| `M2 document rendering` | `E4 media plane complete` | `media_execution_vnext` | `docs/MEDIA_CAPABILITY_SIGNOFF.md`, `docs/MEDIA_ADAPTER_MATRIX.md` | document lane signoff and adapter-family ownership coverage present | document/packet capability family is explicit behind media-factory-owned contracts/runtime with provider-private control | completed (100%, ETA 2026-03-19 met) |
| `M3 portrait forge` | `E4 media plane complete` | `media_execution_vnext` | `docs/MEDIA_CAPABILITY_SIGNOFF.md`, `docs/MEDIA_ADAPTER_MATRIX.md` | portrait lane signoff and adapter-family ownership coverage present | portrait capability family is explicit behind media-factory-owned contracts/runtime with provider-private control | completed (100%, ETA 2026-03-19 met) |
| `M4 bounded video` | `E4 media plane complete` | `media_execution_vnext` | `docs/MEDIA_CAPABILITY_SIGNOFF.md`, `docs/MEDIA_ADAPTER_MATRIX.md` | bounded video lane signoff and adapter-family ownership coverage present | bounded video capability family is explicit behind media-factory-owned contracts/runtime with provider-private control | completed (100%, ETA 2026-03-19 met) |
| `M5 template/style integration` | `E4 media plane complete` | `media_execution_vnext` | `docs/MEDIA_CAPABILITY_SIGNOFF.md` | governed packet-planning seams compile in media-factory-owned capability families | template/style-dependent packet artifacts are integrated as render outputs without importing campaign/session truth | completed (100%, ETA 2026-03-19 met) |
| `M6 run-services cutover` | `C1 media factory extraction` | `media_execution_vnext` | `docs/EXTRACT-006-SEAM-execution-evidence.md`, `docs/EXTRACT-006-run-services-seam-cutover-backlog.md` | `SEAM-01`..`SEAM-04` pass | seam acceptance, handoff conformance, cutover rehearsal, and non-regression gate all pass with media-factory as effective seam owner | completed (100%, ETA 2026-03-10 met) |
| `M7 storage/DR/scale` | `F1 observability, DR, replay safety` | `media_execution_vnext` | `docs/MEDIA_FACTORY_RESTORE_RUNBOOK.md` | restore drill acceptance criteria pass in `Chummer.Media.Factory.Runtime.Verify` | backup contract continuity, replay-safe counters, retention sweep continuity, and pinned-asset restore proof are all executable | completed (100%, ETA 2026-03-19 met) |
| `M8 finished media plant` | `E4 media plane complete` | `media_execution_vnext` | `docs/MEDIA_CAPABILITY_SIGNOFF.md`, `docs/EXTRACT-008-DS-execution-evidence.md`, `docs/MEDIA_FACTORY_RESTORE_RUNBOOK.md` | capability signoff + DTO render-only boundary checks + restore/lifecycle coverage all pass | media lanes (documents/portraits/bounded video/route/archive) are stable and render-only with approval/persist/reject lifecycle semantics and operator verification | completed (100%, ETA 2026-03-19 met) |


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
