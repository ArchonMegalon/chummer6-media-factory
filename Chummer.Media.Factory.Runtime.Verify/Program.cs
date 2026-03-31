using Chummer.Media.Contracts;
using Chummer.Run.AI.Services.Assets;
using Chummer.Campaign.Contracts;

var assets = new AssetLifecycleService();
var jobs = new MediaRenderJobService(assets);
var creatorPublications = new CreatorPublicationPlannerService();
var prepPackets = new GovernedPrepPacketPlannerService();

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
        Title: "Shadow brief campaign packet",
        Kind: "campaign_packet",
        Summary: "Recap-safe outputs should share one governed publication posture.",
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
        LineageSummary: "Shadow brief handoff remains the current lineage anchor for artifact artifact-shadow-brief until a governed successor publication replaces it.",
        TrustSummary: "Trust ranking is review-pending and stays anchored to governed provenance, rule fingerprint, and campaign continuity until approval clears.",
        ComparisonSummary: "Compare by provenance, visibility, trust ranking, lineage, Shadow brief handoff receipts, and campaign-return fit instead of popularity, install counts, or shelf age.",
        ModerationSummary: "Moderation is still waiting on approval review, so discovery stays bounded to publication, campaign, and operator surfaces.",
        NextSafeAction: "Review the publication lane before the same governed dossier handoff leaves the account surface.",
        CampaignReturnSummary: "Return to the dossier-backed campaign checkpoint after creator review finishes.",
        SupportClosureSummary: "Publication stays pinned to the same campaign-safe support answer.",
        Watchouts:
        [
            "No local-only export notes should bypass the governed publication lane."
        ]),
    new BuildLabHandoffProjection(
        HandoffId: "handoff-shadow-brief",
        DossierId: "dossier-kestrel",
        CampaignId: "campaign-shadow",
        Title: "Shadow brief handoff",
        Summary: "Chosen build lane is attached to dossier and publication-safe outputs.",
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
            "Publication lane keeps dossier-safe and campaign-safe outputs aligned."
        ],
        Outputs:
        [
            new PublicationSafeProjection(
                "projection-dossier",
                "dossier_card",
                "Living dossier",
                "Stable runner identity.",
                "artifact-dossier",
                OwnershipSummary: "Dossier truth stays on the same governed publication lane while publication review is still bounded."),
            new PublicationSafeProjection(
                "projection-recap",
                "recap_brief",
                "Recap brief",
                "Campaign recap-safe packet.",
                "artifact-recap",
                OwnershipSummary: "Recap-safe packet stays on the same governed publication lane until the review completes.",
                PublicationSummary: "Preview-ready recap-safe packet is still bounded until publication review clears.",
                NextSafeAction: "Review the recap-safe packet before widening the artifact audience.",
                ProvenanceSummary: "sr6.preview.v1 + run scope + continuity keep the recap packet grounded on the same artifact.",
                AuditSummary: "Generated 2026-03-30 12:00 UTC from the same governed recap receipt."),
            new PublicationSafeProjection(
                "projection-replay",
                "replay_timeline",
                "Replay timeline",
                "Contested-turn review stays attached to the governed replay artifact.",
                "artifact-replay",
                OwnershipSummary: "Replay timeline stays on the same governed publication lane until publication review deliberately widens the audience.",
                PublicationSummary: "Preview-ready replay timeline is still bounded while contested-turn review stays on the publication lane.",
                NextSafeAction: "Review the replay timeline before widening the replay artifact audience.",
                ProvenanceSummary: "sr6.preview.v1 + contested turn receipts + continuity keep the replay timeline grounded on the same artifact.",
                AuditSummary: "Generated 2026-03-30 12:05 UTC from the same governed replay receipt.")
        ],
        UpdatedAtUtc: DateTimeOffset.UtcNow,
        NextSafeAction: "Publish the recap-safe packet only after the dossier checkpoint is accepted.",
        CampaignReturnSummary: "Resume the campaign from the same dossier-backed checkpoint after publication review.",
        SupportClosureSummary: "Reuse the handoff receipt when support verifies the publication lane against the campaign spine.",
        PlannerCoverageSummary: "4 of 4 build follow-through checkpoints are already grounded.",
        PlannerCoverageLines:
        [
            "Campaign continuity: Shadow Circuit is already attached as the governed return lane for this handoff.",
            "Outputs: 2 dossier or campaign-safe outputs are already attached to the handoff.",
            "Restore posture: no restore conflicts are currently blocking replay-safe handoff follow-through.",
            "Claimed install: 1 linked device is already attached for install-aware follow-through."
        ]));
Assert(string.Equals(creatorPublicationPlan.PacketRequest.Title, "Shadow brief campaign packet", StringComparison.Ordinal), "Creator publication planner should reuse the governed publication title.");
Assert(creatorPublicationPlan.AttachmentBatch.Attachments.Count >= 5, "Creator publication planner should attach publication status, campaign, dossier, recap, and replay output shelves.");
Assert(creatorPublicationPlan.PacketRequest.References?.Contains("publication-shadow-brief", StringComparer.Ordinal) == true, "Creator publication planner should keep the creator publication id as a first-class packet reference.");
Assert(creatorPublicationPlan.EvidenceLines.Any(static line => line.Contains("Publication kind: Campaign Packet", StringComparison.Ordinal)), "Creator publication planner should surface publication kind explicitly.");
Assert(creatorPublicationPlan.EvidenceLines.Any(static line => line.Contains("recap-safe", StringComparison.OrdinalIgnoreCase)), "Creator publication planner should retain recap-safe provenance evidence.");
Assert(creatorPublicationPlan.PacketRequest.References?.Contains("artifact-replay", StringComparer.Ordinal) == true, "Creator publication planner should keep replay artifact ids as first-class packet references.");
Assert(creatorPublicationPlan.EvidenceLines.Any(static line => line.Contains("Discovery:", StringComparison.Ordinal)), "Creator publication planner should label discovery posture explicitly.");
Assert(creatorPublicationPlan.EvidenceLines.Any(static line => line.Contains("Trust band: Review Pending", StringComparison.Ordinal)), "Creator publication planner should label trust ranking explicitly.");
Assert(creatorPublicationPlan.EvidenceLines.Any(static line => line.Contains("Trust posture:", StringComparison.Ordinal)), "Creator publication planner should preserve trust reasoning through packet formatting.");
Assert(creatorPublicationPlan.EvidenceLines.Any(static line => line.Contains("Compare by:", StringComparison.Ordinal)), "Creator publication planner should preserve creator-comparison reasoning through packet formatting.");
Assert(creatorPublicationPlan.EvidenceLines.Any(static line => line.Contains("Discoverable: No", StringComparison.Ordinal)), "Creator publication planner should label discoverability explicitly.");
Assert(creatorPublicationPlan.EvidenceLines.Any(static line => line.Contains("Ownership:", StringComparison.Ordinal)), "Creator publication planner should label ownership posture explicitly.");
Assert(creatorPublicationPlan.EvidenceLines.Any(static line => line.Contains("State: Preview Ready", StringComparison.Ordinal)), "Creator publication planner should label publication state explicitly.");
Assert(creatorPublicationPlan.EvidenceLines.Any(static line => line.Contains("Lineage: Shadow brief handoff remains the current lineage anchor", StringComparison.Ordinal)), "Creator publication planner should label lineage posture explicitly.");
Assert(creatorPublicationPlan.EvidenceLines.Any(static line => line.Contains("Output kind (Replay timeline): Replay timeline stays attached", StringComparison.Ordinal)), "Creator publication planner should preserve replay output kind posture through packet formatting.");
Assert(creatorPublicationPlan.EvidenceLines.Any(static line => line.Contains("Output ownership (Replay timeline):", StringComparison.Ordinal)), "Creator publication planner should preserve replay output ownership posture through packet formatting.");
Assert(creatorPublicationPlan.EvidenceLines.Any(static line => line.Contains("Output publication (Replay timeline):", StringComparison.Ordinal)), "Creator publication planner should preserve replay output publication posture through packet formatting.");
Assert(creatorPublicationPlan.EvidenceLines.Any(static line => line.Contains("Output next safe action (Replay timeline):", StringComparison.Ordinal)), "Creator publication planner should preserve replay output next-step posture through packet formatting.");
Assert(creatorPublicationPlan.EvidenceLines.Any(static line => line.Contains("Output provenance (Replay timeline):", StringComparison.Ordinal)), "Creator publication planner should preserve replay-output provenance through packet formatting.");
Assert(creatorPublicationPlan.EvidenceLines.Any(static line => line.Contains("Output audit (Replay timeline):", StringComparison.Ordinal)), "Creator publication planner should preserve replay-output audit posture through packet formatting.");
Assert(creatorPublicationPlan.EvidenceLines.Any(static line => line.Contains("Output provenance (Recap brief):", StringComparison.Ordinal)), "Creator publication planner should preserve recap-output provenance through packet formatting.");
Assert(creatorPublicationPlan.EvidenceLines.Any(static line => line.Contains("Output audit (Recap brief):", StringComparison.Ordinal)), "Creator publication planner should preserve recap-output audit posture through packet formatting.");
Assert(creatorPublicationPlan.EvidenceLines.Any(static line => line.Contains("Planner coverage:", StringComparison.Ordinal)), "Creator publication planner should preserve planner-coverage summary from the governed build handoff.");
Assert(creatorPublicationPlan.EvidenceLines.Any(static line => line.Contains("Campaign continuity:", StringComparison.Ordinal)), "Creator publication planner should preserve planner-coverage evidence lines from the governed build handoff.");
Assert(creatorPublicationPlan.PacketRequest.References?.Contains("handoff-shadow-brief", StringComparer.Ordinal) == true, "Creator publication planner should include the governed handoff reference.");
Assert(creatorPublicationPlan.PacketRequest.References?.Contains("buildlab.handoff.shadow-brief", StringComparer.Ordinal) == true, "Creator publication planner should include the explain entry reference.");
Assert(creatorPublicationPlan.EvidenceLines.Any(static line => line.Contains("Next safe action:", StringComparison.Ordinal)), "Creator publication planner should surface the next safe action.");
Assert(creatorPublicationPlan.EvidenceLines.Any(static line => line.Contains("Campaign return:", StringComparison.Ordinal)), "Creator publication planner should surface the campaign return summary.");
Assert(creatorPublicationPlan.EvidenceLines.Any(static line => line.Contains("Support closure:", StringComparison.Ordinal)), "Creator publication planner should surface the support closure summary.");
Assert(creatorPublicationPlan.EvidenceLines.Any(static line => line.Contains("Moderation:", StringComparison.Ordinal)), "Creator publication planner should preserve moderation reasoning through packet formatting.");
Assert(string.Equals(creatorPublicationPlan.NextAction, "queue_review", StringComparison.Ordinal), "Preview-ready creator publications should route into review next.");

var creatorPublicationWithoutHandoff = creatorPublications.BuildPlan(
    new CreatorPublicationProjection(
        PublicationId: "publication-shadow-brief-no-handoff",
        Title: "Shadow brief campaign packet without attached handoff",
        Kind: "campaign_packet",
        Summary: "Publication should still preserve continuity even when the explicit handoff record is missing.",
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
        LineageSummary: "Shadow Circuit keeps artifact artifact-shadow-brief-no-handoff as the current lineage anchor until a governed successor publication is promoted.",
        TrustSummary: "Trust ranking is review-pending and stays anchored to governed provenance, rule fingerprint, and campaign continuity until approval clears.",
        ComparisonSummary: "Compare by provenance, visibility, trust ranking, lineage, and campaign-return fit instead of popularity, install counts, or shelf age.",
        ModerationSummary: "Moderation is still waiting on approval review, so discovery stays bounded to publication, campaign, and operator surfaces.",
        NextSafeAction: "Review publication continuity before sending it into approval.",
        CampaignReturnSummary: "Return through the same campaign checkpoint after creator review.",
        SupportClosureSummary: "Reuse the publication packet when support validates the governed campaign output.",
        Watchouts:
        [
            "Keep publication subordinate to the shared campaign workspace."
        ]));
Assert(creatorPublicationWithoutHandoff.EvidenceLines.Any(static line => line.Contains("Next safe action: Review publication continuity", StringComparison.Ordinal)), "Creator publication planner should preserve publication next-safe-action evidence even without an explicit handoff.");
Assert(creatorPublicationWithoutHandoff.EvidenceLines.Any(static line => line.Contains("Campaign return: Return through the same campaign checkpoint", StringComparison.Ordinal)), "Creator publication planner should preserve publication campaign-return evidence even without an explicit handoff.");
Assert(creatorPublicationWithoutHandoff.EvidenceLines.Any(static line => line.Contains("Support closure: Reuse the publication packet", StringComparison.Ordinal)), "Creator publication planner should preserve publication support-closure evidence even without an explicit handoff.");
Assert(creatorPublicationWithoutHandoff.EvidenceLines.Any(static line => line.Contains("Trust band: Review Pending", StringComparison.Ordinal)), "Creator publication planner should preserve trust ranking evidence even without an explicit handoff.");
Assert(creatorPublicationWithoutHandoff.EvidenceLines.Any(static line => line.Contains("Trust posture:", StringComparison.Ordinal)), "Creator publication planner should preserve trust posture evidence even without an explicit handoff.");
Assert(creatorPublicationWithoutHandoff.EvidenceLines.Any(static line => line.Contains("Compare by:", StringComparison.Ordinal)), "Creator publication planner should preserve comparison evidence even without an explicit handoff.");
Assert(creatorPublicationWithoutHandoff.EvidenceLines.Any(static line => line.Contains("Discoverable: No", StringComparison.Ordinal)), "Creator publication planner should preserve discoverability evidence even without an explicit handoff.");
Assert(creatorPublicationWithoutHandoff.EvidenceLines.Any(static line => line.Contains("Lineage: Shadow Circuit keeps artifact artifact-shadow-brief-no-handoff", StringComparison.Ordinal)), "Creator publication planner should preserve lineage evidence even without an explicit handoff.");
Assert(creatorPublicationWithoutHandoff.EvidenceLines.Any(static line => line.Contains("Moderation:", StringComparison.Ordinal)), "Creator publication planner should preserve moderation evidence even without an explicit handoff.");
Assert(creatorPublicationWithoutHandoff.EvidenceLines.Any(static line => line.Contains("Watchout: Keep publication subordinate", StringComparison.Ordinal)), "Creator publication planner should preserve publication watchouts even without an explicit handoff.");
Assert(creatorPublicationWithoutHandoff.PacketRequest.References?.Contains("campaign-shadow", StringComparer.Ordinal) == true, "Creator publication planner without a handoff should still keep the governed campaign reference.");
Assert(creatorPublicationWithoutHandoff.PacketRequest.References?.Contains("publication-shadow-brief-no-handoff", StringComparer.Ordinal) == true, "Creator publication planner without a handoff should still keep the creator publication id reference.");

var publishedCreatorPublicationPlan = creatorPublications.BuildPlan(
    new CreatorPublicationProjection(
        PublicationId: "publication-shadow-brief-public",
        Title: "Shadow brief campaign packet on public discovery",
        Kind: "campaign_packet",
        Summary: "Published shared publication is now live on governed public discovery.",
        CampaignId: "campaign-shadow",
        DossierId: "dossier-kestrel",
        ArtifactId: "artifact-shadow-brief-public",
        ProvenanceSummary: "sr6.preview.v1 + recap-safe output shelf + published shared publication",
        DiscoverySummary: "Eligible for governed discovery, creator comparison, and shelf projection.",
        Visibility: "shared",
        PublicationStatus: "published",
        TrustBand: "curated-live",
        Discoverable: true,
        UpdatedAtUtc: DateTimeOffset.UtcNow,
        LineageSummary: "Shadow brief public packet remains the live lineage anchor until a governed successor replaces it.",
        TrustSummary: "Trust ranking is live on governed discovery and stays anchored to provenance, lineage, and campaign continuity instead of popularity fog.",
        ComparisonSummary: "Compare by provenance, trust ranking, lineage, and campaign-return fit instead of popularity or install counts.",
        ModerationSummary: "Moderation watch is active but clear, so the publication can stay on discoverable public shelves until a later note changes its posture.",
        NextSafeAction: "Keep the governed publication live on public discovery, lineage, and shelf surfaces while provenance and support posture stay current.",
        CampaignReturnSummary: "Return through the same published publication whenever the campaign checkpoint needs to be re-opened.",
        SupportClosureSummary: "Reuse the public publication when support validates the published governed output."));
Assert(publishedCreatorPublicationPlan.AttachmentBatch.Attachments.Any(static item => string.Equals(item.TargetLabel, "Public publication", StringComparison.Ordinal)), "Published creator publication planner should rename the lead attachment to the public publication once discovery is live.");
Assert(publishedCreatorPublicationPlan.EvidenceLines.Any(static line => line.Contains("Public route: /artifacts/publications/publication-shadow-brief-public", StringComparison.Ordinal)), "Published creator publication planner should preserve the shared public publication route.");
Assert(string.Equals(publishedCreatorPublicationPlan.NextAction, "share_public_publication", StringComparison.Ordinal), "Published discoverable creator publications should route into public publication sharing next.");

var governedPrepPacketPlan = prepPackets.BuildPlan(
    new GovernedPrepPacketProjection(
        WorkspaceId: "workspace-shadow-circuit",
        CampaignId: "campaign-shadow",
        PacketId: "opposition:workspace-shadow-circuit",
        Kind: "opposition_packet",
        Title: "Shadow Circuit opposition packet",
        Summary: "Reusable opposition packet keeps the next run bind grounded on governed roster truth.",
        BindingSummary: "Reusable across the campaign so the next scene can bind real opposition truth without local shadow packet models.",
        Reusable: true,
        SearchTerms:
        [
            "opposition",
            "scene",
            "security roster",
            "checkpoint"
        ],
        EvidenceLines:
        [
            "Checkpoint opposition packet with lead pressure and matrix overwatch already wired.",
            "Campaign return can reopen through the same governed packet."
        ],
        UpdatedAtUtc: DateTimeOffset.UtcNow),
    new GovernedPrepLaunchProjection(
        LaunchId: "prep-launch-shadow-circuit",
        WorkspaceId: "workspace-shadow-circuit",
        CampaignId: "campaign-shadow",
        PacketId: "opposition:workspace-shadow-circuit",
        PacketKind: "opposition_packet",
        PacketTitle: "Shadow Circuit opposition packet",
        TargetRunId: "run-shadow-circuit",
        TargetRunTitle: "Shadow Circuit",
        TargetSceneId: "scene-lab-breach",
        TargetSceneTitle: "Lab breach",
        InitiatedByUserId: "user-operator",
        Summary: "Bound the governed opposition packet into the active Shadow Circuit run without local shadow prep notes.",
        AuditLines:
        [
            "Launch receipt keeps the active bind auditable."
        ],
        LaunchedAtUtc: DateTimeOffset.UtcNow));
Assert(string.Equals(governedPrepPacketPlan.PacketRequest.Title, "Shadow Circuit opposition packet", StringComparison.Ordinal), "Governed prep packet planner should reuse the governed packet title.");
Assert(string.Equals(governedPrepPacketPlan.NextAction, "refresh_binding_posture", StringComparison.Ordinal), "Launched governed prep packets should route into binding-posture refresh next.");
Assert(governedPrepPacketPlan.AttachmentBatch.Attachments.Count >= 5, "Governed prep packet planner should attach campaign, workspace, packet, run/scene, and launch receipt targets.");
Assert(governedPrepPacketPlan.PacketRequest.References?.Contains("workspace-shadow-circuit", StringComparer.Ordinal) == true, "Governed prep packet planner should keep the workspace reference.");
Assert(governedPrepPacketPlan.PacketRequest.References?.Contains("campaign-shadow", StringComparer.Ordinal) == true, "Governed prep packet planner should keep the governed campaign reference.");
Assert(governedPrepPacketPlan.PacketRequest.References?.Contains("prep-launch-shadow-circuit", StringComparer.Ordinal) == true, "Governed prep packet planner should keep the prep-launch receipt reference.");
Assert(governedPrepPacketPlan.PacketRequest.References?.Contains("scene-lab-breach", StringComparer.Ordinal) == true, "Governed prep packet planner should keep the bound scene reference.");
Assert(governedPrepPacketPlan.EvidenceLines.Any(static line => line.Contains("Binding:", StringComparison.Ordinal)), "Governed prep packet planner should surface binding posture.");
Assert(governedPrepPacketPlan.EvidenceLines.Any(static line => line.Contains("Search terms: opposition, scene, security roster, checkpoint", StringComparison.Ordinal)), "Governed prep packet planner should preserve searchable prep semantics.");
Assert(governedPrepPacketPlan.EvidenceLines.Any(static line => line.Contains("Launch: Bound the governed opposition packet", StringComparison.Ordinal)), "Governed prep packet planner should preserve launch summary evidence.");
Assert(governedPrepPacketPlan.EvidenceLines.Any(static line => line.Contains("Audit: Launch receipt keeps the active bind auditable.", StringComparison.Ordinal)), "Governed prep packet planner should preserve launch audit evidence.");
Assert(governedPrepPacketPlan.EvidenceLines.Any(static line => line.Contains("Bound target: Shadow Circuit / Lab breach", StringComparison.Ordinal)), "Governed prep packet planner should keep the explicit campaign binding target.");

var reusablePrepPacketPlan = prepPackets.BuildPlan(
    new GovernedPrepPacketProjection(
        WorkspaceId: "workspace-shadow-circuit",
        CampaignId: "campaign-shadow",
        PacketId: "scene:workspace-shadow-circuit",
        Kind: "scene_packet",
        Title: "Shadow Circuit scene packet",
        Summary: "Reusable scene packet is searchable and ready for the next governed bind.",
        BindingSummary: "Scene packet stays reusable until a governed launch receipt binds it to the next run lane.",
        Reusable: true,
        SearchTerms:
        [
            "scene",
            "safehouse",
            "opposition"
        ],
        EvidenceLines:
        [
            "Scene packet stays ready for the next governed bind."
        ],
        UpdatedAtUtc: DateTimeOffset.UtcNow));
Assert(string.Equals(reusablePrepPacketPlan.NextAction, "launch_governed_packet", StringComparison.Ordinal), "Reusable prep packets without a launch should route into governed launch next.");
Assert(reusablePrepPacketPlan.EvidenceLines.Any(static line => line.Contains("Launch posture: No governed launch receipt is attached yet", StringComparison.Ordinal)), "Reusable prep packets should keep no-launch posture explicit.");
Assert(reusablePrepPacketPlan.AttachmentBatch.Attachments.Any(static attachment => attachment.TargetId == "scene:workspace-shadow-circuit"), "Reusable prep packets should still attach the governed packet identity.");

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
