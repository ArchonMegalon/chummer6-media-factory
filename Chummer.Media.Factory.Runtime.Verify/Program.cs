using Chummer.Campaign.Contracts;
using Chummer.Media.Contracts;
using Chummer.Run.AI.Services.Assets;

var assets = new AssetLifecycleService();
var jobs = new MediaRenderJobService(assets);

var approvalAsset = await assets.StoreAsync(
    category: "portrait/canon",
    content: "{\"portrait\":\"canon\"}",
    source: "restore-drill",
    policy: new AssetLifecyclePolicy(
        CacheTtl: TimeSpan.FromMinutes(5),
        LongTermCache: false,
        MaxBytes: 4096,
        RequiresApproval: true,
        PersistOnApproval: true,
        StorageClass: AssetStorageClass.ObjectStorage,
        AllowPersistentPinning: true));

await assets.ApplyLifecycleAsync(
    approvalAsset.AssetId,
    new AssetLifecycleMutationRequest(
        ApprovalState: AssetApprovalState.Approved,
        Pin: true,
        Persist: true,
        Reason: "pin canon"));
await assets.ApplyLifecycleAsync(
    approvalAsset.AssetId,
    new AssetLifecycleMutationRequest(
        ApprovalState: AssetApprovalState.Approved,
        Pin: true,
        Persist: true,
        Reason: "pin canon"));

var cacheOnlyAsset = await assets.StoreAsync(
    category: "packet/preview",
    content: "<html>preview</html>",
    source: "restore-drill",
    policy: new AssetLifecyclePolicy(
        CacheTtl: TimeSpan.FromMilliseconds(80),
        LongTermCache: false,
        MaxBytes: 4096,
        RequiresApproval: false,
        PersistOnApproval: false,
        StorageClass: AssetStorageClass.ObjectStorage,
        AllowPersistentPinning: false));

var firstJob = await jobs.EnqueueAsync(new MediaRenderJobEnqueueRequest(
    JobType: MediaRenderJobType.DocumentPreviewImage,
    DeduplicationKey: "restore-drill:preview",
    Category: "packet/preview",
    Payload: "<html>preview</html>",
    Source: "restore-drill",
    CacheTtl: TimeSpan.FromMilliseconds(80),
    MaxBytes: 4096,
    RequiresApproval: false,
    PersistOnApproval: false,
    AllowPersistentPinning: false));

var succeededJob = await WaitForSucceededJobAsync(jobs, firstJob.JobId);
var replayedJob = await jobs.EnqueueAsync(new MediaRenderJobEnqueueRequest(
    JobType: MediaRenderJobType.DocumentPreviewImage,
    DeduplicationKey: "restore-drill:preview",
    Category: "packet/preview",
    Payload: "<html>preview</html>",
    Source: "restore-drill",
    CacheTtl: TimeSpan.FromMilliseconds(80),
    MaxBytes: 4096,
    RequiresApproval: false,
    PersistOnApproval: false,
    AllowPersistentPinning: false));

Assert(string.Equals(firstJob.JobId, replayedJob.JobId, StringComparison.Ordinal), "Replay-safe dedupe should reuse the same job before expiry.");

var backup = MediaFactoryRuntimeBackup.Export(assets, jobs);
Assert(string.Equals(backup.ContractFamily, MediaFactoryRuntimeBackup.ContractFamily, StringComparison.Ordinal), "Backup contract family must stay stable.");
Assert(backup.Assets.Assets.Count >= 2, "Backup must retain both approval and cache assets.");
Assert(backup.Jobs.Jobs.Count >= 1, "Backup must retain render-job rows.");

var restoredAssets = new AssetLifecycleService();
var restoredJobs = new MediaRenderJobService(restoredAssets);
MediaFactoryRuntimeBackup.Restore(restoredAssets, restoredJobs, backup);

var restoredPinned = restoredAssets.Resolve(approvalAsset.AssetId);
Assert(restoredPinned is not null, "Restore must retain pinned approval assets.");
Assert(restoredPinned!.RetentionState == AssetRetentionState.Pinned, "Pinned approval assets must stay pinned after restore.");
Assert(restoredPinned.StorageClass == AssetStorageClass.LongTermObjectStorage, "Pinned approval assets must keep long-term storage after restore.");

var restoredPipeline = restoredAssets.GetApprovalPipelineProjection();
Assert(restoredPipeline.Idempotency.ReplayCount >= 1, "Restore must preserve lifecycle replay counters.");

var restoredJob = restoredJobs.Get(succeededJob.JobId);
Assert(restoredJob is not null, "Restore must retain succeeded render jobs.");
Assert(restoredJob!.State == MediaRenderJobState.Succeeded, "Succeeded render jobs must remain succeeded immediately after restore.");

var restoredMediaProjection = restoredJobs.GetMediaPipelineProjection();
Assert(restoredMediaProjection.Idempotency.ReplayCount >= 1, "Restore must preserve media dedupe replay counts.");

var publication = new CreatorPublicationProjection(
    PublicationId: "publication-creator-roadmap",
    Title: "Creator publication roadmap",
    Kind: "creator_guide",
    Summary: "Plan and publication proof for the creator lane.",
    CampaignId: "campaign-creator-01",
    DossierId: "dossier-creator-01",
    ArtifactId: "artifact-creator-01",
    ProvenanceSummary: "Lane provenance stays attached to the governed publication shelf.",
    DiscoverySummary: "Discovery stays public once the publication reaches the published state.",
    Visibility: "public",
    PublicationStatus: "published",
    TrustBand: "trusted",
    Discoverable: true,
    UpdatedAtUtc: DateTimeOffset.UtcNow,
    NextSafeAction: "share_public_publication",
    CampaignReturnSummary: "Campaign return stays aligned with the publication lane.",
    SupportClosureSummary: "Support closes with the publication shelf release.",
    BuildHandoffId: "handoff-creator-01",
    Watchouts: ["Lane metadata is still under review."],
    LineageSummary: "Inherited from the build lab handoff.",
    TrustSummary: "Trusted publication lane.",
    ComparisonSummary: "Matches the governed publication posture.",
    ModerationSummary: "Moderation proof is retained.");

var handoff = new BuildLabHandoffProjection(
    HandoffId: "handoff-creator-01",
    DossierId: "dossier-creator-01",
    CampaignId: "campaign-creator-01",
    Title: "Creator publication handoff",
    Summary: "Publication proof handoff for the creator lane.",
    VariantLabel: "creator-publication",
    ProgressionLabel: "publish",
    ExplainEntryId: "explain-creator-01",
    TradeoffLines: ["Tradeoff: public visibility retains governed review."],
    ProgressionOutcomes: ["Outcome: creator publication can be shared publicly."],
    Outputs:
    [
        new PublicationSafeProjection(
            ProjectionId: "output-creator-01",
            Kind: "run_module",
            Label: "Run module",
            Summary: "Run module output stays attached to the publication lane.",
            ArtifactId: "artifact-output-creator-01",
            Discoverable: true,
            PublicationState: "published",
            TrustBand: "trusted",
            PublicationSummary: "Publication-ready output.",
            CreatorPublicationId: "publication-creator-roadmap",
            NextSafeAction: "share_public_publication",
            ProvenanceSummary: "Originated from the creator publication handoff.",
            AuditSummary: "Audit proof retained.")
    ],
    UpdatedAtUtc: DateTimeOffset.UtcNow,
    NextSafeAction: "share_public_publication",
    CampaignReturnSummary: "Campaign return is ready for publication release.",
    SupportClosureSummary: "Support closes once the lane is shared.",
    PlannerCoverageSummary: "Planner coverage retains the governed lane proof.",
    PlannerCoverageLines: ["Planner coverage line: creator publication lane proof."],
    CrewFitSummary: "Crew fit remains aligned with the publication path.",
    ConditionalStateSummary: "Conditional state stays aligned with public publication.",
    ConditionalStateLines: ["Conditional state line: public publication remains governed."],
    SourceHintSummary: "Source hints keep the publication lane grounded.",
    SourceHintLines: ["Source hint line: publication lane references remain intact."],
    BuildSurfaceSummary: "Build surface supports creator publication proof.",
    BuildSurfaceLines: ["Build surface line: proof survives restore."],
    ExchangeParitySummary: "Exchange parity stays intact for publication proof.",
    ExchangeParityLines:
    [
        "JSON exchange: creator publication records remain synchronized.",
        "Foundry exchange: creator publication records remain synchronized.",
        "Sheet viewer: creator publication records remain synchronized.",
        "Print PDF: creator publication records remain synchronized.",
        "Character template export: creator publication records remain synchronized."
    ],
    PortabilityPillarSummary: "Portability pillar stays attached to the creator publication path.",
    PortabilityPillarLines:
    [
        "Replay timeline: creator publication replay stays recoverable.",
        "Session recap: creator publication recap stays recoverable.",
        "Run module: creator publication module stays recoverable."
    ]);

var creatorPublicationPlan = new CreatorPublicationPlannerService().BuildPlan(publication, handoff);
Assert(creatorPublicationPlan.EvidenceLines.Any(static line => line.Contains("JSON exchange:", StringComparison.Ordinal)), "Creator publication planner must preserve the JSON exchange lane line.");
Assert(creatorPublicationPlan.EvidenceLines.Any(static line => line.Contains("Foundry exchange:", StringComparison.Ordinal)), "Creator publication planner must preserve the Foundry exchange lane line.");
Assert(creatorPublicationPlan.EvidenceLines.Any(static line => line.Contains("Sheet viewer:", StringComparison.Ordinal)), "Creator publication planner must preserve the Sheet viewer lane line.");
Assert(creatorPublicationPlan.EvidenceLines.Any(static line => line.Contains("Print PDF:", StringComparison.Ordinal)), "Creator publication planner must preserve the Print PDF lane line.");
Assert(creatorPublicationPlan.EvidenceLines.Any(static line => line.Contains("Character template export:", StringComparison.Ordinal)), "Creator publication planner must preserve the character template export lane line.");
Assert(creatorPublicationPlan.EvidenceLines.Any(static line => line.Contains("Replay timeline:", StringComparison.Ordinal)), "Creator publication planner must preserve the replay timeline lane line.");
Assert(creatorPublicationPlan.EvidenceLines.Any(static line => line.Contains("Session recap:", StringComparison.Ordinal)), "Creator publication planner must preserve the session recap lane line.");
Assert(creatorPublicationPlan.EvidenceLines.Any(static line => line.Contains("Run module:", StringComparison.Ordinal)), "Creator publication planner must preserve the run module lane line.");

var expiredSweep = restoredAssets.SweepExpired(DateTimeOffset.UtcNow.AddMinutes(10));
Assert(expiredSweep.ExpiredAssetCount >= 1, "Retention sweep should expire cache-only entries after TTL.");
Assert(restoredAssets.Resolve(cacheOnlyAsset.AssetId) is null, "Cache-only assets should be gone after expiry sweep.");

await Task.Delay(160);
var expiredJob = restoredJobs.Get(succeededJob.JobId);
Assert(expiredJob is not null, "Expired render jobs must remain inspectable after restore.");
Assert(expiredJob!.State == MediaRenderJobState.Expired, "Succeeded render jobs must transition to expired after TTL progression.");

Console.WriteLine("runtime verify ok");

static async Task<MediaRenderJobStatus> WaitForSucceededJobAsync(IMediaRenderJobService jobs, string jobId)
{
    for (var attempt = 0; attempt < 50; attempt++)
    {
        var status = jobs.Get(jobId);
        if (status?.State == MediaRenderJobState.Succeeded)
        {
            return status;
        }

        if (status?.State == MediaRenderJobState.Failed)
        {
            throw new InvalidOperationException($"Job {jobId} failed: {status.Error ?? "unknown"}");
        }

        await Task.Delay(20);
    }

    throw new TimeoutException($"Job {jobId} did not reach succeeded state in time.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
