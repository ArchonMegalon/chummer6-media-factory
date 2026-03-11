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

`EXTRACT-008` generic queue prompt is now superseded by this execution evidence and removed from active queue items.
