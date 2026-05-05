using Chummer.Media.Contracts;
using Chummer.Run.AI.Services.Assets;

var assets = new AssetLifecycleService();
var jobs = new MediaRenderJobService(assets);
var presenter = new ExplainPresenterSiblingRenderingService(jobs);

var request = CreateBaseRequest();
var receipt = await presenter.RenderAsync(request);

Assert(receipt.Artifacts.Count == 2, "Explain presenter rendering should receipt each requested sibling.");
Assert(receipt.AudioReceiptIds.Count == 1, "Explain presenter audio receipt id is required.");
Assert(receipt.PresenterReceiptIds.Count == 1, "Explain presenter presenter receipt id is required.");
Assert(receipt.JobIds.Count == 2, "Explain presenter receipt should expose every media job id directly.");
Assert(receipt.CompanionRefs.Count == 2, "Each explain presenter sibling should publish a stable ref.");
Assert(receipt.CompanionReadyRefs.Count == 2, "Each explain presenter sibling should publish a structured ready ref.");
Assert(receipt.CompanionRefReceipts.Count == 2, "Each explain presenter sibling should publish a first-class companion ref receipt row.");
Assert(!string.IsNullOrWhiteSpace(receipt.FirstPartyTextFallback), "Explain presenter receipt must preserve first-party text fallback.");
Assert(!string.IsNullOrWhiteSpace(receipt.TextFallbackReceipt.ReceiptId), "Explain presenter text fallback must publish a receipt id.");
Assert(receipt.TextFallbackReceipt.GroundingScopeRef == "build-lab.total-essence", "Explain presenter text fallback should preserve the grounding scope ref.");
Assert(receipt.Artifacts.All(static artifact => artifact.JobState == MediaRenderJobState.Succeeded), "Explain presenter receipts must wait for completed media jobs.");
Assert(receipt.CompanionReadyRefs.All(static row => !string.IsNullOrWhiteSpace(row.Ref) && !string.IsNullOrWhiteSpace(row.ReceiptId) && !string.IsNullOrWhiteSpace(row.JobId) && !string.IsNullOrWhiteSpace(row.AssetId) && !string.IsNullOrWhiteSpace(row.AssetUrl)), "Explain presenter ready refs must preserve ref, receipt, job, asset id, and asset url.");
Assert(receipt.CompanionRefReceipts.Any(static row => row.Role == ExplainPresenterSiblingArtifactRole.Audio && row.CaptionRefs.Count == 1 && row.PreviewRefs.Count == 0), "Explain presenter audio companion ref receipts must preserve caption refs without inventing preview refs.");
Assert(receipt.CompanionReadyRefs.Any(static row => row.Role == ExplainPresenterSiblingArtifactRole.PresenterVideo && row.PreviewRefs.Count == 1), "Explain presenter presenter ready refs must preserve preview refs.");
Assert(receipt.RoleReceiptGroups.Count == 2, "Each explain presenter sibling role should publish a first-class receipt group.");
Assert(receipt.RoleReceiptGroups.All(static group => group.JobIds.Count >= 1 && group.CompanionRefs.Count >= 1 && group.ArtifactReceipts.Count >= 1), "Explain presenter role receipt groups must preserve aggregate job ids, grouped companion refs, and grouped artifact rows.");
Assert(receipt.RoleReceiptGroups.All(static group => group.ArtifactReceipts.All(artifact => !string.IsNullOrWhiteSpace(artifact.AssetUrl) && artifact.ApprovalState is not null && artifact.RetentionState is not null && artifact.StorageClass is not null)), "Explain presenter role receipt groups must preserve grouped artifact asset urls and lifecycle truth.");
Assert(receipt.CaptionRefReceipts.Count == 1, "Explain presenter caption refs should publish first-class grouped receipt rows.");
Assert(receipt.CaptionRefReceipts[0].ReceiptIds.Count == 2, "Shared explain presenter caption ref should point at audio and presenter receipts.");
Assert(receipt.CaptionRefReceipts[0].ArtifactReceipts.All(static artifact => !string.IsNullOrWhiteSpace(artifact.AssetUrl) && artifact.ApprovalState is not null && artifact.RetentionState is not null && artifact.StorageClass is not null), "Explain presenter caption receipt rows must preserve grouped artifact asset urls and lifecycle truth.");
Assert(receipt.PreviewRefReceipts.Count == 1, "Explain presenter preview refs should publish first-class grouped receipt rows.");
Assert(receipt.PreviewRefReceipts[0].ReceiptIds.Count == 1, "Explain presenter preview ref should point only at presenter receipts.");
Assert(receipt.PreviewRefReceipts[0].ArtifactReceipts.All(static artifact => !string.IsNullOrWhiteSpace(artifact.AssetUrl) && artifact.ApprovalState is not null && artifact.RetentionState is not null && artifact.StorageClass is not null), "Explain presenter preview receipt rows must preserve grouped artifact asset urls and lifecycle truth.");

foreach (var artifact in receipt.Artifacts)
{
    await WaitForSucceededJobAsync(jobs, artifact.JobId);
}

var replayed = await presenter.RenderAsync(request);
Assert(
    receipt.Artifacts.Select(static artifact => artifact.JobId).SequenceEqual(replayed.Artifacts.Select(static artifact => artifact.JobId)),
    "Replay-safe dedupe should keep explain presenter sibling jobs stable.");
Assert(receipt.RenderedAtUtc == replayed.RenderedAtUtc, "Replay-safe dedupe should keep explain presenter rendered timestamps stable.");
Assert(receipt.TextFallbackReceipt.ReceiptId == replayed.TextFallbackReceipt.ReceiptId, "Explain presenter text fallback receipts should stay stable when callers replay the same request.");

var delayedReplay = await presenter.RenderAsync(request with
{
    Source = "explain-presenter-smoke-updated-source",
    RequestedAtUtc = request.RequestedAtUtc.AddHours(4)
});
Assert(
    receipt.Artifacts.Select(static artifact => artifact.JobId).SequenceEqual(delayedReplay.Artifacts.Select(static artifact => artifact.JobId)),
    "Explain presenter sibling job ids should stay stable when only source and requested timestamps drift.");
Assert(
    receipt.RenderedAtUtc == delayedReplay.RenderedAtUtc,
    "Explain presenter sibling rendered timestamps should stay stable when only source and requested timestamps drift.");
Assert(
    receipt.TextFallbackReceipt.ReceiptId == delayedReplay.TextFallbackReceipt.ReceiptId,
    "Explain presenter text fallback receipts should stay stable when only source and requested timestamps drift.");

var whitespaceReplay = await presenter.RenderAsync(request with
{
    RenderingId = "  explain-presenter-render-001  ",
    ApprovedExplanationPacketId = "  approved-explanation-packet-001  ",
    ExplanationPacketRevisionId = "  explanation-revision-9  ",
    GroundingScopeRef = "  build-lab.total-essence  ",
    Source = "  explain-presenter-smoke  ",
    FirstPartyTextFallback = "  Essence is reduced by ware that consumes living body capacity.  "
});
Assert(
    receipt.Artifacts.Select(static artifact => artifact.JobId).SequenceEqual(whitespaceReplay.Artifacts.Select(static artifact => artifact.JobId)),
    "Explain presenter sibling job ids should stay stable when only top-level request whitespace changes.");
Assert(
    receipt.TextFallbackReceipt.ReceiptId == whitespaceReplay.TextFallbackReceipt.ReceiptId,
    "Explain presenter text fallback receipts should stay stable when only top-level request whitespace changes.");

var reorderedReplay = await presenter.RenderAsync(request with
{
    Artifacts =
    [
        request.Artifacts[1],
        request.Artifacts[0]
    ]
});
Assert(
    receipt.Artifacts.Select(static artifact => artifact.JobId).SequenceEqual(reorderedReplay.Artifacts.Select(static artifact => artifact.JobId)),
    "Explain presenter sibling job ids should stay stable when approved explanation packet artifacts reorder.");
Assert(
    receipt.Artifacts.Select(static artifact => artifact.ReceiptId).SequenceEqual(reorderedReplay.Artifacts.Select(static artifact => artifact.ReceiptId)),
    "Explain presenter sibling receipt ids should stay stable when approved explanation packet artifacts reorder.");
Assert(
    receipt.CompanionReadyRefs.Select(static row => row.ReceiptId).SequenceEqual(reorderedReplay.CompanionReadyRefs.Select(static row => row.ReceiptId)),
    "Explain presenter ready refs should stay stable when approved explanation packet artifacts reorder.");
Assert(
    receipt.TextFallbackReceipt.ReceiptId == reorderedReplay.TextFallbackReceipt.ReceiptId,
    "Explain presenter text fallback receipts should stay stable when approved explanation packet artifacts reorder.");

var mixedCaseRefs = await presenter.RenderAsync(request with
{
    RenderingId = "explain-presenter-mixed-case-ref-normalization",
    Artifacts =
    [
        request.Artifacts[0] with
        {
            CaptionRefs =
            [
                "caption://packet-001/en-us.shared.vtt",
                "Caption://packet-001/EN-US.shared.vtt"
            ]
        },
        request.Artifacts[1] with
        {
            CaptionRefs =
            [
                "Caption://packet-001/EN-US.shared.vtt",
                "caption://packet-001/en-us.shared.vtt"
            ],
            PreviewRefs =
            [
                "preview://packet-001/presenter-shared",
                "Preview://packet-001/PRESENTER-SHARED"
            ]
        }
    ]
});
var mixedCaseRefsReordered = await presenter.RenderAsync(request with
{
    RenderingId = "explain-presenter-mixed-case-ref-normalization",
    Artifacts =
    [
        request.Artifacts[1] with
        {
            CaptionRefs =
            [
                "caption://packet-001/en-us.shared.vtt",
                "Caption://packet-001/EN-US.shared.vtt"
            ],
            PreviewRefs =
            [
                "Preview://packet-001/PRESENTER-SHARED",
                "preview://packet-001/presenter-shared"
            ]
        },
        request.Artifacts[0] with
        {
            CaptionRefs =
            [
                "Caption://packet-001/EN-US.shared.vtt",
                "caption://packet-001/en-us.shared.vtt"
            ]
        }
    ]
});
Assert(
    mixedCaseRefs.Artifacts.Select(static artifact => artifact.ReceiptId).SequenceEqual(mixedCaseRefsReordered.Artifacts.Select(static artifact => artifact.ReceiptId)),
    "Mixed-case explain presenter caption and preview duplicates should keep sibling receipt ids stable when callers reorder the same refs.");
Assert(
    mixedCaseRefs.CaptionRefs.SequenceEqual(mixedCaseRefsReordered.CaptionRefs),
    "Mixed-case explain presenter caption duplicates should keep aggregate caption refs stable when callers reorder the same refs.");
Assert(
    mixedCaseRefs.PreviewRefs.SequenceEqual(mixedCaseRefsReordered.PreviewRefs),
    "Mixed-case explain presenter preview duplicates should keep aggregate preview refs stable when callers reorder the same refs.");
Assert(
    mixedCaseRefs.CaptionRefReceipts.Select(static row => row.Ref).SequenceEqual(mixedCaseRefsReordered.CaptionRefReceipts.Select(static row => row.Ref)),
    "Mixed-case explain presenter caption receipt rows should keep canonical ref casing stable when callers reorder the same refs.");
Assert(
    mixedCaseRefs.PreviewRefReceipts.Select(static row => row.Ref).SequenceEqual(mixedCaseRefsReordered.PreviewRefReceipts.Select(static row => row.Ref)),
    "Mixed-case explain presenter preview receipt rows should keep canonical ref casing stable when callers reorder the same refs.");

var collisionReceipt = await presenter.RenderAsync(request with
{
    RenderingId = "explain-presenter-render-collision-proof",
    Artifacts =
    [
        .. request.Artifacts,
        request.Artifacts[1] with
        {
            OutputFormat = "mov",
            CompanionRef = "explain-presenter://packet-001/presenter-deluxe",
            CaptionRefs = ["caption://packet-001/en-US.presenter.vtt"],
            PreviewRefs = ["preview://packet-001/presenter-deluxe"],
            DeduplicationKey = "packet-001-presenter"
        }
    ]
});
var collidingPresenterJobs = collisionReceipt.Artifacts
    .Where(static artifact => artifact.Role == ExplainPresenterSiblingArtifactRole.PresenterVideo)
    .Select(static artifact => artifact.JobId)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();
Assert(collidingPresenterJobs.Length == 2, "Different explain presenter output refs must not collapse onto one sibling render job when request dedupe keys collide.");

var delimiterReceiptCollision = await presenter.RenderAsync(request with
{
    RenderingId = "explain-presenter-render-receipt-collision-proof",
    Artifacts =
    [
        request.Artifacts[0],
        request.Artifacts[1] with
        {
            OutputFormat = "webm",
            CompanionRef = "explain-presenter://packet-001/receipt-delimiter/a",
            CaptionRefs = ["caption", "variant|one"],
            PreviewRefs = ["preview://packet-001/receipt/card-a"],
            DeduplicationKey = "packet-001-presenter-a"
        },
        request.Artifacts[1] with
        {
            OutputFormat = "avi",
            CompanionRef = "explain-presenter://packet-001/receipt-delimiter/b",
            CaptionRefs = ["caption|variant", "one"],
            PreviewRefs = ["preview://packet-001/receipt/card-b"],
            DeduplicationKey = "packet-001-presenter-b"
        }
    ]
});
var delimiterReceiptIds = delimiterReceiptCollision.Artifacts
    .Where(static artifact => artifact.CompanionRef.StartsWith("explain-presenter://packet-001/receipt-delimiter/", StringComparison.OrdinalIgnoreCase))
    .Select(static artifact => artifact.ReceiptId)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();
Assert(delimiterReceiptIds.Length == 2, "Delimiter-heavy explain presenter caption refs must not collapse onto one receipt id.");

try
{
    await presenter.RenderAsync(request with
    {
        RenderingId = "explain-presenter-duplicate-companion-ref",
        Artifacts =
        [
            request.Artifacts[0],
            request.Artifacts[1] with { CompanionRef = request.Artifacts[0].CompanionRef }
        ]
    });
    throw new InvalidOperationException("Duplicate explain presenter companion ref validation did not fail.");
}
catch (ArgumentException ex) when (ex.Message.Contains("must be unique", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await presenter.RenderAsync(request with
    {
        RenderingId = "explain-presenter-null-artifact-entry",
        Artifacts =
        [
            request.Artifacts[0],
            null!
        ]
    });
    throw new InvalidOperationException("Null explain presenter artifact entry validation did not fail.");
}
catch (ArgumentException ex) when (ex.Message.Contains("artifacts[1] is required", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await presenter.RenderAsync(request with
    {
        RenderingId = "explain-presenter-null-artifact-list",
        Artifacts = null!
    });
    throw new InvalidOperationException("Null explain presenter artifact list validation did not fail.");
}
catch (ArgumentNullException ex) when (string.Equals(ex.ParamName, "Artifacts", StringComparison.Ordinal))
{
}

try
{
    await presenter.RenderAsync(request with
    {
        RenderingId = "explain-presenter-missing-approved-packet-scope",
        Artifacts =
        [
            request.Artifacts[0] with
            {
                Payload = "{\"approvedExplanationPacketId\":\"wrong-packet\",\"explanationPacketRevisionId\":\"explanation-revision-9\",\"groundingScopeRef\":\"build-lab.total-essence\"}"
            },
            request.Artifacts[1]
        ]
    });
    throw new InvalidOperationException("Explain presenter payload packet scope validation did not fail.");
}
catch (ArgumentException ex) when (ex.Message.Contains("approved explanation packet id", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await presenter.RenderAsync(request with
    {
        RenderingId = "explain-presenter-text-scope-near-miss",
        Artifacts =
        [
            request.Artifacts[0] with
            {
                Payload = "approvedExplanationPacketId=approved-explanation-packet-001-shadow explanationPacketRevisionId=explanation-revision-9-shadow groundingScopeRef=build-lab.total-essence-shadow note=approved-explanation-packet-001 explanation-revision-9 build-lab.total-essence"
            },
            request.Artifacts[1]
        ]
    });
    throw new InvalidOperationException("Explain presenter text payload near-miss scope validation did not fail.");
}
catch (ArgumentException ex) when (ex.Message.Contains("approved explanation packet id", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await presenter.RenderAsync(request with
    {
        RenderingId = "explain-presenter-text-revision-near-miss",
        Artifacts =
        [
            request.Artifacts[0] with
            {
                Payload = "approvedExplanationPacketId=approved-explanation-packet-001 explanationPacketRevisionId=explanation-revision-9-shadow groundingScopeRef=build-lab.total-essence note=approved-explanation-packet-001 explanation-revision-9 build-lab.total-essence"
            },
            request.Artifacts[1]
        ]
    });
    throw new InvalidOperationException("Explain presenter text payload revision near-miss validation did not fail.");
}
catch (ArgumentException ex) when (ex.Message.Contains("revision id", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await presenter.RenderAsync(request with
    {
        RenderingId = "explain-presenter-text-grounding-near-miss",
        Artifacts =
        [
            request.Artifacts[0] with
            {
                Payload = "approvedExplanationPacketId=approved-explanation-packet-001 explanationPacketRevisionId=explanation-revision-9 groundingScopeRef=build-lab.total-essence-shadow note=approved-explanation-packet-001 explanation-revision-9 build-lab.total-essence"
            },
            request.Artifacts[1]
        ]
    });
    throw new InvalidOperationException("Explain presenter text payload grounding near-miss validation did not fail.");
}
catch (ArgumentException ex) when (ex.Message.Contains("grounding scope ref", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await presenter.RenderAsync(request with
    {
        RenderingId = "explain-presenter-missing-revision-scope",
        Artifacts =
        [
            request.Artifacts[0] with
            {
                Payload = "{\"approvedExplanationPacketId\":\"approved-explanation-packet-001\",\"explanationPacketRevisionId\":\"wrong-revision\",\"groundingScopeRef\":\"build-lab.total-essence\"}"
            },
            request.Artifacts[1]
        ]
    });
    throw new InvalidOperationException("Explain presenter payload revision scope validation did not fail.");
}
catch (ArgumentException ex) when (ex.Message.Contains("revision id", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await presenter.RenderAsync(request with
    {
        RenderingId = "explain-presenter-missing-grounding-scope",
        Artifacts =
        [
            request.Artifacts[0] with
            {
                Payload = "{\"approvedExplanationPacketId\":\"approved-explanation-packet-001\",\"explanationPacketRevisionId\":\"explanation-revision-9\",\"groundingScopeRef\":\"wrong-scope\"}"
            },
            request.Artifacts[1]
        ]
    });
    throw new InvalidOperationException("Explain presenter payload grounding scope validation did not fail.");
}
catch (ArgumentException ex) when (ex.Message.Contains("grounding scope ref", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await presenter.RenderAsync(request with
    {
        RenderingId = "explain-presenter-json-scope-spoof",
        Artifacts =
        [
            request.Artifacts[0] with
            {
                Payload = "{\"approvedExplanationPacketId\":\"wrong-packet\",\"explanationPacketRevisionId\":\"wrong-revision\",\"groundingScopeRef\":\"wrong-scope\",\"note\":\"approved-explanation-packet-001 explanation-revision-9 build-lab.total-essence\"}"
            },
            request.Artifacts[1]
        ]
    });
    throw new InvalidOperationException("Explain presenter JSON scope spoof validation did not fail.");
}
catch (ArgumentException ex) when (ex.Message.Contains("approved explanation packet id", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await presenter.RenderAsync(request with
    {
        RenderingId = "explain-presenter-json-missing-scope-fields",
        Artifacts =
        [
            request.Artifacts[0] with
            {
                Payload = "{\"note\":\"approved-explanation-packet-001 explanation-revision-9 build-lab.total-essence\"}"
            },
            request.Artifacts[1]
        ]
    });
    throw new InvalidOperationException("Explain presenter JSON payloads without required scope fields should fail closed instead of falling back to substring matching.");
}
catch (ArgumentException ex) when (ex.Message.Contains("approved explanation packet id", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await presenter.RenderAsync(request with
    {
        RenderingId = "explain-presenter-json-array-scope-spoof",
        Artifacts =
        [
            request.Artifacts[0] with
            {
                Payload = "[\"approved-explanation-packet-001\",\"explanation-revision-9\",\"build-lab.total-essence\"]"
            },
            request.Artifacts[1]
        ]
    });
    throw new InvalidOperationException("Explain presenter JSON array payloads without required scope fields should fail closed instead of falling back to substring matching.");
}
catch (ArgumentException ex) when (ex.Message.Contains("approved explanation packet id", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await presenter.RenderAsync(request with
    {
        RenderingId = "explain-presenter-json-string-scope-spoof",
        Artifacts =
        [
            request.Artifacts[0] with
            {
                Payload = "\"approved-explanation-packet-001 explanation-revision-9 build-lab.total-essence\""
            },
            request.Artifacts[1]
        ]
    });
    throw new InvalidOperationException("Explain presenter JSON string payloads without required scope fields should fail closed instead of falling back to substring matching.");
}
catch (ArgumentException ex) when (ex.Message.Contains("approved explanation packet id", StringComparison.OrdinalIgnoreCase))
{
}

var nonJsonPayloadReceipt = await presenter.RenderAsync(request with
{
    RenderingId = "explain-presenter-non-json-scope-fallback",
    Artifacts =
    [
        request.Artifacts[0] with
        {
            Payload = "approvedExplanationPacketId=approved-explanation-packet-001 explanationPacketRevisionId=explanation-revision-9 groundingScopeRef=build-lab.total-essence artifact=audio-text"
        },
        request.Artifacts[1] with
        {
            Payload = "packet_id=approved-explanation-packet-001 packet_revision_id=explanation-revision-9 value_ref=build-lab.total-essence artifact=presenter-text"
        }
    ]
});
Assert(nonJsonPayloadReceipt.Artifacts.Count == 2, "Non-JSON explain presenter payloads should still render when they carry the approved packet scope text.");

var paddedJsonScopeReceipt = await presenter.RenderAsync(request with
{
    RenderingId = "explain-presenter-padded-json-scope",
    Artifacts =
    [
        request.Artifacts[0] with
        {
            Payload = "{\"approvedExplanationPacketId\":\"  approved-explanation-packet-001  \",\"explanationPacketRevisionId\":\"  explanation-revision-9  \",\"groundingScopeRef\":\"  build-lab.total-essence  \"}"
        },
        request.Artifacts[1] with
        {
            Payload = "{\"packet_id\":\"  approved-explanation-packet-001  \",\"packet_revision_id\":\"  explanation-revision-9  \",\"value_ref\":\"  build-lab.total-essence  \"}"
        }
    ]
});
Assert(paddedJsonScopeReceipt.Artifacts.Count == 2, "JSON explain presenter payloads should trim surrounding whitespace on approved packet scope values.");

var paddedTextScopeReceipt = await presenter.RenderAsync(request with
{
    RenderingId = "explain-presenter-padded-text-scope",
    Artifacts =
    [
        request.Artifacts[0] with
        {
            Payload = "approvedExplanationPacketId=\"  approved-explanation-packet-001  \" explanationPacketRevisionId=\"  explanation-revision-9  \" groundingScopeRef=\"  build-lab.total-essence  \""
        },
        request.Artifacts[1] with
        {
            Payload = "packet_id='  approved-explanation-packet-001  ' packet_revision_id='  explanation-revision-9  ' value_ref='  build-lab.total-essence  '"
        }
    ]
});
Assert(paddedTextScopeReceipt.Artifacts.Count == 2, "Keyed text explain presenter payloads should trim surrounding whitespace on approved packet scope values.");

var fallbackChanged = await presenter.RenderAsync(request with
{
    RenderingId = "explain-presenter-fallback-changed",
    FirstPartyTextFallback = "Essence changes here are still shown in the explain drawer if media is unavailable."
});
Assert(receipt.TextFallbackReceipt.ReceiptId != fallbackChanged.TextFallbackReceipt.ReceiptId, "Explain presenter text fallback receipts should change when first-party fallback text changes.");

ExplainPresenterSiblingRenderRequest CreateBaseRequest() =>
    new(
        RenderingId: "explain-presenter-render-001",
        ApprovedExplanationPacketId: "approved-explanation-packet-001",
        ExplanationPacketRevisionId: "explanation-revision-9",
        GroundingScopeRef: "build-lab.total-essence",
        Source: "explain-presenter-smoke",
        RequestedAtUtc: DateTimeOffset.UtcNow,
        FirstPartyTextFallback: "Essence is reduced by ware that consumes living body capacity.",
        Artifacts:
        [
            new ExplainPresenterSiblingArtifactRenderRequest(
                Role: ExplainPresenterSiblingArtifactRole.Audio,
                Category: "explain-presenter/audio",
                Payload: "{\"approvedExplanationPacketId\":\"approved-explanation-packet-001\",\"explanationPacketRevisionId\":\"explanation-revision-9\",\"groundingScopeRef\":\"build-lab.total-essence\",\"artifact\":\"audio\"}",
                OutputFormat: "mp3",
                CompanionRef: "explain-presenter://packet-001/audio",
                CaptionRefs: ["caption://packet-001/en-US.vtt"],
                PreviewRefs: [],
                DeduplicationKey: "packet-001-audio",
                CacheTtl: TimeSpan.FromMinutes(10),
                MaxBytes: 4096),
            new ExplainPresenterSiblingArtifactRenderRequest(
                Role: ExplainPresenterSiblingArtifactRole.PresenterVideo,
                Category: "explain-presenter/presenter-video",
                Payload: "{\"approvedExplanationPacketId\":\"approved-explanation-packet-001\",\"explanationPacketRevisionId\":\"explanation-revision-9\",\"groundingScopeRef\":\"build-lab.total-essence\",\"artifact\":\"presenter\"}",
                OutputFormat: "mp4",
                CompanionRef: "explain-presenter://packet-001/presenter",
                CaptionRefs: ["caption://packet-001/en-US.vtt"],
                PreviewRefs: ["preview://packet-001/card"],
                DeduplicationKey: "packet-001-presenter",
                CacheTtl: TimeSpan.FromMinutes(10),
                MaxBytes: 4096)
        ]);

static async Task WaitForSucceededJobAsync(MediaRenderJobService jobs, string jobId)
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

    throw new InvalidOperationException($"Timed out waiting for explain presenter job {jobId}.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
