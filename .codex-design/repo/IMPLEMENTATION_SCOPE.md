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

## Milestone coverage model (ETA and completion truth)

Program family: `media_execution_vnext`  
Contract set: `C1` (media execution and seam ownership), `C1c` (capability family ownership), `E4` (finished media plant), `F1` (storage/DR/scale proof)

| Milestone | Program mapping | Contract set | Coverage sources | Completion gates | ETA/completion basis | Status |
|---|---|---|---|---|---|---|
| `M0` contract canon | `M0 -> C1 -> media_execution_vnext` | `C1` | `src/Chummer.Media.Contracts`, `docs/EXTRACT-001A-canonical-package-plane-runtime-backlog.md`, `docs/EXTRACT-001A-canonical-package-plane-evidence.md` | `CP-01`..`CP-03` pass and package/release verification holds | Basis is evidence-backed package plane closure and ongoing `scripts/ai/verify.sh` pass | complete |
| `M1` asset/job kernel | `M1 -> C1 -> media_execution_vnext` | `C1` | `src/Chummer.Media.Factory.Runtime`, `docs/EXTRACT-007-asset-kernel-implementation-backlog.md`, `docs/EXTRACT-007-AK-execution-evidence.md` | `AK-01`..`AK-06` pass for manifests/storage/jobs/previews/retention/lineage | Basis is AK execution evidence plus runtime verification stability | complete |
| `M2` document rendering | `M2 -> C1c -> media_execution_vnext` | `C1c` | `docs/MEDIA_ADAPTER_MATRIX.md`, `docs/MEDIA_CAPABILITY_SIGNOFF.md` | document lane present as owner-repo adapter family with operator signoff | Basis is capability signoff and adapter matrix ownership proof | complete |
| `M3` portrait forge | `M3 -> C1c -> media_execution_vnext` | `C1c` | `docs/MEDIA_ADAPTER_MATRIX.md`, `docs/MEDIA_CAPABILITY_SIGNOFF.md` | portrait lane present as owner-repo adapter family with approval-aware lifecycle and provenance | Basis is capability signoff and provenance/approval coverage | complete |
| `M4` bounded video | `M4 -> E4 -> media_execution_vnext` | `E4` | `docs/MEDIA_ADAPTER_MATRIX.md`, `docs/MEDIA_CAPABILITY_SIGNOFF.md` | bounded-video lane present with approval-aware lifecycle and operator-verifiable capability proof | Basis is `E4` closure evidence for bounded-video family | complete |
| `M5` template/style integration | `M5 -> C1c -> media_execution_vnext` | `C1c` | `docs/MEDIA_ADAPTER_MATRIX.md`, `docs/MEDIA_CAPABILITY_SIGNOFF.md` | preview/template style surfaces remain media-factory-owned and switchable under adapter-private control | Basis is adapter matrix/switchability contract in current capability docs | complete |
| `M6` run-services cutover | `M6 -> C1 -> media_execution_vnext` | `C1` | `docs/EXTRACT-006-run-services-seam-cutover-backlog.md`, `docs/EXTRACT-006-SEAM-execution-evidence.md` | `SEAM-01`..`SEAM-04` pass and render path ownership remains in media-factory | Basis is seam execution evidence and handoff conformance | complete |
| `M7` storage/DR/scale | `M7 -> F1 -> media_execution_vnext` | `F1` | `docs/MEDIA_FACTORY_RESTORE_RUNBOOK.md`, `Chummer.Media.Factory.Runtime.Verify` | restore, replay safety, retention sweep, and operator runbook proof remain green | Basis is restore runbook plus runtime verify coverage | complete |
| `M8` finished media plant | `M8 -> E4 -> media_execution_vnext` | `E4` | `docs/EXTRACT-008-dto-split-boundary-backlog.md`, `docs/EXTRACT-008-DS-execution-evidence.md`, `docs/MEDIA_CAPABILITY_SIGNOFF.md` | `DS-01`..`DS-05` pass and DTO surfaces stay render-only across package and compatibility planes | Basis is DTO boundary evidence + capability signoff + verification | complete |

### Status rule

`complete` is only valid when milestone mapping, coverage source(s), gate set, and ETA/completion basis are all explicit. Any missing element must drop status to `partial`.
