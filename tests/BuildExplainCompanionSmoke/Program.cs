using Chummer.Media.Contracts;
using Chummer.Run.AI.Services.Assets;

var assets = new AssetLifecycleService();
var jobs = new MediaRenderJobService(assets);
var explain = new BuildExplainCompanionRenderingService(jobs);

var request = new BuildExplainCompanionRenderRequest(
    RenderingId: "build-explain-render-001",
    ApprovedExplainPacketId: "approved-build-explain-packet-001",
    ExplainPacketRevisionId: "build-explain-revision-7",
    Source: "build-explain-companion-smoke",
    RequestedAtUtc: DateTimeOffset.UtcNow,
    Artifacts:
    [
        new BuildExplainCompanionArtifactRenderRequest(
            Role: BuildExplainCompanionArtifactRole.Video,
            Category: "build-explain/companion/video",
            Payload: "{\"approvedExplainPacketId\":\"approved-build-explain-packet-001\",\"explainPacketRevisionId\":\"build-explain-revision-7\",\"artifact\":\"video\"}",
            OutputFormat: "mp4",
            CompanionRef: "build-explain://packet-001/video",
            CaptionRefs: ["caption://packet-001/en-US.vtt"],
            PreviewRefs: ["preview://packet-001/card"],
            DeduplicationKey: "packet-001-video",
            CacheTtl: TimeSpan.FromMinutes(10),
            MaxBytes: 4096),
        new BuildExplainCompanionArtifactRenderRequest(
            Role: BuildExplainCompanionArtifactRole.Audio,
            Category: "build-explain/companion/audio",
            Payload: "{\"approvedExplainPacketId\":\"approved-build-explain-packet-001\",\"explainPacketRevisionId\":\"build-explain-revision-7\",\"artifact\":\"audio\"}",
            OutputFormat: "mp3",
            CompanionRef: "build-explain://packet-001/audio",
            CaptionRefs: ["caption://packet-001/en-US.vtt"],
            PreviewRefs: [],
            DeduplicationKey: "packet-001-audio",
            CacheTtl: TimeSpan.FromMinutes(10),
            MaxBytes: 4096),
        new BuildExplainCompanionArtifactRenderRequest(
            Role: BuildExplainCompanionArtifactRole.PreviewCard,
            Category: "build-explain/companion/preview-card",
            Payload: "{\"approvedExplainPacketId\":\"approved-build-explain-packet-001\",\"explainPacketRevisionId\":\"build-explain-revision-7\",\"artifact\":\"preview-card\"}",
            OutputFormat: "png",
            CompanionRef: "build-explain://packet-001/preview-card",
            CaptionRefs: [],
            PreviewRefs: ["preview://packet-001/card"],
            DeduplicationKey: "packet-001-preview-card",
            CacheTtl: TimeSpan.FromMinutes(10),
            MaxBytes: 4096),
        new BuildExplainCompanionArtifactRenderRequest(
            Role: BuildExplainCompanionArtifactRole.PacketCompanion,
            Category: "build-explain/companion/packet",
            Payload: "{\"approvedExplainPacketId\":\"approved-build-explain-packet-001\",\"explainPacketRevisionId\":\"build-explain-revision-7\",\"artifact\":\"packet\"}",
            OutputFormat: "zip",
            CompanionRef: "build-explain://packet-001/packet",
            CaptionRefs: [],
            PreviewRefs: ["preview://packet-001/card"],
            DeduplicationKey: "packet-001-packet",
            CacheTtl: TimeSpan.FromMinutes(10),
            MaxBytes: 4096)
    ]);

var receipt = await explain.RenderAsync(request);
Assert(receipt.Artifacts.Count == 4, "Build explain companion rendering should receipt each requested sibling.");
Assert(receipt.VideoReceiptIds.Count == 1, "Build explain video receipt id is required.");
Assert(receipt.AudioReceiptIds.Count == 1, "Build explain audio receipt id is required.");
Assert(receipt.PreviewCardReceiptIds.Count == 1, "Build explain preview-card receipt id is required.");
Assert(receipt.PacketCompanionReceiptIds.Count == 1, "Build explain packet companion receipt id is required.");
Assert(receipt.JobIds.Count == 4, "Build explain bundle receipt should expose every media job id directly.");
Assert(receipt.CompanionRefs.Count == 4, "Each companion sibling should publish a stable ref.");
Assert(receipt.CompanionReadyRefs.Count == 4, "Each companion sibling should publish a structured ready ref.");
Assert(receipt.CompanionRefReceipts.Count == 4, "Each companion sibling should publish a first-class companion ref receipt row.");
Assert(receipt.Artifacts.All(static artifact => artifact.JobState == MediaRenderJobState.Succeeded), "Build explain companion receipts must wait for completed media jobs.");
Assert(receipt.Artifacts.All(static artifact => !string.IsNullOrWhiteSpace(artifact.AssetId)), "Build explain receipts must preserve concrete asset ids.");
Assert(receipt.Artifacts.All(static artifact => !string.IsNullOrWhiteSpace(artifact.AssetUrl)), "Build explain receipts must preserve concrete asset urls.");
Assert(receipt.Artifacts.All(static artifact => artifact.ApprovalState == AssetApprovalState.Approved), "Build explain receipts must preserve asset approval truth.");
Assert(receipt.Artifacts.All(static artifact => artifact.RetentionState == AssetRetentionState.CacheOnly), "Build explain receipts must preserve asset retention truth.");
Assert(receipt.Artifacts.All(static artifact => artifact.StorageClass == AssetStorageClass.ObjectStorage), "Build explain receipts must preserve asset storage truth.");
Assert(receipt.CompanionReadyRefs.All(static row => !string.IsNullOrWhiteSpace(row.Ref) && !string.IsNullOrWhiteSpace(row.ReceiptId) && !string.IsNullOrWhiteSpace(row.JobId) && !string.IsNullOrWhiteSpace(row.AssetId) && !string.IsNullOrWhiteSpace(row.AssetUrl)), "Build explain ready refs must preserve ref, receipt, job, asset id, and asset url.");
Assert(receipt.CompanionRefReceipts.All(static row => !string.IsNullOrWhiteSpace(row.Ref) && !string.IsNullOrWhiteSpace(row.ReceiptId) && !string.IsNullOrWhiteSpace(row.JobId) && !string.IsNullOrWhiteSpace(row.AssetId) && !string.IsNullOrWhiteSpace(row.AssetUrl)), "Build explain companion ref receipts must preserve ref, receipt, job, asset id, and asset url.");
Assert(receipt.CompanionReadyRefs.Any(static row => row.Role == BuildExplainCompanionArtifactRole.Video && row.CaptionRefs.Count == 1 && row.PreviewRefs.Count == 1), "Build explain video ready refs must preserve caption and preview refs.");
Assert(receipt.CompanionReadyRefs.Any(static row => row.Role == BuildExplainCompanionArtifactRole.PacketCompanion && row.PreviewRefs.Count == 1), "Build explain packet ready refs must preserve preview refs.");
Assert(receipt.CompanionRefReceipts.Any(static row => row.Role == BuildExplainCompanionArtifactRole.Audio && row.CaptionRefs.Count == 1 && row.PreviewRefs.Count == 0), "Build explain audio companion ref receipts must preserve caption refs without inventing preview refs.");
Assert(receipt.RoleReceiptGroups.Count == 4, "Each build explain sibling role should publish a first-class receipt group.");
Assert(receipt.RoleReceiptGroups.All(static group => group.ReceiptIds.Count > 0 && group.JobIds.Count > 0 && group.CompanionRefs.Count > 0 && group.ArtifactReceipts.Count > 0), "Build explain role receipt groups must preserve receipt, job, ref, and artifact rows.");
Assert(receipt.RoleReceiptGroups.Any(static group => group.Role == BuildExplainCompanionArtifactRole.Audio && group.CaptionRefs.Count == 1 && group.PreviewRefs.Count == 0), "Build explain audio role group must preserve caption refs without inventing preview refs.");
Assert(receipt.CaptionRefReceipts.Count == 1, "Build explain caption refs should publish first-class grouped receipt rows.");
Assert(receipt.CaptionRefReceipts[0].ReceiptIds.Count == 2, "Shared build explain caption ref should point at video and audio receipts.");
Assert(receipt.CaptionRefReceipts[0].JobIds.Count == 2, "Build explain caption ref receipts must preserve aggregate job ids.");
Assert(receipt.CaptionRefReceipts[0].CompanionRefs.Count == 2, "Build explain caption ref receipts must preserve grouped companion refs.");
Assert(receipt.CaptionRefReceipts[0].ArtifactReceipts.Count == 2, "Build explain caption ref receipts must preserve grouped artifact rows.");
Assert(receipt.CaptionRefReceipts[0].ArtifactReceipts.All(static artifact => artifact.CaptionRefs.Count == 1 && !string.IsNullOrWhiteSpace(artifact.CompanionRef)), "Build explain caption ref grouped artifacts must preserve caption refs and companion refs.");
Assert(receipt.PreviewRefReceipts.Count == 1, "Build explain preview refs should publish first-class grouped receipt rows.");
Assert(receipt.PreviewRefReceipts[0].ReceiptIds.Count == 3, "Shared build explain preview ref should point at video, preview-card, and packet receipts.");
Assert(receipt.PreviewRefReceipts[0].JobIds.Count == 3, "Build explain preview ref receipts must preserve aggregate job ids.");
Assert(receipt.PreviewRefReceipts[0].CompanionRefs.Count == 3, "Build explain preview ref receipts must preserve grouped companion refs.");
Assert(receipt.PreviewRefReceipts[0].ArtifactReceipts.Count == 3, "Build explain preview ref receipts must preserve grouped artifact rows.");
Assert(receipt.PreviewRefReceipts[0].ArtifactReceipts.All(static artifact => artifact.PreviewRefs.Count == 1 && !string.IsNullOrWhiteSpace(artifact.AssetUrl)), "Build explain preview ref grouped artifacts must preserve preview refs and asset urls.");
Assert(receipt.RoleReceiptGroups.Any(static group => group.Role == BuildExplainCompanionArtifactRole.Video && group.JobIds.Count == 1 && group.CompanionRefs.Count == 1 && group.ArtifactReceipts.Count == 1), "Build explain video role groups must preserve exact aggregate receipt, job, and artifact linkage.");

foreach (var artifact in receipt.Artifacts)
{
    await WaitForSucceededJobAsync(jobs, artifact.JobId);
}

var replayed = await explain.RenderAsync(request);
Assert(
    receipt.Artifacts.Select(static artifact => artifact.JobId).SequenceEqual(replayed.Artifacts.Select(static artifact => artifact.JobId)),
    "Replay-safe dedupe should keep build explain companion jobs stable.");
Assert(receipt.RenderedAtUtc == replayed.RenderedAtUtc, "Replay-safe dedupe should keep build explain rendered timestamps stable.");

var delayedReplay = await explain.RenderAsync(request with
{
    RequestedAtUtc = request.RequestedAtUtc.AddHours(6)
});
Assert(
    receipt.Artifacts.Select(static artifact => artifact.JobId).SequenceEqual(delayedReplay.Artifacts.Select(static artifact => artifact.JobId)),
    "Replay-safe dedupe should keep build explain companion jobs stable even when callers retry later.");
Assert(
    receipt.RenderedAtUtc == delayedReplay.RenderedAtUtc,
    "Replay-safe dedupe should not let later request timestamps rewrite build explain rendered timestamps.");

var collisionReceipt = await explain.RenderAsync(request with
{
    RenderingId = "build-explain-render-collision-proof",
    Artifacts =
    [
        .. request.Artifacts,
        request.Artifacts[0] with
        {
            OutputFormat = "webm",
            CompanionRef = "build-explain://packet-001/video-web",
            CaptionRefs = ["caption://packet-001/en-US.web.vtt"],
            PreviewRefs = ["preview://packet-001/web-card"],
            DeduplicationKey = "packet-001-video"
        }
    ]
});
var collidingVideoJobs = collisionReceipt.Artifacts
    .Where(static artifact => artifact.Role == BuildExplainCompanionArtifactRole.Video)
    .Select(static artifact => artifact.JobId)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();
Assert(collidingVideoJobs.Length == 2, "Different build explain output refs must not collapse onto one companion render job when request dedupe keys collide.");

var receiptDelimiterCollision = await explain.RenderAsync(request with
{
    RenderingId = "build-explain-render-receipt-collision-proof",
    Artifacts =
    [
        .. request.Artifacts,
        request.Artifacts[0] with
        {
            OutputFormat = "mov",
            CompanionRef = "build-explain://packet-001/receipt-delimiter/a",
            CaptionRefs = ["caption", "variant|one"],
            PreviewRefs = ["preview://packet-001/receipt/card-a"],
            DeduplicationKey = "packet-001-video-receipt-a"
        },
        request.Artifacts[0] with
        {
            OutputFormat = "avi",
            CompanionRef = "build-explain://packet-001/receipt-delimiter/b",
            CaptionRefs = ["caption|variant", "one"],
            PreviewRefs = ["preview://packet-001/receipt/card-b"],
            DeduplicationKey = "packet-001-video-receipt-b"
        }
    ]
});
var delimiterReceiptIds = receiptDelimiterCollision.Artifacts
    .Where(static artifact => artifact.CompanionRef.StartsWith("build-explain://packet-001/receipt-delimiter/", StringComparison.OrdinalIgnoreCase))
    .Select(static artifact => artifact.ReceiptId)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();
Assert(delimiterReceiptIds.Length == 2, "Delimiter-heavy build explain caption refs must not collapse onto one receipt id.");

try
{
    await explain.RenderAsync(request with
    {
        RenderingId = "build-explain-duplicate-companion-ref",
        Artifacts =
        [
            request.Artifacts[0],
            request.Artifacts[1] with { CompanionRef = request.Artifacts[0].CompanionRef },
            request.Artifacts[2],
            request.Artifacts[3]
        ]
    });
    throw new InvalidOperationException("Duplicate build explain companion ref validation did not fail.");
}
catch (ArgumentException ex) when (ex.Message.Contains("must be unique", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await explain.RenderAsync(request with
    {
        RenderingId = "build-explain-duplicate-companion-ref-normalized",
        Artifacts =
        [
            request.Artifacts[0] with { CompanionRef = " build-explain://packet-001/video " },
            request.Artifacts[1],
            request.Artifacts[2],
            request.Artifacts[3] with { CompanionRef = "BUILD-EXPLAIN://PACKET-001/VIDEO" }
        ]
    });
    throw new InvalidOperationException("Case-insensitive or padded build explain companion ref validation did not fail.");
}
catch (ArgumentException ex) when (ex.Message.Contains("must be unique", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await explain.RenderAsync(request with
    {
        RenderingId = "build-explain-null-artifact-entry",
        Artifacts =
        [
            request.Artifacts[0],
            null!,
            request.Artifacts[2],
            request.Artifacts[3]
        ]
    });
    throw new InvalidOperationException("Null build explain artifact entry validation did not fail.");
}
catch (ArgumentException ex) when (ex.Message.Contains("artifacts[1] is required", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await explain.RenderAsync(request with
    {
        RenderingId = "build-explain-null-artifact-list",
        Artifacts = null!
    });
    throw new InvalidOperationException("Null build explain artifact list validation did not fail.");
}
catch (ArgumentNullException ex) when (string.Equals(ex.ParamName, "Artifacts", StringComparison.Ordinal))
{
}

try
{
    await explain.RenderAsync(request with
    {
        RenderingId = "build-explain-missing-approved-packet-scope",
        Artifacts =
        [
            request.Artifacts[0] with
            {
                Payload = "{\"approvedExplainPacketId\":\"wrong-packet\",\"explainPacketRevisionId\":\"build-explain-revision-7\",\"artifact\":\"video\"}"
            },
            request.Artifacts[1],
            request.Artifacts[2],
            request.Artifacts[3]
        ]
    });
    throw new InvalidOperationException("Build explain payload packet scope validation did not fail.");
}
catch (ArgumentException ex) when (ex.Message.Contains("approved explain packet id", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await explain.RenderAsync(request with
    {
        RenderingId = "build-explain-text-scope-near-miss",
        Artifacts =
        [
            request.Artifacts[0] with
            {
                Payload = "approvedExplainPacketId=approved-build-explain-packet-001-shadow explainPacketRevisionId=build-explain-revision-7-shadow note=approved-build-explain-packet-001 build-explain-revision-7"
            },
            request.Artifacts[1],
            request.Artifacts[2],
            request.Artifacts[3]
        ]
    });
    throw new InvalidOperationException("Build explain text payload near-miss scope validation did not fail.");
}
catch (ArgumentException ex) when (ex.Message.Contains("approved explain packet id", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await explain.RenderAsync(request with
    {
        RenderingId = "build-explain-missing-revision-scope",
        Artifacts =
        [
            request.Artifacts[0] with
            {
                Payload = "{\"approvedExplainPacketId\":\"approved-build-explain-packet-001\",\"explainPacketRevisionId\":\"wrong-revision\",\"artifact\":\"video\"}"
            },
            request.Artifacts[1],
            request.Artifacts[2],
            request.Artifacts[3]
        ]
    });
    throw new InvalidOperationException("Build explain payload revision scope validation did not fail.");
}
catch (ArgumentException ex) when (ex.Message.Contains("revision id", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await explain.RenderAsync(request with
    {
        RenderingId = "build-explain-json-scope-spoof",
        Artifacts =
        [
            request.Artifacts[0] with
            {
                Payload = "{\"approvedExplainPacketId\":\"wrong-packet\",\"explainPacketRevisionId\":\"wrong-revision\",\"note\":\"approved-build-explain-packet-001 build-explain-revision-7\"}"
            },
            request.Artifacts[1],
            request.Artifacts[2],
            request.Artifacts[3]
        ]
    });
    throw new InvalidOperationException("Build explain JSON scope spoof validation did not fail.");
}
catch (ArgumentException ex) when (ex.Message.Contains("approved explain packet id", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await explain.RenderAsync(request with
    {
        RenderingId = "build-explain-json-missing-scope-fields",
        Artifacts =
        [
            request.Artifacts[0] with
            {
                Payload = "{\"note\":\"approved-build-explain-packet-001 build-explain-revision-7\"}"
            },
            request.Artifacts[1],
            request.Artifacts[2],
            request.Artifacts[3]
        ]
    });
    throw new InvalidOperationException("Build explain JSON payloads without required scope fields should fail closed instead of falling back to substring matching.");
}
catch (ArgumentException ex) when (ex.Message.Contains("approved explain packet id", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await explain.RenderAsync(request with
    {
        RenderingId = "build-explain-json-array-scope-spoof",
        Artifacts =
        [
            request.Artifacts[0] with
            {
                Payload = "[\"approved-build-explain-packet-001\",\"build-explain-revision-7\",\"video-array-note\"]"
            },
            request.Artifacts[1],
            request.Artifacts[2],
            request.Artifacts[3]
        ]
    });
    throw new InvalidOperationException("Build explain JSON array payloads without required scope fields should fail closed instead of falling back to substring matching.");
}
catch (ArgumentException ex) when (ex.Message.Contains("approved explain packet id", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await explain.RenderAsync(request with
    {
        RenderingId = "build-explain-json-string-scope-spoof",
        Artifacts =
        [
            request.Artifacts[0] with
            {
                Payload = "\"approved-build-explain-packet-001 build-explain-revision-7 video-string-note\""
            },
            request.Artifacts[1],
            request.Artifacts[2],
            request.Artifacts[3]
        ]
    });
    throw new InvalidOperationException("Build explain JSON string payloads without required scope fields should fail closed instead of falling back to substring matching.");
}
catch (ArgumentException ex) when (ex.Message.Contains("approved explain packet id", StringComparison.OrdinalIgnoreCase))
{
}

var nonJsonPayloadReceipt = await explain.RenderAsync(request with
{
    RenderingId = "build-explain-non-json-scope-fallback",
    Artifacts =
    [
        request.Artifacts[0] with
        {
            Payload = "approvedExplainPacketId=approved-build-explain-packet-001 explainPacketRevisionId=build-explain-revision-7 artifact=video-text"
        },
        request.Artifacts[1] with
        {
            Payload = "approvedExplainPacketId=approved-build-explain-packet-001 explainPacketRevisionId=build-explain-revision-7 artifact=audio-text"
        },
        request.Artifacts[2] with
        {
            Payload = "approvedExplainPacketId=approved-build-explain-packet-001 explainPacketRevisionId=build-explain-revision-7 artifact=preview-text"
        },
        request.Artifacts[3] with
        {
            Payload = "approvedExplainPacketId=approved-build-explain-packet-001 explainPacketRevisionId=build-explain-revision-7 artifact=packet-text"
        }
    ]
});
Assert(nonJsonPayloadReceipt.Artifacts.Count == 4, "Non-JSON build explain payloads should still render when they carry the approved packet scope text.");

var paddedJsonScopeReceipt = await explain.RenderAsync(request with
{
    RenderingId = "build-explain-padded-json-scope",
    Artifacts =
    [
        request.Artifacts[0] with
        {
            Payload = "{\"approvedExplainPacketId\":\"  approved-build-explain-packet-001  \",\"explainPacketRevisionId\":\"  build-explain-revision-7  \",\"artifact\":\"video-json-padded\"}"
        },
        request.Artifacts[1] with
        {
            Payload = "{\"approvedExplainPacketId\":\"  approved-build-explain-packet-001  \",\"explainPacketRevisionId\":\"  build-explain-revision-7  \",\"artifact\":\"audio-json-padded\"}"
        },
        request.Artifacts[2] with
        {
            Payload = "{\"approvedExplainPacketId\":\"  approved-build-explain-packet-001  \",\"explainPacketRevisionId\":\"  build-explain-revision-7  \",\"artifact\":\"preview-json-padded\"}"
        },
        request.Artifacts[3] with
        {
            Payload = "{\"approvedExplainPacketId\":\"  approved-build-explain-packet-001  \",\"explainPacketRevisionId\":\"  build-explain-revision-7  \",\"artifact\":\"packet-json-padded\"}"
        }
    ]
});
Assert(paddedJsonScopeReceipt.Artifacts.Count == 4, "JSON build explain payloads should trim surrounding whitespace on approved packet scope values.");

var paddedTextScopeReceipt = await explain.RenderAsync(request with
{
    RenderingId = "build-explain-padded-text-scope",
    Artifacts =
    [
        request.Artifacts[0] with
        {
            Payload = "approvedExplainPacketId=\"  approved-build-explain-packet-001  \" explainPacketRevisionId='  build-explain-revision-7  ' artifact=video-text-padded"
        },
        request.Artifacts[1] with
        {
            Payload = "approvedExplainPacketId=\"  approved-build-explain-packet-001  \" explainPacketRevisionId='  build-explain-revision-7  ' artifact=audio-text-padded"
        },
        request.Artifacts[2] with
        {
            Payload = "approvedExplainPacketId=\"  approved-build-explain-packet-001  \" explainPacketRevisionId='  build-explain-revision-7  ' artifact=preview-text-padded"
        },
        request.Artifacts[3] with
        {
            Payload = "approvedExplainPacketId=\"  approved-build-explain-packet-001  \" explainPacketRevisionId='  build-explain-revision-7  ' artifact=packet-text-padded"
        }
    ]
});
Assert(paddedTextScopeReceipt.Artifacts.Count == 4, "Keyed text build explain payloads should trim surrounding whitespace on approved packet scope values.");

var mixedCaseRefs = await explain.RenderAsync(request with
{
    RenderingId = "build-explain-mixed-case-ref-normalization",
    Artifacts =
    [
        request.Artifacts[0] with
        {
            CaptionRefs = ["caption://packet-001/EN-us.vtt", "Caption://packet-001/en-US.vtt"],
            PreviewRefs = ["preview://packet-001/Card", "Preview://packet-001/card"]
        },
        request.Artifacts[1] with
        {
            CaptionRefs = ["caption://packet-001/en-US.vtt", "CAPTION://packet-001/EN-us.vtt"]
        },
        request.Artifacts[2] with
        {
            PreviewRefs = ["preview://packet-001/card", "PREVIEW://packet-001/CARD"]
        },
        request.Artifacts[3] with
        {
            PreviewRefs = ["Preview://packet-001/card", "preview://packet-001/CARD"]
        }
    ]
});
var mixedCaseRefsReordered = await explain.RenderAsync((request with
{
    RenderingId = " build-explain-mixed-case-ref-normalization ",
    Artifacts =
    [
        request.Artifacts[3] with
        {
            PreviewRefs = ["preview://packet-001/CARD", "Preview://packet-001/card"]
        },
        request.Artifacts[2] with
        {
            PreviewRefs = ["PREVIEW://packet-001/CARD", "preview://packet-001/card"]
        },
        request.Artifacts[1] with
        {
            CaptionRefs = ["CAPTION://packet-001/EN-us.vtt", "caption://packet-001/en-US.vtt"]
        },
        request.Artifacts[0] with
        {
            CaptionRefs = ["Caption://packet-001/en-US.vtt", "caption://packet-001/EN-us.vtt"],
            PreviewRefs = ["Preview://packet-001/card", "preview://packet-001/Card"]
        }
    ]
}) with
{
    ApprovedExplainPacketId = " approved-build-explain-packet-001 ",
    ExplainPacketRevisionId = " build-explain-revision-7 ",
    Source = " build-explain-companion-smoke "
});
Assert(
    mixedCaseRefs.CaptionRefs.SequenceEqual(mixedCaseRefsReordered.CaptionRefs),
    "Mixed-case caption refs should keep canonical ref casing stable when callers reorder the same refs.");
Assert(
    mixedCaseRefs.PreviewRefs.SequenceEqual(mixedCaseRefsReordered.PreviewRefs),
    "Mixed-case preview refs should keep canonical ref casing stable when callers reorder the same refs.");
Assert(
    mixedCaseRefs.CaptionRefReceipts.Select(static row => row.Ref).SequenceEqual(
        mixedCaseRefsReordered.CaptionRefReceipts.Select(static row => row.Ref)),
    "Mixed-case caption ref receipt rows should keep canonical ref casing stable when callers reorder the same refs.");
Assert(
    mixedCaseRefs.PreviewRefReceipts.Select(static row => row.Ref).SequenceEqual(
        mixedCaseRefsReordered.PreviewRefReceipts.Select(static row => row.Ref)),
    "Mixed-case preview ref receipt rows should keep canonical ref casing stable when callers reorder the same refs.");

var paddedRefs = await explain.RenderAsync(request with
{
    Artifacts =
    [
        request.Artifacts[0] with
        {
            CaptionRefs = ["  caption://packet-001/en-US.vtt  "],
            PreviewRefs = ["  preview://packet-001/card  "]
        },
        request.Artifacts[1] with
        {
            CaptionRefs = ["  caption://packet-001/en-US.vtt  "]
        },
        request.Artifacts[2] with
        {
            PreviewRefs = ["  preview://packet-001/card  "]
        },
        request.Artifacts[3] with
        {
            PreviewRefs = ["  preview://packet-001/card  "]
        }
    ]
});
Assert(
    receipt.CaptionRefs.SequenceEqual(paddedRefs.CaptionRefs),
    "Build explain caption refs should trim surrounding whitespace before grouped receipts emit.");
Assert(
    receipt.PreviewRefs.SequenceEqual(paddedRefs.PreviewRefs),
    "Build explain preview refs should trim surrounding whitespace before grouped receipts emit.");
Assert(
    receipt.CaptionRefReceipts.Select(static row => row.Ref).SequenceEqual(
        paddedRefs.CaptionRefReceipts.Select(static row => row.Ref)),
    "Build explain caption ref receipt rows should trim surrounding whitespace before grouped receipts emit.");
Assert(
    receipt.PreviewRefReceipts.Select(static row => row.Ref).SequenceEqual(
        paddedRefs.PreviewRefReceipts.Select(static row => row.Ref)),
    "Build explain preview ref receipt rows should trim surrounding whitespace before grouped receipts emit.");
Assert(
    receipt.Artifacts.Select(static artifact => artifact.ReceiptId).SequenceEqual(
        paddedRefs.Artifacts.Select(static artifact => artifact.ReceiptId)),
    "Build explain receipt ids should stay stable when only caption and preview ref whitespace changes.");
Assert(
    receipt.CompanionReadyRefs.Select(static row => (
            row.Role,
            row.Ref,
            CaptionRefs: string.Join("|", row.CaptionRefs),
            PreviewRefs: string.Join("|", row.PreviewRefs)))
        .SequenceEqual(
            paddedRefs.CompanionReadyRefs.Select(static row => (
                row.Role,
                row.Ref,
                CaptionRefs: string.Join("|", row.CaptionRefs),
                PreviewRefs: string.Join("|", row.PreviewRefs)))),
    "Companion ready refs should stay stable when only caption and preview ref whitespace changes.");

var reordered = await explain.RenderAsync(request with
{
    Artifacts =
    [
        request.Artifacts[3],
        request.Artifacts[1],
        request.Artifacts[0],
        request.Artifacts[2]
    ]
});
Assert(
    receipt.VideoReceiptIds.SequenceEqual(reordered.VideoReceiptIds),
    "Video receipt ids should stay stable when callers reorder build explain siblings.");
Assert(
    receipt.AudioReceiptIds.SequenceEqual(reordered.AudioReceiptIds),
    "Audio receipt ids should stay stable when callers reorder build explain siblings.");
Assert(
    receipt.PreviewCardReceiptIds.SequenceEqual(reordered.PreviewCardReceiptIds),
    "Preview-card receipt ids should stay stable when callers reorder build explain siblings.");
Assert(
    receipt.PacketCompanionReceiptIds.SequenceEqual(reordered.PacketCompanionReceiptIds),
    "Packet companion receipt ids should stay stable when callers reorder build explain siblings.");
Assert(
    receipt.CompanionRefs.SequenceEqual(reordered.CompanionRefs),
    "Companion refs should stay stable when callers reorder build explain siblings.");
Assert(
    receipt.CaptionRefs.SequenceEqual(reordered.CaptionRefs),
    "Caption refs should stay stable when callers reorder build explain siblings.");
Assert(
    receipt.PreviewRefs.SequenceEqual(reordered.PreviewRefs),
    "Preview refs should stay stable when callers reorder build explain siblings.");
Assert(
    receipt.CompanionReadyRefs.Select(static row => (
            row.Role,
            row.Ref,
            row.OutputFormat,
            CaptionRefs: string.Join("|", row.CaptionRefs),
            PreviewRefs: string.Join("|", row.PreviewRefs)))
        .SequenceEqual(
            reordered.CompanionReadyRefs.Select(static row => (
                row.Role,
                row.Ref,
                row.OutputFormat,
                CaptionRefs: string.Join("|", row.CaptionRefs),
                PreviewRefs: string.Join("|", row.PreviewRefs)))),
    "Companion ready refs should stay stable when callers reorder build explain siblings.");
Assert(
    receipt.CompanionRefReceipts.Select(static row => (row.Role, row.Ref, row.OutputFormat)).SequenceEqual(
        reordered.CompanionRefReceipts.Select(static row => (row.Role, row.Ref, row.OutputFormat))),
    "Companion ref receipt rows should stay stable when callers reorder build explain siblings.");
Assert(
    receipt.RoleReceiptGroups.Select(static group => (
            group.Role,
            CompanionRefs: string.Join("|", group.CompanionRefs),
            CaptionRefs: string.Join("|", group.CaptionRefs),
            PreviewRefs: string.Join("|", group.PreviewRefs)))
        .SequenceEqual(
            reordered.RoleReceiptGroups.Select(static group => (
                group.Role,
                CompanionRefs: string.Join("|", group.CompanionRefs),
                CaptionRefs: string.Join("|", group.CaptionRefs),
                PreviewRefs: string.Join("|", group.PreviewRefs)))),
    "Role receipt groups should stay stable when callers reorder build explain siblings.");
Assert(
    receipt.CaptionRefReceipts.Select(static row => (
            row.Ref,
            CompanionRefs: string.Join("|", row.CompanionRefs),
            Roles: string.Join("|", row.Roles.OrderBy(static role => role)),
            ArtifactRoles: string.Join("|", row.ArtifactReceipts.Select(static artifact => artifact.Role).OrderBy(static role => role))))
        .SequenceEqual(
            reordered.CaptionRefReceipts.Select(static row => (
                row.Ref,
                CompanionRefs: string.Join("|", row.CompanionRefs),
                Roles: string.Join("|", row.Roles.OrderBy(static role => role)),
                ArtifactRoles: string.Join("|", row.ArtifactReceipts.Select(static artifact => artifact.Role).OrderBy(static role => role))))),
    "Caption ref receipt rows should stay stable when callers reorder build explain siblings.");
Assert(
    receipt.PreviewRefReceipts.Select(static row => (
            row.Ref,
            CompanionRefs: string.Join("|", row.CompanionRefs),
            Roles: string.Join("|", row.Roles.OrderBy(static role => role)),
            ArtifactRoles: string.Join("|", row.ArtifactReceipts.Select(static artifact => artifact.Role).OrderBy(static role => role))))
        .SequenceEqual(
            reordered.PreviewRefReceipts.Select(static row => (
                row.Ref,
                CompanionRefs: string.Join("|", row.CompanionRefs),
                Roles: string.Join("|", row.Roles.OrderBy(static role => role)),
                ArtifactRoles: string.Join("|", row.ArtifactReceipts.Select(static artifact => artifact.Role).OrderBy(static role => role))))),
    "Preview ref receipt rows should stay stable when callers reorder build explain siblings.");

var paddedRequest = await explain.RenderAsync(request with
{
    RenderingId = "  build-explain-render-001  ",
    ApprovedExplainPacketId = "  approved-build-explain-packet-001  ",
    ExplainPacketRevisionId = "  build-explain-revision-7  ",
    Source = "  build-explain-companion-smoke  "
});
Assert(paddedRequest.RenderingId == request.RenderingId, "Build explain rendering ids should normalize surrounding whitespace before receipts emit.");
Assert(paddedRequest.ApprovedExplainPacketId == request.ApprovedExplainPacketId, "Build explain approved packet ids should normalize surrounding whitespace before scope enforcement.");
Assert(paddedRequest.ExplainPacketRevisionId == request.ExplainPacketRevisionId, "Build explain revision ids should normalize surrounding whitespace before scope enforcement.");
Assert(paddedRequest.Source == request.Source, "Build explain source values should normalize surrounding whitespace before receipts emit.");
Assert(
    receipt.Artifacts.Select(static artifact => artifact.JobId).SequenceEqual(paddedRequest.Artifacts.Select(static artifact => artifact.JobId)),
    "Build explain companion job ids should stay stable when only top-level request whitespace changes.");
Assert(
    receipt.Artifacts.Select(static artifact => artifact.ReceiptId).SequenceEqual(paddedRequest.Artifacts.Select(static artifact => artifact.ReceiptId)),
    "Build explain receipt ids should stay stable when only top-level request whitespace changes.");
Assert(
    receipt.CompanionReadyRefs.Select(static row => (
            row.Role,
            row.Ref,
            row.ReceiptId,
            row.JobId))
        .SequenceEqual(
            paddedRequest.CompanionReadyRefs.Select(static row => (
                row.Role,
                row.Ref,
                row.ReceiptId,
                row.JobId))),
    "Companion ready refs should stay stable when only top-level request whitespace changes.");

var metadataDrifted = await explain.RenderAsync(request with
{
    Source = "build-explain-companion-smoke-replayed-from-another-surface",
    RequestedAtUtc = request.RequestedAtUtc.AddHours(6)
});
Assert(
    receipt.Artifacts.Select(static artifact => artifact.JobId).SequenceEqual(metadataDrifted.Artifacts.Select(static artifact => artifact.JobId)),
    "Build explain companion job ids should stay stable when only source and requested timestamps drift.");
Assert(
    receipt.Artifacts.Select(static artifact => artifact.ReceiptId).SequenceEqual(metadataDrifted.Artifacts.Select(static artifact => artifact.ReceiptId)),
    "Build explain receipt ids should stay stable when only source and requested timestamps drift.");
Assert(
    receipt.CompanionReadyRefs.Select(static row => (
            row.Role,
            row.Ref,
            row.ReceiptId,
            row.JobId,
            row.AssetUrl))
        .SequenceEqual(
            metadataDrifted.CompanionReadyRefs.Select(static row => (
                row.Role,
                row.Ref,
                row.ReceiptId,
                row.JobId,
                row.AssetUrl))),
    "Companion ready refs should stay stable when only source and requested timestamps drift.");
Assert(
    receipt.RoleReceiptGroups.Select(static group => (
            group.Role,
            ReceiptIds: string.Join("|", group.ReceiptIds),
            JobIds: string.Join("|", group.JobIds)))
        .SequenceEqual(
            metadataDrifted.RoleReceiptGroups.Select(static group => (
                group.Role,
                ReceiptIds: string.Join("|", group.ReceiptIds),
                JobIds: string.Join("|", group.JobIds)))),
    "Role receipt groups should stay stable when only source and requested timestamps drift.");

Console.WriteLine("Build explain companion smoke passed.");

static async Task WaitForSucceededJobAsync(IMediaRenderJobService jobs, string jobId)
{
    for (var attempt = 0; attempt < 50; attempt++)
    {
        var status = jobs.Get(jobId);
        if (status?.State == MediaRenderJobState.Succeeded)
        {
            return;
        }

        await Task.Delay(20);
    }

    throw new InvalidOperationException($"Job {jobId} did not reach succeeded state.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
