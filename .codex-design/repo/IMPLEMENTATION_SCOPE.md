# Media factory implementation scope

## Mission

`chummer6-media-factory` owns render execution, render jobs, previews, manifests, asset lifecycle, provider adapters, and signed asset access for Chummer media workloads.

## Owns

* `Chummer.Media.Contracts`
* the canonical `governed_spatial_render_v1` contract family and authoritative deterministic compose receipts
* render job intake and state
* provider selection behind adapters plus restartable attempt, retry, cancellation, and compensation state
* consumer-authorization enforcement, idempotency ledger, atomic quota reservation/consumption/release/compensation, and provider-route kill switch
* previews and thumbnails
* immutable output manifests, encrypted private execution receipts, provider-redacted projections, and asset/deletion receipts
* asset lifecycle, retention, pinning, supersession, deletion cascades, tombstones, and provider-deletion evidence
* provider adapters for document/image/video execution
* signed asset access and media storage discipline

## Package boundary

`chummer6-media-factory` owns `Chummer.Media.Contracts` and may consume:

* `Chummer.Campaign.Contracts` for campaign-linked render context
* `Chummer.World.Contracts` for approved world-state, newsreel, mission-market, and opposition-packet projections

It must not redefine campaign, world, approval, or rules semantics inside media execution DTOs.
For governed spatial rendering, Hub supplies approved immutable runsite/campaign/run/scene/actor/provided-outcome/permission/audience refs and a time-limited consumer authorization through the canonical package.
Media-factory never imports Hub implementation source or accepts a source-copied or `ea.*` alias of the contract.

## Must not own

* campaign or session truth
* rules math
* initiative, action, damage, effect, encounter-outcome, or other mechanics calculation or mutation
* approvals policy
* consumer identity, purpose, permission, private audience, product projection, or user-visible closeout truth
* publication/moderation workflows
* Registry publication, renewal, revocation, or public-ref truth
* play/client UX
* general AI orchestration
* service identity or relay

## Governed spatial-render execution rule

Amendment state is `proposed_for_independent_re_review`; implementation, provider execution, quota use, mirror publication, and release widening remain blocked.

Media-factory is the sole Chummer owner for:

* authoritative compose validation, normalization, composition digest, and zero-burn compose receipt
* authorized build intake only after a fresh Hub authorization, accepted composition digest, exact-family capability evidence, current quota/kill-switch evidence, `consume_quota: true`, and bounded attempts
* one-job/one-reservation idempotency, attempt lineage, provider execution, retry/cancellation, quota consumption and compensation
* request/source/style/output hashes, provider-private task/account refs and traces, immutable output manifests, provider-redacted projections, lifecycle, deletion, and provider-deletion receipts

Compose must enqueue no provider work, create no quota reservation or consumption, and project no readiness.
EA may assist synthetic zero-burn composition but cannot issue the authoritative receipt or retain provider-private evidence.
Any missing, stale, revoked, wrong-family, wrong-environment, wrong-route-digest, or wrong-gate-version evidence fails closed to `unverified` or `blocked`.

Allowed dependency direction is Hub orchestration -> `Chummer.Media.Contracts` -> media-factory execution -> provider-redacted manifest/status -> Hub product meaning and Registry publication/revocation.
Forbidden direction is media-factory -> mechanics/campaign/audience/approval/product/publication mutation, Hub -> provider execution/quota/private receipts, or EA -> durable contract/execution/readiness authority.

The numeric lifecycle rules live in `GOVERNED_SPATIAL_RENDER_PRIVACY_RETENTION_POLICY.md`; the private capability and quota receipt shape and freshness windows live in `GOVERNED_SPATIAL_RENDER_CAPABILITY_QUOTA_EVIDENCE.schema.yaml`.

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

That existing completion statement does not authorize the pending governed spatial-render family or widen provider, artifact, RUNSITE, or release readiness.

That means:

* document, preview, route, portrait, bounded-video, and archive lanes are explicit owner families
* preview backend choice remains switchable and kill-switchable inside media-factory-owned surfaces
* lifecycle, restore, provenance, and operator signoff are explicit in `MEDIA_ADAPTER_MATRIX.md`, `MEDIA_CAPABILITY_SIGNOFF.md`, and `MEDIA_FACTORY_RESTORE_RUNBOOK.md`
