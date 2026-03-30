using Chummer.Media.Contracts;
using Chummer.Run.AI.Services.Assets;
using Chummer.Campaign.Contracts;

var assets = new AssetLifecycleService();
var jobs = new MediaRenderJobService(assets);
var creatorPublications = new CreatorPublicationPlannerService();

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

var creatorPublicationPlan = creatorPublications.BuildPlan(
    new CreatorPublicationProjection(
        PublicationId: "publication-shadow-brief",
        Title: "Shadow brief creator packet",
        Kind: "campaign_packet",
        Summary: "Recap-safe outputs should share one governed creator publication posture.",
        CampaignId: "campaign-shadow",
        DossierId: "dossier-kestrel",
        ArtifactId: "artifact-shadow-brief",
        ProvenanceSummary: "sr6.preview.v1 + recap-safe output shelf",
        DiscoverySummary: "Group visibility with grounded provenance",
        Visibility: "group",
        PublicationStatus: "preview_ready",
        TrustBand: "review-pending",
        Discoverable: false,
        UpdatedAtUtc: DateTimeOffset.UtcNow,
        NextSafeAction: "Review the creator packet before the same governed dossier handoff leaves the account surface.",
        CampaignReturnSummary: "Return to the dossier-backed campaign checkpoint after creator review finishes.",
        SupportClosureSummary: "Creator publication stays pinned to the same campaign-safe support answer.",
        Watchouts:
        [
            "No local-only export notes should bypass the governed creator packet."
        ]),
    new BuildLabHandoffProjection(
        HandoffId: "handoff-shadow-brief",
        DossierId: "dossier-kestrel",
        CampaignId: "campaign-shadow",
        Title: "Shadow brief handoff",
        Summary: "Chosen build lane is attached to dossier and creator-safe outputs.",
        VariantLabel: "Ops-first dossier carry-forward",
        ProgressionLabel: "25 / 50 / 100 Karma path stays attached",
        ExplainEntryId: "buildlab.handoff.shadow-brief",
        TradeoffLines:
        [
            "Role overlap stays explicit before the packet is published.",
            "Campaign return remains the governing downstream truth."
        ],
        ProgressionOutcomes:
        [
            "Creator packet keeps dossier-safe and campaign-safe outputs aligned."
        ],
        Outputs:
        [
            new PublicationSafeProjection("projection-dossier", "dossier_card", "Living dossier", "Stable runner identity.", "artifact-dossier"),
            new PublicationSafeProjection("projection-recap", "recap_brief", "Recap brief", "Campaign recap-safe packet.", "artifact-recap")
        ],
        UpdatedAtUtc: DateTimeOffset.UtcNow,
        NextSafeAction: "Publish the recap-safe packet only after the dossier checkpoint is accepted.",
        CampaignReturnSummary: "Resume the campaign from the same dossier-backed checkpoint after publication review.",
        SupportClosureSummary: "Reuse the handoff receipt when support verifies the creator packet against the campaign spine.",
        PlannerCoverageSummary: "4 of 4 build follow-through checkpoints are already grounded.",
        PlannerCoverageLines:
        [
            "Campaign continuity: Shadow Circuit is already attached as the governed return lane for this handoff.",
            "Outputs: 2 dossier or campaign-safe outputs are already attached to the handoff.",
            "Restore posture: no restore conflicts are currently blocking replay-safe handoff follow-through.",
            "Claimed install: 1 linked device is already attached for install-aware follow-through."
        ]));
Assert(string.Equals(creatorPublicationPlan.PacketRequest.Title, "Shadow brief creator packet", StringComparison.Ordinal), "Creator publication planner should reuse the governed publication title.");
Assert(creatorPublicationPlan.AttachmentBatch.Attachments.Count >= 4, "Creator publication planner should attach creator publication status, campaign, dossier, and output shelves.");
Assert(creatorPublicationPlan.PacketRequest.References?.Contains("publication-shadow-brief", StringComparer.Ordinal) == true, "Creator publication planner should keep the creator publication id as a first-class packet reference.");
Assert(creatorPublicationPlan.EvidenceLines.Any(static line => line.Contains("recap-safe", StringComparison.OrdinalIgnoreCase)), "Creator publication planner should retain recap-safe provenance evidence.");
Assert(creatorPublicationPlan.EvidenceLines.Any(static line => line.Contains("Discovery:", StringComparison.Ordinal)), "Creator publication planner should label discovery posture explicitly.");
Assert(creatorPublicationPlan.EvidenceLines.Any(static line => line.Contains("Trust band: Review Pending", StringComparison.Ordinal)), "Creator publication planner should label trust ranking explicitly.");
Assert(creatorPublicationPlan.EvidenceLines.Any(static line => line.Contains("Discoverable: No", StringComparison.Ordinal)), "Creator publication planner should label discoverability explicitly.");
Assert(creatorPublicationPlan.EvidenceLines.Any(static line => line.Contains("Ownership:", StringComparison.Ordinal)), "Creator publication planner should label ownership posture explicitly.");
Assert(creatorPublicationPlan.EvidenceLines.Any(static line => line.Contains("State: Preview Ready", StringComparison.Ordinal)), "Creator publication planner should label publication state explicitly.");
Assert(creatorPublicationPlan.EvidenceLines.Any(static line => line.Contains("Planner coverage:", StringComparison.Ordinal)), "Creator publication planner should preserve planner-coverage summary from the governed build handoff.");
Assert(creatorPublicationPlan.EvidenceLines.Any(static line => line.Contains("Campaign continuity:", StringComparison.Ordinal)), "Creator publication planner should preserve planner-coverage evidence lines from the governed build handoff.");
Assert(creatorPublicationPlan.PacketRequest.References?.Contains("handoff-shadow-brief", StringComparer.Ordinal) == true, "Creator publication planner should include the governed handoff reference.");
Assert(creatorPublicationPlan.PacketRequest.References?.Contains("buildlab.handoff.shadow-brief", StringComparer.Ordinal) == true, "Creator publication planner should include the explain entry reference.");
Assert(creatorPublicationPlan.EvidenceLines.Any(static line => line.Contains("Next safe action:", StringComparison.Ordinal)), "Creator publication planner should surface the next safe action.");
Assert(creatorPublicationPlan.EvidenceLines.Any(static line => line.Contains("Campaign return:", StringComparison.Ordinal)), "Creator publication planner should surface the campaign return summary.");
Assert(creatorPublicationPlan.EvidenceLines.Any(static line => line.Contains("Support closure:", StringComparison.Ordinal)), "Creator publication planner should surface the support closure summary.");
Assert(string.Equals(creatorPublicationPlan.NextAction, "queue_review", StringComparison.Ordinal), "Preview-ready creator publications should route into review next.");

var creatorPublicationWithoutHandoff = creatorPublications.BuildPlan(
    new CreatorPublicationProjection(
        PublicationId: "publication-shadow-brief-no-handoff",
        Title: "Shadow brief creator packet without attached handoff",
        Kind: "campaign_packet",
        Summary: "Creator publication should still preserve continuity even when the explicit handoff record is missing.",
        CampaignId: "campaign-shadow",
        DossierId: "dossier-kestrel",
        ArtifactId: "artifact-shadow-brief-no-handoff",
        ProvenanceSummary: "sr6.preview.v1 + recap-safe output shelf",
        DiscoverySummary: "Group visibility with grounded provenance",
        Visibility: "group",
        PublicationStatus: "preview_ready",
        TrustBand: "review-pending",
        Discoverable: false,
        UpdatedAtUtc: DateTimeOffset.UtcNow,
        NextSafeAction: "Review creator publication continuity before sending it into approval.",
        CampaignReturnSummary: "Return through the same campaign checkpoint after creator review.",
        SupportClosureSummary: "Reuse the creator publication packet when support validates the governed campaign output.",
        Watchouts:
        [
            "Keep creator publication subordinate to the shared campaign workspace."
        ]));
Assert(creatorPublicationWithoutHandoff.EvidenceLines.Any(static line => line.Contains("Next safe action: Review creator publication continuity", StringComparison.Ordinal)), "Creator publication planner should preserve publication next-safe-action evidence even without an explicit handoff.");
Assert(creatorPublicationWithoutHandoff.EvidenceLines.Any(static line => line.Contains("Campaign return: Return through the same campaign checkpoint", StringComparison.Ordinal)), "Creator publication planner should preserve publication campaign-return evidence even without an explicit handoff.");
Assert(creatorPublicationWithoutHandoff.EvidenceLines.Any(static line => line.Contains("Support closure: Reuse the creator publication packet", StringComparison.Ordinal)), "Creator publication planner should preserve publication support-closure evidence even without an explicit handoff.");
Assert(creatorPublicationWithoutHandoff.EvidenceLines.Any(static line => line.Contains("Trust band: Review Pending", StringComparison.Ordinal)), "Creator publication planner should preserve trust ranking evidence even without an explicit handoff.");
Assert(creatorPublicationWithoutHandoff.EvidenceLines.Any(static line => line.Contains("Discoverable: No", StringComparison.Ordinal)), "Creator publication planner should preserve discoverability evidence even without an explicit handoff.");
Assert(creatorPublicationWithoutHandoff.EvidenceLines.Any(static line => line.Contains("Watchout: Keep creator publication subordinate", StringComparison.Ordinal)), "Creator publication planner should preserve publication watchouts even without an explicit handoff.");
Assert(creatorPublicationWithoutHandoff.PacketRequest.References?.Contains("campaign-shadow", StringComparer.Ordinal) == true, "Creator publication planner without a handoff should still keep the governed campaign reference.");
Assert(creatorPublicationWithoutHandoff.PacketRequest.References?.Contains("publication-shadow-brief-no-handoff", StringComparer.Ordinal) == true, "Creator publication planner without a handoff should still keep the creator publication id reference.");

await Task.Delay(120);
var sweep = restoredAssets.SweepExpired(DateTimeOffset.UtcNow);
Assert(sweep.ExpiredAssetCount >= 1, "Retention sweep after restore must expire cache-only assets.");

var expiredCacheAsset = restoredAssets.Resolve(cacheOnlyAsset.AssetId);
Assert(expiredCacheAsset is null, "Expired cache-only assets must not resolve after restore and sweep.");

var expiredJob = restoredJobs.Get(succeededJob.JobId);
Assert(expiredJob is not null, "Expired render jobs must remain inspectable after restore.");
Assert(expiredJob!.State == MediaRenderJobState.Expired, "Render-job expiry must still work after restore.");

Console.WriteLine("Media factory runtime verification passed.");

static async Task<MediaRenderJobStatus> WaitForSucceededJobAsync(IMediaRenderJobService jobs, string jobId)
{
    for (var attempt = 0; attempt < 100; attempt++)
    {
        var job = jobs.Get(jobId);
        if (job is not null && job.State == MediaRenderJobState.Succeeded)
        {
            return job;
        }

        await Task.Delay(20);
    }

    throw new InvalidOperationException($"Timed out waiting for media job '{jobId}' to succeed.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
