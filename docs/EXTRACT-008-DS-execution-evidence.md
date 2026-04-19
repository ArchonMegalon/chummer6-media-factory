# EXTRACT-008 DS-01..DS-05 Execution Evidence

Date: 2026-03-10

This evidence file records execution of the DTO boundary split backlog and confirms `Chummer.Media.Contracts` remains render-only.

## DS-01 Contract inventory and ownership classification

Legend:
- `render_input`: deterministic render execution input
- `job_lifecycle`: render job queue/execution lifecycle
- `asset_lifecycle`: asset manifest/storage/retention/lineage lifecycle
- `forbidden_upstream`: narrative authoring, campaign/session truth, delivery/approval policy, canon/rules, relay/routing concerns

| Type | Field | Classification |
| --- | --- | --- |
| `MediaRenderRequest` | `RenderRequestId` | `render_input` |
| `MediaRenderRequest` | `RenderKind` | `render_input` |
| `MediaRenderRequest` | `TemplateId` | `render_input` |
| `MediaRenderRequest` | `TemplateVersion` | `render_input` |
| `MediaRenderRequest` | `OutputFormat` | `render_input` |
| `MediaRenderRequest` | `ContentHash` | `render_input` |
| `MediaRenderRequest` | `RequestedBy` | `render_input` |
| `MediaRenderRequest` | `Inputs` | `render_input` |
| `MediaRenderRequest` | `RequestedAtUtc` | `render_input` |
| `RenderJobContract` | `RenderJobId` | `job_lifecycle` |
| `RenderJobContract` | `QueueName` | `job_lifecycle` |
| `RenderJobContract` | `DedupeKey` | `job_lifecycle` |
| `RenderJobContract` | `DedupeScope` | `job_lifecycle` |
| `RenderJobContract` | `Request` | `render_input` |
| `RenderJobContract` | `Status` | `job_lifecycle` |
| `RenderJobContract` | `AttemptCount` | `job_lifecycle` |
| `RenderJobContract` | `MaxAttemptCount` | `job_lifecycle` |
| `RenderJobContract` | `CreatedAtUtc` | `job_lifecycle` |
| `RenderJobContract` | `AvailableAtUtc` | `job_lifecycle` |
| `RenderJobContract` | `ClaimedAtUtc` | `job_lifecycle` |
| `RenderJobContract` | `LastAttemptedAtUtc` | `job_lifecycle` |
| `RenderJobContract` | `RetryAfterUtc` | `job_lifecycle` |
| `RenderJobContract` | `CompletedAtUtc` | `job_lifecycle` |
| `RenderJobContract` | `FailureCode` | `job_lifecycle` |
| `RenderJobContract` | `SupersededByRenderJobId` | `job_lifecycle` |
| `MediaAssetManifest` | `AssetId` | `asset_lifecycle` |
| `MediaAssetManifest` | `CatalogKey` | `asset_lifecycle` |
| `MediaAssetManifest` | `RenderJobId` | `asset_lifecycle` |
| `MediaAssetManifest` | `RenderKind` | `asset_lifecycle` |
| `MediaAssetManifest` | `StorageBucket` | `asset_lifecycle` |
| `MediaAssetManifest` | `StorageObjectKey` | `asset_lifecycle` |
| `MediaAssetManifest` | `ContentType` | `asset_lifecycle` |
| `MediaAssetManifest` | `ContentLengthBytes` | `asset_lifecycle` |
| `MediaAssetManifest` | `ContentHash` | `asset_lifecycle` |
| `MediaAssetManifest` | `PreviewAssetId` | `asset_lifecycle` |
| `MediaAssetManifest` | `ParentAssetId` | `asset_lifecycle` |
| `MediaAssetManifest` | `Lifecycle` | `asset_lifecycle` |
| `MediaAssetManifest` | `DerivedAssetIds` | `asset_lifecycle` |
| `MediaAssetCatalogEntry` | `AssetId` | `asset_lifecycle` |
| `MediaAssetCatalogEntry` | `CatalogKey` | `asset_lifecycle` |
| `MediaAssetCatalogEntry` | `OwnerId` | `asset_lifecycle` |
| `MediaAssetCatalogEntry` | `RenderJobId` | `asset_lifecycle` |
| `MediaAssetCatalogEntry` | `RenderRequestId` | `asset_lifecycle` |
| `MediaAssetCatalogEntry` | `DisplayName` | `asset_lifecycle` |
| `MediaAssetCatalogEntry` | `PreviewAssetId` | `asset_lifecycle` |
| `MediaAssetCatalogEntry` | `CreatedAtUtc` | `asset_lifecycle` |
| `MediaAssetCatalogEntry` | `ExpiresAtUtc` | `asset_lifecycle` |
| `MediaAssetLifecycleState` | `ApprovalStatus` | `asset_lifecycle` |
| `MediaAssetLifecycleState` | `CreatedAtUtc` | `asset_lifecycle` |
| `MediaAssetLifecycleState` | `ApprovedAtUtc` | `asset_lifecycle` |
| `MediaAssetLifecycleState` | `RejectedAtUtc` | `asset_lifecycle` |
| `MediaAssetLifecycleState` | `PersistedAtUtc` | `asset_lifecycle` |
| `MediaAssetLifecycleState` | `ExpiresAtUtc` | `asset_lifecycle` |
| `MediaAssetLifecycleState` | `PurgedAtUtc` | `asset_lifecycle` |
| `CreateManifestRequest` | `IdempotencyKey` | `asset_lifecycle` |
| `CreateManifestRequest` | `Manifest` | `asset_lifecycle` |
| `CreateManifestResult` | `Created` | `asset_lifecycle` |
| `CreateManifestResult` | `Manifest` | `asset_lifecycle` |
| `CreateManifestResult` | `Version` | `asset_lifecycle` |
| `GetManifestRequest` | `AssetId` | `asset_lifecycle` |
| `GetManifestResult` | `Found` | `asset_lifecycle` |
| `GetManifestResult` | `Manifest` | `asset_lifecycle` |
| `GetManifestResult` | `Version` | `asset_lifecycle` |
| `UpdateManifestLifecycleRequest` | `AssetId` | `asset_lifecycle` |
| `UpdateManifestLifecycleRequest` | `ExpectedVersion` | `asset_lifecycle` |
| `UpdateManifestLifecycleRequest` | `Lifecycle` | `asset_lifecycle` |
| `UpdateManifestLifecycleRequest` | `MutationReason` | `asset_lifecycle` |
| `UpdateManifestLifecycleResult` | `Updated` | `asset_lifecycle` |
| `UpdateManifestLifecycleResult` | `Manifest` | `asset_lifecycle` |
| `UpdateManifestLifecycleResult` | `Version` | `asset_lifecycle` |
| `UpdateManifestLifecycleResult` | `RejectionReason` | `asset_lifecycle` |
| `SubmitRenderJobRequest` | `IdempotencyKey` | `job_lifecycle` |
| `SubmitRenderJobRequest` | `QueueName` | `job_lifecycle` |
| `SubmitRenderJobRequest` | `DedupeKey` | `job_lifecycle` |
| `SubmitRenderJobRequest` | `DedupeScope` | `job_lifecycle` |
| `SubmitRenderJobRequest` | `Request` | `render_input` |
| `SubmitRenderJobRequest` | `AvailableAtUtc` | `job_lifecycle` |
| `SubmitRenderJobResult` | `Accepted` | `job_lifecycle` |
| `SubmitRenderJobResult` | `Job` | `job_lifecycle` |
| `SubmitRenderJobResult` | `ReplayedFromIdempotencyKey` | `job_lifecycle` |
| `ClaimRenderJobRequest` | `QueueName` | `job_lifecycle` |
| `ClaimRenderJobRequest` | `WorkerId` | `job_lifecycle` |
| `ClaimRenderJobRequest` | `ClaimedAtUtc` | `job_lifecycle` |
| `ClaimRenderJobResult` | `Claimed` | `job_lifecycle` |
| `ClaimRenderJobResult` | `Job` | `job_lifecycle` |
| `CompleteRenderJobRequest` | `RenderJobId` | `job_lifecycle` |
| `CompleteRenderJobRequest` | `FinalStatus` | `job_lifecycle` |
| `CompleteRenderJobRequest` | `CompletedAtUtc` | `job_lifecycle` |
| `CompleteRenderJobRequest` | `FailureCode` | `job_lifecycle` |
| `CompleteRenderJobResult` | `Updated` | `job_lifecycle` |
| `CompleteRenderJobResult` | `Job` | `job_lifecycle` |
| `CompleteRenderJobResult` | `RejectionReason` | `job_lifecycle` |
| `RetryRenderJobRequest` | `RenderJobId` | `job_lifecycle` |
| `RetryRenderJobRequest` | `RetryAfterUtc` | `job_lifecycle` |
| `RetryRenderJobRequest` | `Reason` | `job_lifecycle` |
| `RetryRenderJobResult` | `Retried` | `job_lifecycle` |
| `RetryRenderJobResult` | `Job` | `job_lifecycle` |
| `RetryRenderJobResult` | `RejectionReason` | `job_lifecycle` |
| `SupersedeRenderJobRequest` | `RenderJobId` | `job_lifecycle` |
| `SupersedeRenderJobRequest` | `SupersededByRenderJobId` | `job_lifecycle` |
| `SupersedeRenderJobRequest` | `Reason` | `job_lifecycle` |
| `SupersedeRenderJobResult` | `Superseded` | `job_lifecycle` |
| `SupersedeRenderJobResult` | `Job` | `job_lifecycle` |
| `SupersedeRenderJobResult` | `RejectionReason` | `job_lifecycle` |
| `PreviewLink` | `SourceAssetId` | `asset_lifecycle` |
| `PreviewLink` | `PreviewAssetId` | `asset_lifecycle` |
| `PreviewLink` | `RelationType` | `asset_lifecycle` |
| `PreviewLink` | `LinkedAtUtc` | `asset_lifecycle` |
| `UpsertPreviewLinkRequest` | `SourceAssetId` | `asset_lifecycle` |
| `UpsertPreviewLinkRequest` | `PreviewAssetId` | `asset_lifecycle` |
| `UpsertPreviewLinkRequest` | `RelationType` | `asset_lifecycle` |
| `UpsertPreviewLinkRequest` | `IdempotencyKey` | `asset_lifecycle` |
| `UpsertPreviewLinkRequest` | `LinkedAtUtc` | `asset_lifecycle` |
| `UpsertPreviewLinkResult` | `Upserted` | `asset_lifecycle` |
| `UpsertPreviewLinkResult` | `Link` | `asset_lifecycle` |
| `UpsertPreviewLinkResult` | `ReplayedFromIdempotencyKey` | `asset_lifecycle` |
| `GetPreviewChainRequest` | `SourceAssetId` | `asset_lifecycle` |
| `GetPreviewChainResult` | `SourceAssetId` | `asset_lifecycle` |
| `GetPreviewChainResult` | `PreviewChain` | `asset_lifecycle` |
| `RetentionSweepRequest` | `WatermarkUtc` | `asset_lifecycle` |
| `RetentionSweepRequest` | `SweepKey` | `asset_lifecycle` |
| `RetentionSweepRequest` | `IncludePurge` | `asset_lifecycle` |
| `RetentionSweepRequest` | `PolicyHints` | `asset_lifecycle` |
| `RetentionSweepAssetTransition` | `AssetId` | `asset_lifecycle` |
| `RetentionSweepAssetTransition` | `ExpiredAtUtc` | `asset_lifecycle` |
| `RetentionSweepAssetTransition` | `MarkedPurgeCandidateAtUtc` | `asset_lifecycle` |
| `RetentionSweepAssetTransition` | `PurgedAtUtc` | `asset_lifecycle` |
| `RetentionSweepAssetTransition` | `LifecycleOnlyMutation` | `asset_lifecycle` |
| `RetentionSweepResult` | `SweepKey` | `asset_lifecycle` |
| `RetentionSweepResult` | `WatermarkUtc` | `asset_lifecycle` |
| `RetentionSweepResult` | `Replayed` | `asset_lifecycle` |
| `RetentionSweepResult` | `ExaminedAssetCount` | `asset_lifecycle` |
| `RetentionSweepResult` | `ExpiredCount` | `asset_lifecycle` |
| `RetentionSweepResult` | `PurgeCandidateCount` | `asset_lifecycle` |
| `RetentionSweepResult` | `PurgedCount` | `asset_lifecycle` |
| `RetentionSweepResult` | `Transitions` | `asset_lifecycle` |
| `BinaryLocator` | `Store` | `asset_lifecycle` |
| `BinaryLocator` | `Container` | `asset_lifecycle` |
| `BinaryLocator` | `ObjectKey` | `asset_lifecycle` |
| `BinaryLocator` | `LocatorHash` | `asset_lifecycle` |
| `BinaryWriteRequest` | `AssetId` | `asset_lifecycle` |
| `BinaryWriteRequest` | `Locator` | `asset_lifecycle` |
| `BinaryWriteRequest` | `ContentLengthBytes` | `asset_lifecycle` |
| `BinaryWriteRequest` | `ContentHash` | `asset_lifecycle` |
| `BinaryWriteRequest` | `ContentType` | `asset_lifecycle` |
| `BinaryWriteResult` | `Accepted` | `asset_lifecycle` |
| `BinaryWriteResult` | `Locator` | `asset_lifecycle` |
| `BinaryWriteResult` | `HashVerified` | `asset_lifecycle` |
| `BinaryWriteResult` | `RejectionReason` | `asset_lifecycle` |
| `BinaryReadRequest` | `AssetId` | `asset_lifecycle` |
| `BinaryReadRequest` | `Locator` | `asset_lifecycle` |
| `BinaryReadRequest` | `ExpectedLengthBytes` | `asset_lifecycle` |
| `BinaryReadRequest` | `ExpectedHash` | `asset_lifecycle` |
| `BinaryReadResult` | `Found` | `asset_lifecycle` |
| `BinaryReadResult` | `Locator` | `asset_lifecycle` |
| `BinaryReadResult` | `ContentLengthBytes` | `asset_lifecycle` |
| `BinaryReadResult` | `ContentHash` | `asset_lifecycle` |
| `BinaryReadResult` | `HashMatches` | `asset_lifecycle` |
| `BinaryReadResult` | `RejectionReason` | `asset_lifecycle` |
| `BinaryDeleteRequest` | `AssetId` | `asset_lifecycle` |
| `BinaryDeleteRequest` | `Locator` | `asset_lifecycle` |
| `BinaryDeleteRequest` | `Reason` | `asset_lifecycle` |
| `BinaryDeleteResult` | `Deleted` | `asset_lifecycle` |
| `BinaryDeleteResult` | `Locator` | `asset_lifecycle` |
| `BinaryDeleteResult` | `RejectionReason` | `asset_lifecycle` |
| `AssetLineageNode` | `AssetId` | `asset_lifecycle` |
| `AssetLineageNode` | `RenderJobId` | `asset_lifecycle` |
| `AssetLineageNode` | `ParentAssetId` | `asset_lifecycle` |
| `AssetLineageNode` | `PreviewAssetId` | `asset_lifecycle` |
| `AssetLineageNode` | `DerivedAssetIds` | `asset_lifecycle` |
| `AssetLineageNode` | `CreatedAtUtc` | `asset_lifecycle` |
| `AssetLineageEdge` | `FromAssetId` | `asset_lifecycle` |
| `AssetLineageEdge` | `ToAssetId` | `asset_lifecycle` |
| `AssetLineageEdge` | `RelationType` | `asset_lifecycle` |
| `AssetLineageQuery` | `RootAssetId` | `asset_lifecycle` |
| `AssetLineageQuery` | `IncludeParents` | `asset_lifecycle` |
| `AssetLineageQuery` | `IncludeDerived` | `asset_lifecycle` |
| `AssetLineageQuery` | `IncludeSupersessions` | `asset_lifecycle` |
| `AssetLineageQuery` | `IncludePreviews` | `asset_lifecycle` |
| `AssetLineageQuery` | `MaxDepth` | `asset_lifecycle` |
| `AssetLineageResult` | `RootAssetId` | `asset_lifecycle` |
| `AssetLineageResult` | `Nodes` | `asset_lifecycle` |
| `AssetLineageResult` | `Edges` | `asset_lifecycle` |

All inventory rows are classified; no field is categorized as `forbidden_upstream`.

## DS-02 Forbidden concern guardrails

Guardrails were added as executable checks in:
- `scripts/ai/contract-boundary-tests.sh`
- wired into `scripts/ai/verify.sh`

Checks include:
- namespace policy drift
- forbidden upstream concern identifiers in public type and contract field declarations
- forbidden dependency edges (`Chummer.Engine`, `Chummer.Play`, `Chummer.Ui.Kit`, `Chummer.Run.Services`, provider SDK packages)

## DS-03 Split/removal pass for mixed DTO residue

No mixed DTO residue was found in `src/Chummer.Media.Contracts`. No split/removal edits were required.

Revalidation note (2026-04-15):
- That closure statement was too broad. It covered the canonical render/job/asset families, but it did not inventory the legacy compatibility shim in `src/Chummer.Media.Contracts/Compatibility/RunServices/MediaFactoryContracts.cs`.
- The compatibility shim still exposes upstream semantics that are out of scope for media-factory-owned render-only contracts, so follow-on backlog units are required.

## DS-04 Boundary conformance tests

Contract conformance tests are enforced by `scripts/ai/contract-boundary-tests.sh` and executed from `scripts/ai/verify.sh`:
- namespace and forbidden dependency checks
- forbidden upstream concern identifier checks
- lifecycle semantic checks for approval/persist/reject coverage:
  - `AssetApprovalStatus`: `Pending`, `Approved`, `Rejected`
  - `MediaAssetLifecycleState`: `ApprovalStatus`, `ApprovedAtUtc`, `RejectedAtUtc`, `PersistedAtUtc`

## DS-05 Queue/worklist and seam synchronization

Artifacts updated:
- `WORKLIST.md`
- `.codex-studio/published/QUEUE.generated.yaml`
- `docs/MF-005-service-seams-and-handoffs.md`
- `docs/EXTRACT-006-run-services-seam-cutover-backlog.md`

`EXTRACT-008` generic queue prompt was initially mapped to `DS-01`..`DS-05`, but revalidation on `2026-04-15` found remaining compatibility-shim residue. The active generic prompt now maps to `DS-01`..`DS-09`, with `DS-06`..`DS-09` covering the reopened compatibility follow-on lane.

## DS-06 Compatibility-shim residue inventory

The following public compatibility DTOs still mix render/job/asset lifecycle concerns with upstream authoring, delivery, or campaign-context semantics:

| Type | Field | Classification | Notes |
| --- | --- | --- | --- |
| `PacketFactoryRequest` | `Title` | `forbidden_upstream` | Packet authoring title belongs to upstream orchestration or campaign/publication contracts. |
| `PacketFactoryRequest` | `Subject` | `forbidden_upstream` | Narrative subject framing is upstream authoring meaning, not render execution input. |
| `PacketFactoryRequest` | `References` | `forbidden_upstream` | Upstream evidence/reference selection is not a media-owned render/job lifecycle concern. |
| `PacketFactoryRequest` | `Attachments` | `forbidden_upstream` | Attachment targeting mixes delivery/publication semantics into media contracts. |
| `PacketAttachmentTargetKind` | `Route`, `Message`, `Export` | `forbidden_upstream` | Target routing semantics belong upstream. |
| `PacketAttachmentRequest` | `TargetKind`, `TargetId`, `TargetLabel` | `forbidden_upstream` | Delivery/attachment targeting is not render-only ownership. |
| `PacketAttachmentBatchRequest` | `Attachments` | `forbidden_upstream` | Batch attachment semantics remain upstream. |
| `PacketAttachmentRecord` | `PacketId` | `forbidden_upstream` | Packet identity is upstream artifact/session meaning. |
| `PacketAttachmentRecord` | `TargetKind`, `TargetId`, `TargetLabel` | `forbidden_upstream` | Attachment/delivery target meaning belongs upstream. |
| `PacketFactoryResult` | `PacketId`, `Title`, `Subject`, `Html`, `Attachments`, `Evidence` | `forbidden_upstream` | Packet authoring/output semantics are upstream; only render artifact handles belong in media contracts. |
| `RouteCinemaRequest` | `SourceNode`, `TargetNode` | `forbidden_upstream` | Route selection/context belongs upstream; media should receive prepared render inputs only. |
| `RouteCinemaResult` | `SourceNode`, `TargetNode`, `Waypoints`, `WaypointScript`, `TravelSummary`, `ProjectionFingerprint`, `ReviewState` | `forbidden_upstream` | Route narration/review meaning is upstream context, not render/job/asset lifecycle state. |
| `RouteCinemaResult` | `ApprovalState`, `RetentionState`, `CreatedAtUtc`, `ExpiresAtUtc`, `PreviewAssetId`, `RouteVideoAssetId`, `PreviewJobId`, `PreviewJobState`, `RouteVideoJobId`, `RouteVideoJobState`, `Artifacts`, `CacheTtl` | `asset_lifecycle` / `job_lifecycle` | These fields are media-owned and should remain after the upstream residue is split out. |

Render-only compatibility types that still fit media ownership:
- `AssetLifecyclePolicy`
- `AssetCatalogItem`
- `AssetRenderResult`
- `AssetLifecycleMutationRequest`
- `AssetLifecycleSweepResult`
- `MediaRenderJobEnqueueRequest`
- `MediaRenderJobStatus`
- `PacketArtifactHandle`
- `RouteCinemaArtifactHandle`

## DS-07 Compatibility contract split plan

Required split/quarantine plan:
- `PacketFactoryRequest` / `PacketFactoryResult` / attachment DTOs: move packet authoring, attachment targeting, and HTML/evidence semantics upstream; keep only media artifact-handle/result DTOs in `Chummer.Media.Contracts`.
- `RouteCinemaRequest` / `RouteCinemaResult`: move route selection, narration, waypoint script, projection fingerprint, and review-state meaning upstream; keep only render job ids, asset ids, artifact handles, and lifecycle timestamps/status in media contracts.
- Compatibility transition posture: the known mixed compatibility DTOs in `src/Chummer.Media.Contracts/Compatibility/RunServices/MediaFactoryContracts.cs` now carry explicit `EXTRACT-008A quarantine` `Obsolete` markers until upstream owner packages absorb the removed meaning. Do not introduce upstream package dependencies here.

## DS-08 Guardrail expansion for compatibility residue

Implemented guardrails:
- `scripts/ai/contract-boundary-tests.sh` blocks obvious identifiers such as `Campaign*`, `Session*`, `Narrative*`, `Story*`, `Delivery*`, `RouteContext*`, and `Scene*`.
- The contract-boundary test now requires every known mixed compatibility DTO type to carry an explicit `EXTRACT-008A quarantine` `Obsolete` marker.
- The contract-boundary test now fails if those mixed DTO types appear anywhere outside `src/Chummer.Media.Contracts/Compatibility/RunServices/MediaFactoryContracts.cs`.
- This keeps the reopened residue explicitly quarantined while preventing silent spread into canonical render/job/asset lifecycle contracts.
- Verification passed on `2026-04-15` via `bash scripts/ai/contract-boundary-tests.sh` and `bash scripts/ai/verify.sh`.

## DS-09 Queue/worklist remap

`WORKLIST.md` now records the DTO split generic prompt as reopened for compatibility-shim residue rather than fully satisfied.

## Milestone mapping for DTO split queue slices

Program mapping:
- Milestone spine: `M8 finished media plant` (`.codex-design/repo/IMPLEMENTATION_SCOPE.md`)
- Program milestone: `E4 media plane complete` (`.codex-design/product/PROGRAM_MILESTONES.yaml`)
- Contract set: `media_execution_vnext` (`.codex-design/product/CONTRACT_SETS.yaml`)

Execution-to-gate mapping:
- `DS-01` + `DS-03` enforce render-only DTO ownership required for `M8` aggregate closure.
- `DS-02` + `DS-04` provide executable guardrails/tests used as completion truth gates for boundary integrity.
- `DS-05` maps active generic queue prompts to runnable units so milestone evidence remains explicit rather than implied.
- `DS-06` + `DS-07` inventory and plan the remaining compatibility-shim split work.
- `DS-08` adds the missing verification follow-through for compatibility DTO names that currently evade the guardrails.
- `DS-09` keeps queue/worklist truth honest while the compatibility follow-on lane remains open.

Current queue item `Add milestone mapping or executable queue work for Media DTOs ...` remains covered by the existing milestone mapping in this evidence file.
Current queue item `Publish or append runnable backlog for Media DTOs still need to be split cleanly between downstream render/job/asset lifecycle contracts and upstream narrative-authoring, delivery, and campaign-context contracts..` is not fully closed by `DS-01`..`DS-05` alone; it now maps to the appended `DS-06`..`DS-09` compatibility-residue lane.
