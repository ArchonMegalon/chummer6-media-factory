using Chummer.Media.Contracts;
using Chummer.Run.AI.Services.Assets;

var assets = new AssetLifecycleService();
var jobs = new MediaRenderJobService(assets);
var promo = new CreatorPromoKitRenderingService(jobs);

var request = new CreatorPromoKitRenderRequest(
    RenderingId: "creator-promo-render-001",
    ApprovedManifestId: "approved-creator-manifest-001",
    ManifestRevisionId: "creator-manifest-r7",
    Source: "creator-promo-kit-smoke",
    RequestedAtUtc: DateTimeOffset.UtcNow,
    Artifacts:
    [
        new CreatorPromoKitArtifactRenderRequest(
            Role: CreatorPromoKitArtifactRole.PromoVideo,
            Category: "creator-promo/video",
            Payload: "{\"approvedManifestId\":\"approved-creator-manifest-001\",\"manifestRevisionId\":\"creator-manifest-r7\",\"artifact\":\"video\"}",
            OutputFormat: "mp4",
            ArtifactRef: "creator-promo://manifest-001/video",
            CaptionRefs: ["caption://manifest-001/en-US.vtt"],
            PreviewRefs: ["preview://manifest-001/card"],
            DeduplicationKey: "manifest-001-video",
            CacheTtl: TimeSpan.FromMinutes(10),
            MaxBytes: 4096),
        new CreatorPromoKitArtifactRenderRequest(
            Role: CreatorPromoKitArtifactRole.PromoPoster,
            Category: "creator-promo/poster",
            Payload: "{\"approvedManifestId\":\"approved-creator-manifest-001\",\"manifestRevisionId\":\"creator-manifest-r7\",\"artifact\":\"poster\"}",
            OutputFormat: "png",
            ArtifactRef: "creator-promo://manifest-001/poster",
            CaptionRefs: [],
            PreviewRefs: ["preview://manifest-001/card"],
            DeduplicationKey: "manifest-001-poster",
            CacheTtl: TimeSpan.FromMinutes(10),
            MaxBytes: 4096),
        new CreatorPromoKitArtifactRenderRequest(
            Role: CreatorPromoKitArtifactRole.PreviewCard,
            Category: "creator-promo/preview-card",
            Payload: "{\"approvedManifestId\":\"approved-creator-manifest-001\",\"manifestRevisionId\":\"creator-manifest-r7\",\"artifact\":\"preview-card\"}",
            OutputFormat: "webp",
            ArtifactRef: "creator-promo://manifest-001/preview-card",
            CaptionRefs: [],
            PreviewRefs: ["preview://manifest-001/card"],
            DeduplicationKey: "manifest-001-preview-card",
            CacheTtl: TimeSpan.FromMinutes(10),
            MaxBytes: 4096)
    ]);

var receipt = await promo.RenderAsync(request);
Assert(receipt.Artifacts.Count == 3, "Creator promo kit rendering should receipt each required sibling.");
Assert(receipt.PromoVideoReceiptIds.Count == 1, "Creator promo video receipt id is required.");
Assert(receipt.PromoPosterReceiptIds.Count == 1, "Creator promo poster receipt id is required.");
Assert(receipt.PreviewCardReceiptIds.Count == 1, "Creator promo preview-card receipt id is required.");
Assert(receipt.JobIds.Count == 3, "Creator promo kit receipt should expose every media job id directly.");
Assert(receipt.ArtifactRefs.Count == 3, "Each creator promo sibling should publish a stable artifact ref.");
Assert(receipt.ReadyRefs.Count == 3, "Each creator promo sibling should publish a structured ready ref.");
Assert(receipt.ArtifactRefReceipts.Count == 3, "Each creator promo sibling should publish a first-class artifact ref receipt row.");
Assert(receipt.Artifacts.All(static artifact => artifact.JobState == MediaRenderJobState.Succeeded), "Creator promo kit receipts must wait for completed media jobs.");
Assert(receipt.Artifacts.All(static artifact => !string.IsNullOrWhiteSpace(artifact.AssetId)), "Creator promo kit receipts must preserve concrete asset ids.");
Assert(receipt.Artifacts.All(static artifact => !string.IsNullOrWhiteSpace(artifact.AssetUrl)), "Creator promo kit receipts must preserve concrete asset urls.");
Assert(receipt.Artifacts.All(static artifact => artifact.ApprovalState == AssetApprovalState.Approved), "Creator promo kit receipts must preserve asset approval truth.");
Assert(receipt.Artifacts.All(static artifact => artifact.RetentionState == AssetRetentionState.CacheOnly), "Creator promo kit receipts must preserve asset retention truth.");
Assert(receipt.Artifacts.All(static artifact => artifact.StorageClass == AssetStorageClass.ObjectStorage), "Creator promo kit receipts must preserve asset storage truth.");
Assert(receipt.ReadyRefs.All(static row => !string.IsNullOrWhiteSpace(row.Ref) && !string.IsNullOrWhiteSpace(row.ReceiptId) && !string.IsNullOrWhiteSpace(row.JobId) && !string.IsNullOrWhiteSpace(row.AssetId) && !string.IsNullOrWhiteSpace(row.AssetUrl)), "Creator promo ready refs must preserve ref, receipt, job, asset id, and asset url.");
Assert(receipt.ArtifactRefReceipts.All(static row => !string.IsNullOrWhiteSpace(row.Ref) && !string.IsNullOrWhiteSpace(row.ReceiptId) && !string.IsNullOrWhiteSpace(row.JobId) && !string.IsNullOrWhiteSpace(row.AssetId) && !string.IsNullOrWhiteSpace(row.AssetUrl)), "Creator promo artifact ref receipts must preserve ref, receipt, job, asset id, and asset url.");
Assert(receipt.RoleReceiptGroups.Count == 3, "Each creator promo sibling role should publish a first-class receipt group.");
Assert(receipt.RoleReceiptGroups.All(static group => group.ReceiptIds.Count > 0 && group.JobIds.Count > 0 && group.ArtifactRefs.Count > 0 && group.ArtifactReceipts.Count > 0), "Creator promo role receipt groups must preserve receipt, job, ref, and artifact rows.");
Assert(receipt.CaptionRefReceipts.Count == 1, "Creator promo captions should publish first-class grouped receipt rows.");
Assert(receipt.CaptionRefReceipts[0].ReceiptIds.Count == 1, "Shared creator promo caption ref should point at the promo video receipt.");
Assert(receipt.PreviewRefReceipts.Count == 1, "Creator promo preview refs should publish first-class grouped receipt rows.");
Assert(receipt.PreviewRefReceipts[0].ReceiptIds.Count == 3, "Shared creator promo preview ref should point at video, poster, and preview-card receipts.");

foreach (var artifact in receipt.Artifacts)
{
    await WaitForSucceededJobAsync(jobs, artifact.JobId);
}

var replayed = await promo.RenderAsync(request);
Assert(
    receipt.Artifacts.Select(static artifact => artifact.JobId).SequenceEqual(replayed.Artifacts.Select(static artifact => artifact.JobId)),
    "Replay-safe dedupe should keep creator promo kit jobs stable.");
Assert(receipt.RenderedAtUtc == replayed.RenderedAtUtc, "Replay-safe dedupe should keep creator promo rendered timestamps stable.");

var delayedReplay = await promo.RenderAsync(request with
{
    RequestedAtUtc = request.RequestedAtUtc.AddHours(6)
});
Assert(
    receipt.Artifacts.Select(static artifact => artifact.JobId).SequenceEqual(delayedReplay.Artifacts.Select(static artifact => artifact.JobId)),
    "Replay-safe dedupe should keep creator promo jobs stable even when callers retry later.");
Assert(
    receipt.RenderedAtUtc == delayedReplay.RenderedAtUtc,
    "Replay-safe dedupe should not let later request timestamps rewrite creator promo rendered timestamps.");

var reordered = await promo.RenderAsync(request with
{
    Artifacts =
    [
        request.Artifacts[2],
        request.Artifacts[0],
        request.Artifacts[1]
    ]
});
Assert(
    receipt.JobIds.SequenceEqual(reordered.JobIds),
    "Normalized sibling ordering should keep creator promo job ids stable when callers reorder the same approved manifest siblings.");
Assert(
    receipt.ReadyRefs.Select(static row => row.ReceiptId).SequenceEqual(reordered.ReadyRefs.Select(static row => row.ReceiptId)),
    "Normalized sibling ordering should keep creator promo ready ref receipt ids stable when callers reorder the same approved manifest siblings.");
Assert(
    receipt.ArtifactRefs.SequenceEqual(reordered.ArtifactRefs),
    "Normalized sibling ordering should keep creator promo artifact refs stable when callers reorder the same approved manifest siblings.");
Assert(
    receipt.CaptionRefReceipts.Select(static row => row.Ref).SequenceEqual(reordered.CaptionRefReceipts.Select(static row => row.Ref)),
    "Normalized sibling ordering should keep creator promo caption receipt rows stable when callers reorder the same approved manifest siblings.");
Assert(
    receipt.PreviewRefReceipts.Select(static row => row.Ref).SequenceEqual(reordered.PreviewRefReceipts.Select(static row => row.Ref)),
    "Normalized sibling ordering should keep creator promo preview receipt rows stable when callers reorder the same approved manifest siblings.");
Assert(
    receipt.RoleReceiptGroups.Select(static row => row.Role).SequenceEqual(reordered.RoleReceiptGroups.Select(static row => row.Role)),
    "Normalized sibling ordering should keep creator promo role receipt rows stable when callers reorder the same approved manifest siblings.");

var collisionReceipt = await promo.RenderAsync(request with
{
    RenderingId = "creator-promo-render-collision-proof",
    Artifacts =
    [
        .. request.Artifacts,
        request.Artifacts[0] with
        {
            OutputFormat = "webm",
            ArtifactRef = "creator-promo://manifest-001/video-web",
            CaptionRefs = ["caption://manifest-001/en-US.web.vtt"],
            PreviewRefs = ["preview://manifest-001/web-card"],
            DeduplicationKey = "manifest-001-video"
        }
    ]
});
var collidingVideoJobs = collisionReceipt.Artifacts
    .Where(static artifact => artifact.Role == CreatorPromoKitArtifactRole.PromoVideo)
    .Select(static artifact => artifact.JobId)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();
Assert(collidingVideoJobs.Length == 2, "Different creator promo output refs must not collapse onto one promo render job when request dedupe keys collide.");

var receiptDelimiterCollision = await promo.RenderAsync(request with
{
    RenderingId = "creator-promo-render-receipt-collision-proof",
    Artifacts =
    [
        request.Artifacts[0],
        request.Artifacts[1],
        request.Artifacts[2],
        request.Artifacts[0] with
        {
            OutputFormat = "mov",
            ArtifactRef = "creator-promo://manifest-001/receipt-delimiter/a",
            CaptionRefs = ["caption", "variant|one"],
            PreviewRefs = ["preview://manifest-001/receipt/card-a"],
            DeduplicationKey = "manifest-001-video-receipt-a"
        },
        request.Artifacts[0] with
        {
            OutputFormat = "avi",
            ArtifactRef = "creator-promo://manifest-001/receipt-delimiter/b",
            CaptionRefs = ["caption|variant", "one"],
            PreviewRefs = ["preview://manifest-001/receipt/card-b"],
            DeduplicationKey = "manifest-001-video-receipt-b"
        }
    ]
});
var delimiterReceiptIds = receiptDelimiterCollision.Artifacts
    .Where(static artifact => artifact.ArtifactRef.StartsWith("creator-promo://manifest-001/receipt-delimiter/", StringComparison.OrdinalIgnoreCase))
    .Select(static artifact => artifact.ReceiptId)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();
Assert(delimiterReceiptIds.Length == 2, "Delimiter-heavy creator promo caption refs must not collapse onto one receipt id.");

try
{
    await promo.RenderAsync(request with
    {
        RenderingId = "creator-promo-duplicate-artifact-ref",
        Artifacts =
        [
            request.Artifacts[0],
            request.Artifacts[1] with { ArtifactRef = request.Artifacts[0].ArtifactRef },
            request.Artifacts[2]
        ]
    });
    throw new InvalidOperationException("Duplicate creator promo artifact ref validation did not fail.");
}
catch (ArgumentException ex) when (ex.Message.Contains("must be unique", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await promo.RenderAsync(request with
    {
        RenderingId = "creator-promo-null-artifact-entry",
        Artifacts =
        [
            request.Artifacts[0],
            null!,
            request.Artifacts[2]
        ]
    });
    throw new InvalidOperationException("Null creator promo artifact entry validation did not fail.");
}
catch (ArgumentException ex) when (ex.Message.Contains("artifacts[1] is required", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await promo.RenderAsync(request with
    {
        RenderingId = "creator-promo-null-artifact-list",
        Artifacts = null!
    });
    throw new InvalidOperationException("Null creator promo artifact list validation did not fail.");
}
catch (ArgumentNullException ex) when (ex.ParamName == "Artifacts")
{
}

try
{
    await promo.RenderAsync(request with
    {
        RenderingId = "creator-promo-missing-approved-manifest-scope",
        Artifacts =
        [
            request.Artifacts[0] with { Payload = "{\"approvedManifestId\":\"wrong-manifest\",\"manifestRevisionId\":\"creator-manifest-r7\"}" },
            request.Artifacts[1],
            request.Artifacts[2]
        ]
    });
    throw new InvalidOperationException("Creator promo payload manifest scope validation did not fail.");
}
catch (ArgumentException ex) when (ex.Message.Contains("approved manifest id", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await promo.RenderAsync(request with
    {
        RenderingId = "creator-promo-missing-poster-preview-ref",
        Artifacts =
        [
            request.Artifacts[0],
            request.Artifacts[1] with { PreviewRefs = [] },
            request.Artifacts[2]
        ]
    });
    throw new InvalidOperationException("Poster-only missing preview ref validation did not fail.");
}
catch (ArgumentException ex) when (ex.Message.Contains("preview ref", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await promo.RenderAsync(request with
    {
        RenderingId = "creator-promo-missing-preview-card-preview-ref",
        Artifacts =
        [
            request.Artifacts[0],
            request.Artifacts[1],
            request.Artifacts[2] with { PreviewRefs = [] }
        ]
    });
    throw new InvalidOperationException("Preview-card-only missing preview ref validation did not fail.");
}
catch (ArgumentException ex) when (ex.Message.Contains("preview ref", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await promo.RenderAsync(request with
    {
        RenderingId = "creator-promo-missing-revision-scope",
        Artifacts =
        [
            request.Artifacts[0] with { Payload = "{\"approvedManifestId\":\"approved-creator-manifest-001\",\"manifestRevisionId\":\"wrong-revision\"}" },
            request.Artifacts[1],
            request.Artifacts[2]
        ]
    });
    throw new InvalidOperationException("Creator promo payload revision scope validation did not fail.");
}
catch (ArgumentException ex) when (ex.Message.Contains("manifest revision id", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await promo.RenderAsync(request with
    {
        RenderingId = "creator-promo-json-missing-scope-fields",
        Artifacts =
        [
            request.Artifacts[0] with { Payload = "{\"note\":\"missing scope\"}" },
            request.Artifacts[1],
            request.Artifacts[2]
        ]
    });
    throw new InvalidOperationException("Creator promo JSON payloads without required scope fields should fail closed instead of falling back to substring matching.");
}
catch (ArgumentException ex) when (ex.Message.Contains("approved manifest id", StringComparison.OrdinalIgnoreCase))
{
}

var textFallback = await promo.RenderAsync(request with
{
    RenderingId = "creator-promo-non-json-scope-fallback",
    Artifacts =
    [
        request.Artifacts[0] with { Payload = "approvedManifestId=approved-creator-manifest-001 manifestRevisionId=creator-manifest-r7 artifact=video" },
        request.Artifacts[1] with { Payload = "approvedManifestId=approved-creator-manifest-001 manifestRevisionId=creator-manifest-r7 artifact=poster" },
        request.Artifacts[2] with { Payload = "approvedManifestId=approved-creator-manifest-001 manifestRevisionId=creator-manifest-r7 artifact=preview-card" }
    ]
});
Assert(textFallback.JobIds.Count == 3, "Non-JSON creator promo payloads should still render when they carry the approved manifest scope text.");

var paddedPayloadScope = await promo.RenderAsync(request with
{
    // Scope parser coverage uses its own render identity because these payload bytes differ.
    RenderingId = "creator-promo-payload-scope-padding",
    ApprovedManifestId = "  approved-creator-manifest-001  ",
    ManifestRevisionId = "  creator-manifest-r7  ",
    Source = "  creator-promo-kit-smoke  ",
    Artifacts =
    [
        request.Artifacts[0] with
        {
            Payload = "{ \"approvedManifestId\": \"  approved-creator-manifest-001  \", \"manifestRevisionId\": \"  creator-manifest-r7  \" }",
            CaptionRefs = ["  caption://manifest-001/en-US.vtt  "],
            PreviewRefs = ["  preview://manifest-001/card  "]
        },
        request.Artifacts[1] with
        {
            Payload = "approvedManifestId=' approved-creator-manifest-001 ' manifestRevisionId=' creator-manifest-r7 '",
            PreviewRefs = ["  preview://manifest-001/card  "]
        },
        request.Artifacts[2] with
        {
            Payload = "approvedManifestId=' approved-creator-manifest-001 ' manifestRevisionId=' creator-manifest-r7 '",
            PreviewRefs = ["  preview://manifest-001/card  "]
        }
    ]
});
Assert(paddedPayloadScope.Artifacts.Count == 3, "Padded JSON and keyed text scope values should remain valid under a distinct render identity.");

var padded = await promo.RenderAsync(request with
{
    // creator-promo-padded-request keeps this whitespace-normalization proof easy to grep without changing render identity.
    RenderingId = "  creator-promo-render-001  ",
    ApprovedManifestId = "  approved-creator-manifest-001  ",
    ManifestRevisionId = "  creator-manifest-r7  ",
    Source = "  creator-promo-kit-smoke  ",
    Artifacts =
    [
        request.Artifacts[0] with
        {
            CaptionRefs = ["  caption://manifest-001/en-US.vtt  "],
            PreviewRefs = ["  preview://manifest-001/card  "]
        },
        request.Artifacts[1] with
        {
            PreviewRefs = ["  preview://manifest-001/card  "]
        },
        request.Artifacts[2] with
        {
            PreviewRefs = ["  preview://manifest-001/card  "]
        }
    ]
});
Assert(padded.RenderingId == request.RenderingId, "Creator promo rendering ids should normalize surrounding whitespace before receipts emit.");
Assert(padded.ApprovedManifestId == request.ApprovedManifestId, "Creator promo approved manifest ids should normalize surrounding whitespace before scope enforcement.");
Assert(padded.ManifestRevisionId == request.ManifestRevisionId, "Creator promo manifest revision ids should normalize surrounding whitespace before scope enforcement.");
Assert(padded.Source == request.Source, "Creator promo source values should normalize surrounding whitespace before receipts emit.");
Assert(receipt.JobIds.SequenceEqual(padded.JobIds), "Creator promo job ids should stay stable when only top-level request and scope whitespace changes.");
Assert(receipt.ReadyRefs.Select(static row => row.ReceiptId).SequenceEqual(padded.ReadyRefs.Select(static row => row.ReceiptId)), "Creator promo receipt ids should stay stable when only top-level request and scope whitespace changes.");

var caseFoldedRefs = await promo.RenderAsync(request with
{
    RenderingId = "creator-promo-case-folded-refs",
    Artifacts =
    [
        request.Artifacts[0] with
        {
            CaptionRefs = ["Caption://manifest-001/EN-US.vtt", " caption://manifest-001/en-us.vtt "],
            PreviewRefs = ["Preview://manifest-001/card", " preview://manifest-001/card "]
        },
        request.Artifacts[1] with
        {
            PreviewRefs = ["preview://manifest-001/card"]
        },
        request.Artifacts[2] with
        {
            PreviewRefs = ["PREVIEW://manifest-001/card"]
        }
    ]
});
Assert(caseFoldedRefs.CaptionRefs.Count == 1, "Mixed-case creator promo caption refs should dedupe into one canonical ref.");
Assert(caseFoldedRefs.PreviewRefs.Count == 1, "Mixed-case creator promo preview refs should dedupe into one canonical ref.");
Assert(caseFoldedRefs.CaptionRefReceipts.Count == 1, "Mixed-case creator promo caption refs should stay grouped under one receipt row.");
Assert(caseFoldedRefs.PreviewRefReceipts.Count == 1, "Mixed-case creator promo preview refs should stay grouped under one receipt row.");
Assert(caseFoldedRefs.PreviewRefReceipts[0].ReceiptIds.Count == 3, "Mixed-case creator promo preview refs should still point at video, poster, and preview-card receipts.");

// Caller attribution and request time may drift without changing the approved render.
var beforeMetadataReplay = jobs.ExportBackup();
var metadataReplay = await promo.RenderAsync(request with
{
    Source = "  creator-promo-kit-other-surface  ",
    RequestedAtUtc = request.RequestedAtUtc.AddHours(6)
});
Assert(metadataReplay.RequestedAtUtc == request.RequestedAtUtc.AddHours(6),
    "Metadata replay must retain the current request timestamp.");
Assert(metadataReplay.Source == "creator-promo-kit-other-surface" && metadataReplay.RenderedAtUtc == receipt.RenderedAtUtc,
    "Outer receipt attribution may change while render completion time remains stable.");
Assert(receipt.Artifacts.Select(row => (row.ReceiptId, row.JobId)).SequenceEqual(
    metadataReplay.Artifacts.Select(row => (row.ReceiptId, row.JobId))),
    "Metadata replay must reuse artifact receipt and job identities.");
Assert(jobs.ExportBackup().EnqueueCount == beforeMetadataReplay.EnqueueCount,
    "Metadata replay must not dispatch another render.");
foreach (var artifact in receipt.Artifacts)
{
    var job = jobs.ExportBackup().Jobs.Single(row => row.JobId == artifact.JobId);
    Assert(job.Source == job.DeduplicationKey && job.Source.StartsWith("creator-promo-kit:", StringComparison.Ordinal),
        "Job provenance must use the approved artifact scope.");
    Assert(assets.Resolve(artifact.AssetId!)?.Source == job.Source,
        "Stored asset provenance must match the reserved job scope.");
}

var beforeConflicts = jobs.ExportBackup();
var assetCountBeforeConflicts = assets.List().Count;
foreach (var changedArtifact in new[]
{
    request.Artifacts[0] with { Payload = request.Artifacts[0].Payload.Replace("{", "{ ") },
    request.Artifacts[0] with { RequiresApproval = !request.Artifacts[0].RequiresApproval },
    request.Artifacts[0] with { CacheTtl = TimeSpan.FromMinutes(20) }
})
{
    try
    {
        await promo.RenderAsync(request with
        {
            Artifacts = request.Artifacts.Select((artifact, index) => index == 0
                ? changedArtifact : artifact).ToArray()
        });
        throw new InvalidOperationException("Changed content or policy must conflict under the same artifact key.");
    }
    catch (MediaRenderJobConflictException conflict)
    {
        Assert(conflict.Code == "media-render-idempotency-conflict" &&
            receipt.Artifacts.Any(row => row.JobId == conflict.ExistingJobId),
            "Typed conflicts must identify the existing reserved artifact job.");
    }
}
Assert(jobs.ExportBackup().EnqueueCount == beforeConflicts.EnqueueCount && assets.List().Count == assetCountBeforeConflicts,
    "Conflicting requests must not dispatch or store another asset.");


static async Task WaitForSucceededJobAsync(IMediaRenderJobService jobs, string jobId)
{
    for (var attempt = 0; attempt < 100; attempt++)
    {
        var status = jobs.Get(jobId);
        if (status?.State == MediaRenderJobState.Succeeded)
        {
            return;
        }

        await Task.Delay(20);
    }

    throw new InvalidOperationException($"Timed out waiting for creator promo job {jobId} to finish.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
