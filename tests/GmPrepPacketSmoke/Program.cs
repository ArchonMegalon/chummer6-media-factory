using Chummer.Media.Contracts;
using Chummer.Run.AI.Services.Assets;

var assets = new AssetLifecycleService();
var jobs = new MediaRenderJobService(assets);
var packets = new GmPrepPacketBundleService(jobs);

var request = new GmPrepPacketRenderRequest(
    RenderingId: "gm-prep-render-001",
    GovernedSourcePackId: "governed-pack-001",
    SourcePackRevisionId: "governed-pack-rev-001",
    Source: "gm-prep-smoke",
    RequestedAtUtc: DateTimeOffset.UtcNow,
    Entries:
    [
        CreateEntry(
            GmPrepPacketSubjectKind.Opposition,
            "opposition-alpha",
            "packet://opposition/alpha",
            includeBriefing: true,
            packetCategory: "gm-prep/opposition/packet",
            previewCategory: "gm-prep/opposition/preview",
            briefingCategory: "gm-prep/opposition/briefing",
            packetOutputFormat: "pdf",
            previewOutputFormat: "png",
            briefingOutputFormat: "mp3",
            packetDeduplicationKey: "opposition-alpha-packet",
            previewDeduplicationKey: "opposition-alpha-preview",
            briefingDeduplicationKey: "opposition-alpha-briefing"),
        CreateEntry(
            GmPrepPacketSubjectKind.Scene,
            "scene-docks",
            "packet://scene/docks",
            includeBriefing: false,
            packetCategory: "gm-prep/scene/packet",
            previewCategory: "gm-prep/scene/preview",
            briefingCategory: null,
            packetOutputFormat: "pdf",
            previewOutputFormat: "png",
            briefingOutputFormat: null,
            packetDeduplicationKey: "scene-docks-packet",
            previewDeduplicationKey: "scene-docks-preview",
            briefingDeduplicationKey: null),
        CreateEntry(
            GmPrepPacketSubjectKind.PrepLibraryEntry,
            "prep-safehouse",
            "packet://prep/safehouse",
            includeBriefing: true,
            packetCategory: "gm-prep/library/packet",
            previewCategory: "gm-prep/library/preview",
            briefingCategory: "gm-prep/library/briefing",
            packetOutputFormat: "pdf",
            previewOutputFormat: "png",
            briefingOutputFormat: "wav",
            packetDeduplicationKey: "prep-safehouse-packet",
            previewDeduplicationKey: "prep-safehouse-preview",
            briefingDeduplicationKey: "prep-safehouse-briefing"),
    ]);

var receipt = await packets.RenderAsync(request);
var oppositionBriefing = request.Entries[0].Briefing ?? throw new InvalidOperationException("Opposition entry should include a briefing artifact.");
Assert(receipt.Artifacts.Count == 8, "GM prep packet rendering should receipt packet, preview, and optional briefing siblings.");
Assert(receipt.EntryReceipts.Count == 3, "Every GM prep entry should emit a first-class entry receipt.");
Assert(receipt.SubjectReceiptGroups.Count == 3, "Each GM prep subject kind should emit a receipt group.");
Assert(receipt.PacketReceiptIds.Count == 3, "Packet receipt ids should stay first-class.");
Assert(receipt.PreviewReceiptIds.Count == 3, "Preview receipt ids should stay first-class.");
Assert(receipt.BriefingReceiptIds.Count == 2, "Optional briefing receipts should emit only for entries that request them.");
Assert(receipt.OppositionPacketReceiptIds.Count == 1, "Opposition packet receipts should stay directly addressable.");
Assert(receipt.ScenePacketReceiptIds.Count == 1, "Scene packet receipts should stay directly addressable.");
Assert(receipt.PrepLibraryPacketReceiptIds.Count == 1, "Prep-library packet receipts should stay directly addressable.");
Assert(receipt.PacketRefs.Count == 3, "Packet refs should stay first-class across the bundle.");
Assert(receipt.JobIds.Count == 8, "GM prep packet bundle receipt should expose each artifact job id.");
Assert(receipt.Artifacts.All(static artifact => artifact.JobState == MediaRenderJobState.Succeeded), "GM prep packet receipts must wait for completed media jobs.");
Assert(receipt.Artifacts.All(static artifact => !string.IsNullOrWhiteSpace(artifact.AssetId)), "GM prep packet receipts must preserve concrete asset ids.");
Assert(receipt.Artifacts.All(static artifact => !string.IsNullOrWhiteSpace(artifact.AssetUrl)), "GM prep packet receipts must preserve concrete asset urls.");
Assert(receipt.EntryReceipts.Any(static entry => entry.SubjectKind == GmPrepPacketSubjectKind.Scene && entry.BriefingReceiptId is null), "Scene entries should allow optional briefing omission.");
Assert(receipt.SubjectReceiptGroups.Any(static group => group.SubjectKind == GmPrepPacketSubjectKind.Opposition && group.BriefingReceiptIds.Count == 1), "Opposition groups should preserve briefing receipt ids.");

foreach (var artifact in receipt.Artifacts)
{
    await WaitForSucceededJobAsync(jobs, artifact.JobId);
}

var replayed = await packets.RenderAsync(request);
Assert(
    receipt.Artifacts.Select(static artifact => artifact.JobId).SequenceEqual(replayed.Artifacts.Select(static artifact => artifact.JobId)),
    "Replay-safe dedupe should keep GM prep packet jobs stable.");
Assert(receipt.RenderedAtUtc == replayed.RenderedAtUtc, "Replay-safe dedupe should keep GM prep rendered timestamps stable.");

var whitespaceNormalized = await packets.RenderAsync(request with
{
    RenderingId = "  gm-prep-whitespace-normalized  ",
    GovernedSourcePackId = "  governed-pack-001  ",
    SourcePackRevisionId = "  governed-pack-rev-001  ",
    Source = "  gm-prep-smoke  "
});
var whitespaceBaseline = await packets.RenderAsync(request with
{
    RenderingId = "gm-prep-whitespace-normalized"
});
Assert(whitespaceNormalized.RenderingId == "gm-prep-whitespace-normalized", "GM prep rendering ids should normalize surrounding whitespace before receipts emit.");
Assert(whitespaceNormalized.GovernedSourcePackId == "governed-pack-001", "GM prep governed source pack ids should normalize surrounding whitespace before scope enforcement.");
Assert(whitespaceNormalized.SourcePackRevisionId == "governed-pack-rev-001", "GM prep source pack revision ids should normalize surrounding whitespace before scope enforcement.");
Assert(whitespaceNormalized.Source == "gm-prep-smoke", "GM prep source values should normalize surrounding whitespace before receipts emit.");
Assert(
    whitespaceNormalized.Artifacts.Select(static artifact => artifact.JobId).SequenceEqual(whitespaceBaseline.Artifacts.Select(static artifact => artifact.JobId)),
    "GM prep packet job ids should stay stable when only top-level request whitespace changes.");
Assert(
    whitespaceNormalized.Artifacts.Select(static artifact => artifact.ReceiptId).SequenceEqual(whitespaceBaseline.Artifacts.Select(static artifact => artifact.ReceiptId)),
    "GM prep packet receipt ids should stay stable when only top-level request whitespace changes.");
Assert(
    whitespaceNormalized.SubjectReceiptGroups.Select(static group => group.ReceiptId).SequenceEqual(whitespaceBaseline.SubjectReceiptGroups.Select(static group => group.ReceiptId)),
    "GM prep subject receipt groups should stay stable when only top-level request whitespace changes.");

var collisionReceipt = await packets.RenderAsync(request with
{
    RenderingId = "gm-prep-render-collision-proof",
    Entries =
    [
        request.Entries[0],
        request.Entries[0] with
        {
            SourceEntryId = "opposition-beta",
            PacketRef = "packet://opposition/beta",
            Packet = request.Entries[0].Packet with
            {
                Category = "gm-prep/opposition/packet:alt",
                OutputFormat = "pdf:alt",
                DeduplicationKey = "shared:key",
                Payload = request.Entries[0].Packet.Payload
                    .Replace("packet://opposition/alpha", "packet://opposition/beta", StringComparison.Ordinal)
                    .Replace("opposition-alpha", "opposition-beta", StringComparison.Ordinal)
            },
            Preview = request.Entries[0].Preview with
            {
                Category = "gm-prep/opposition/preview",
                OutputFormat = "png",
                DeduplicationKey = "01:shared:key",
                Payload = request.Entries[0].Preview.Payload
                    .Replace("packet://opposition/alpha", "packet://opposition/beta", StringComparison.Ordinal)
                    .Replace("opposition-alpha", "opposition-beta", StringComparison.Ordinal)
            },
            Briefing = oppositionBriefing with
            {
                Category = "gm-prep/opposition/briefing|variant",
                OutputFormat = "mp3",
                DeduplicationKey = "shared:key",
                Payload = oppositionBriefing.Payload
                    .Replace("packet://opposition/alpha", "packet://opposition/beta", StringComparison.Ordinal)
                    .Replace("opposition-alpha", "opposition-beta", StringComparison.Ordinal)
            }
        }
    ]
});
var collisionPacketJobs = collisionReceipt.Artifacts
    .Where(static artifact => artifact.Role == GmPrepPacketArtifactRole.Packet)
    .Select(static artifact => artifact.JobId)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();
Assert(collisionPacketJobs.Length == 2, "Delimiter-heavy GM prep packet variants must not collapse onto one media job.");

var receiptCollision = await packets.RenderAsync(request with
{
    RenderingId = "gm-prep-render-receipt-collision-proof",
    Entries =
    [
        request.Entries[0],
        request.Entries[0] with
        {
            SourceEntryId = "opposition-gamma",
            PacketRef = "packet://opposition/gamma",
            Packet = request.Entries[0].Packet with
            {
                OutputFormat = "pdf|variant",
                DeduplicationKey = "packet-receipt-a",
                Payload = request.Entries[0].Packet.Payload
                    .Replace("packet://opposition/alpha", "packet://opposition/gamma", StringComparison.Ordinal)
                    .Replace("opposition-alpha", "opposition-gamma", StringComparison.Ordinal)
            },
            Preview = request.Entries[0].Preview with
            {
                OutputFormat = "pdf",
                DeduplicationKey = "packet-receipt-b",
                Payload = request.Entries[0].Preview.Payload
                    .Replace("packet://opposition/alpha", "packet://opposition/gamma", StringComparison.Ordinal)
                    .Replace("opposition-alpha", "opposition-gamma", StringComparison.Ordinal)
            },
            Briefing = oppositionBriefing with
            {
                OutputFormat = "briefing|variant",
                DeduplicationKey = "briefing-receipt-c",
                Payload = oppositionBriefing.Payload
                    .Replace("packet://opposition/alpha", "packet://opposition/gamma", StringComparison.Ordinal)
                    .Replace("opposition-alpha", "opposition-gamma", StringComparison.Ordinal)
            }
        }
    ]
});
var receiptCollisionIds = receiptCollision.Artifacts
    .Where(static artifact => artifact.PacketRef.StartsWith("packet://opposition/gamma", StringComparison.OrdinalIgnoreCase))
    .Select(static artifact => artifact.ReceiptId)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();
Assert(receiptCollisionIds.Length == 3, "Delimiter-heavy GM prep packet receipt variants must not collapse onto one receipt id.");

var scopedEntryReceipt = await packets.RenderAsync(request with
{
    GovernedSourcePackId = "governed-pack-002",
    SourcePackRevisionId = "governed-pack-rev-002",
    Entries = request.Entries
        .Select(static entry => entry with
        {
            Packet = entry.Packet with
            {
                Payload = entry.Packet.Payload
                    .Replace("governed-pack-001", "governed-pack-002", StringComparison.Ordinal)
                    .Replace("governed-pack-rev-001", "governed-pack-rev-002", StringComparison.Ordinal)
            },
            Preview = entry.Preview with
            {
                Payload = entry.Preview.Payload
                    .Replace("governed-pack-001", "governed-pack-002", StringComparison.Ordinal)
                    .Replace("governed-pack-rev-001", "governed-pack-rev-002", StringComparison.Ordinal)
            },
            Briefing = entry.Briefing is null
                ? null
                : entry.Briefing with
                {
                    Payload = entry.Briefing.Payload
                        .Replace("governed-pack-001", "governed-pack-002", StringComparison.Ordinal)
                        .Replace("governed-pack-rev-001", "governed-pack-rev-002", StringComparison.Ordinal)
                }
        })
        .ToArray()
});
Assert(
    !receipt.EntryReceipts.Select(static entry => entry.EntryReceiptId)
        .SequenceEqual(scopedEntryReceipt.EntryReceipts.Select(static entry => entry.EntryReceiptId)),
    "Governed source pack scope should keep GM prep entry receipt ids distinct across reused rendering ids.");
Assert(
    !receipt.SubjectReceiptGroups.Select(static group => group.ReceiptId)
        .SequenceEqual(scopedEntryReceipt.SubjectReceiptGroups.Select(static group => group.ReceiptId)),
    "Governed source pack scope should keep GM prep subject receipt groups distinct across reused rendering ids.");

try
{
    await packets.RenderAsync(request with
    {
        RenderingId = "gm-prep-null-entry",
        Entries =
        [
            request.Entries[0],
            null!,
            request.Entries[2]
        ]
    });
    throw new InvalidOperationException("Null GM prep entry validation did not fail.");
}
catch (ArgumentException exception) when (exception.Message.Contains("null entries", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await packets.RenderAsync(request with
    {
        RenderingId = "gm-prep-missing-preview-artifact",
        Entries =
        [
            request.Entries[0] with
            {
                Preview = null!
            },
            .. request.Entries.Skip(1)
        ]
    });
    throw new InvalidOperationException("Missing GM prep preview artifact validation did not fail.");
}
catch (ArgumentException exception) when (exception.Message.Contains("packet and preview artifacts", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await packets.RenderAsync(request with
    {
        RenderingId = "gm-prep-missing-opposition",
        Entries = request.Entries
            .Where(static entry => entry.SubjectKind != GmPrepPacketSubjectKind.Opposition)
            .ToArray()
    });
    throw new InvalidOperationException("Missing opposition entry validation did not fail.");
}
catch (ArgumentException exception) when (exception.Message.Contains("opposition", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await packets.RenderAsync(request with
    {
        RenderingId = "gm-prep-duplicate-source-entry",
        Entries =
        [
            request.Entries[0],
            request.Entries[1] with { SourceEntryId = request.Entries[0].SourceEntryId },
            request.Entries[2]
        ]
    });
    throw new InvalidOperationException("Duplicate GM prep source entry validation did not fail.");
}
catch (ArgumentException exception) when (exception.Message.Contains("source entry", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await packets.RenderAsync(request with
    {
        RenderingId = "gm-prep-duplicate-packet-ref",
        Entries =
        [
            request.Entries[0],
            request.Entries[1] with { PacketRef = request.Entries[0].PacketRef },
            request.Entries[2]
        ]
    });
    throw new InvalidOperationException("Duplicate GM prep packet ref validation did not fail.");
}
catch (ArgumentException exception) when (exception.Message.Contains("packet refs", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await packets.RenderAsync(request with
    {
        RenderingId = "gm-prep-json-scope-spoof",
        Entries =
        [
            request.Entries[0] with
            {
                Packet = request.Entries[0].Packet with
                {
                    Payload = "{\"governedSourcePackId\":\"wrong-pack\",\"sourcePackRevisionId\":\"wrong-rev\",\"note\":\"governed-pack-001 governed-pack-rev-001\"}"
                }
            },
            .. request.Entries.Skip(1)
        ]
    });
    throw new InvalidOperationException("GM prep packet JSON scope spoof validation did not fail.");
}
catch (ArgumentException exception) when (exception.Message.Contains("governed source pack id", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await packets.RenderAsync(request with
    {
        RenderingId = "gm-prep-text-scope-near-miss",
        Entries =
        [
            request.Entries[0] with
            {
                Packet = request.Entries[0].Packet with
                {
                    Payload = "governedSourcePackId=governed-pack-001-near sourcePackRevisionId=governed-pack-rev-001 packetRef=packet://opposition/alpha"
                }
            },
            .. request.Entries.Skip(1)
        ]
    });
    throw new InvalidOperationException("GM prep packet text payload near-miss scope validation did not fail.");
}
catch (ArgumentException exception) when (exception.Message.Contains("governed source pack id", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await packets.RenderAsync(request with
    {
        RenderingId = "gm-prep-json-missing-scope-fields",
        Entries =
        [
            request.Entries[0] with
            {
                Packet = request.Entries[0].Packet with
                {
                    Payload = "{\"note\":\"governed-pack-001 governed-pack-rev-001\"}"
                }
            },
            .. request.Entries.Skip(1)
        ]
    });
    throw new InvalidOperationException("GM prep packet JSON payloads without required scope fields should fail closed instead of falling back to substring matching.");
}
catch (ArgumentException exception) when (exception.Message.Contains("governed source pack id", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await packets.RenderAsync(request with
    {
        RenderingId = "gm-prep-json-array-scope-spoof",
        Entries =
        [
            request.Entries[0] with
            {
                Packet = request.Entries[0].Packet with
                {
                    Payload = "[\"governed-pack-001\",\"governed-pack-rev-001\"]"
                }
            },
            .. request.Entries.Skip(1)
        ]
    });
    throw new InvalidOperationException("GM prep packet JSON array payloads without required scope fields should fail closed instead of falling back to substring matching.");
}
catch (ArgumentException exception) when (exception.Message.Contains("governed source pack id", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await packets.RenderAsync(request with
    {
        RenderingId = "gm-prep-json-string-scope-spoof",
        Entries =
        [
            request.Entries[0] with
            {
                Packet = request.Entries[0].Packet with
                {
                    Payload = "\"governed-pack-001 governed-pack-rev-001\""
                }
            },
            .. request.Entries.Skip(1)
        ]
    });
    throw new InvalidOperationException("GM prep packet JSON string payloads without required scope fields should fail closed instead of falling back to substring matching.");
}
catch (ArgumentException exception) when (exception.Message.Contains("governed source pack id", StringComparison.OrdinalIgnoreCase))
{
}

var nonJsonScopeReceipt = await packets.RenderAsync(request with
{
    RenderingId = "gm-prep-non-json-scope-fallback",
    Entries = request.Entries
        .Select(static entry => entry with
        {
            Packet = entry.Packet with
            {
                Payload = $"governedSourcePackId=governed-pack-001 sourcePackRevisionId=governed-pack-rev-001 packetRef={entry.PacketRef} sourceEntryId={entry.SourceEntryId}"
            },
            Preview = entry.Preview with
            {
                Payload = $"governedSourcePackId=governed-pack-001 sourcePackRevisionId=governed-pack-rev-001 packetRef={entry.PacketRef} sourceEntryId={entry.SourceEntryId}"
            },
            Briefing = entry.Briefing is null
                ? null
                : entry.Briefing with
                {
                    Payload = $"governedSourcePackId=governed-pack-001 sourcePackRevisionId=governed-pack-rev-001 packetRef={entry.PacketRef} sourceEntryId={entry.SourceEntryId}"
                }
        })
        .ToArray()
});
Assert(nonJsonScopeReceipt.Artifacts.Count == 8, "Non-JSON GM prep payloads should still render when they carry governed source scope text.");

var paddedJsonScopeReceipt = await packets.RenderAsync(request with
{
    RenderingId = "gm-prep-json-scope-padding",
    Entries = request.Entries
        .Select(static entry => entry with
        {
            Packet = entry.Packet with
            {
                Payload = $$"""{"governedSourcePackId":"  governed-pack-001  ","sourcePackRevisionId":"  governed-pack-rev-001  ","packetRef":"  {{entry.PacketRef}}  ","sourceEntryId":"  {{entry.SourceEntryId}}  "}"""
            },
            Preview = entry.Preview with
            {
                Payload = $$"""{"governedSourcePackId":"  governed-pack-001  ","sourcePackRevisionId":"  governed-pack-rev-001  ","packetRef":"  {{entry.PacketRef}}  ","sourceEntryId":"  {{entry.SourceEntryId}}  "}"""
            },
            Briefing = entry.Briefing is null
                ? null
                : entry.Briefing with
                {
                    Payload = $$"""{"governedSourcePackId":"  governed-pack-001  ","sourcePackRevisionId":"  governed-pack-rev-001  ","packetRef":"  {{entry.PacketRef}}  ","sourceEntryId":"  {{entry.SourceEntryId}}  "}"""
                }
        })
        .ToArray()
});
Assert(paddedJsonScopeReceipt.Artifacts.Count == 8, "JSON GM prep payloads should trim surrounding whitespace on governed scope values.");

var paddedTextScopeReceipt = await packets.RenderAsync(request with
{
    RenderingId = "gm-prep-text-scope-padding",
    Entries = request.Entries
        .Select(static entry => entry with
        {
            Packet = entry.Packet with
            {
                Payload = $"governedSourcePackId=\"  governed-pack-001  \" sourcePackRevisionId=\"  governed-pack-rev-001  \" packetRef=\"  {entry.PacketRef}  \" sourceEntryId=\"  {entry.SourceEntryId}  \""
            },
            Preview = entry.Preview with
            {
                Payload = $"governedSourcePackId=\"  governed-pack-001  \" sourcePackRevisionId=\"  governed-pack-rev-001  \" packetRef=\"  {entry.PacketRef}  \" sourceEntryId=\"  {entry.SourceEntryId}  \""
            },
            Briefing = entry.Briefing is null
                ? null
                : entry.Briefing with
                {
                    Payload = $"governedSourcePackId=\"  governed-pack-001  \" sourcePackRevisionId=\"  governed-pack-rev-001  \" packetRef=\"  {entry.PacketRef}  \" sourceEntryId=\"  {entry.SourceEntryId}  \""
                }
        })
        .ToArray()
});
Assert(paddedTextScopeReceipt.Artifacts.Count == 8, "Keyed text GM prep payloads should trim surrounding whitespace on governed scope values.");

try
{
    await packets.RenderAsync(request with
    {
        RenderingId = "gm-prep-json-packet-ref-spoof",
        Entries =
        [
            request.Entries[0] with
            {
                Packet = request.Entries[0].Packet with
                {
                    Payload = "{\"governedSourcePackId\":\"governed-pack-001\",\"sourcePackRevisionId\":\"governed-pack-rev-001\",\"packetRef\":\"packet://opposition/other\",\"sourceEntryId\":\"opposition-alpha\"}"
                }
            },
            .. request.Entries.Skip(1)
        ]
    });
    throw new InvalidOperationException("GM prep packet JSON packet-ref spoof validation did not fail.");
}
catch (ArgumentException exception) when (exception.Message.Contains("packet ref", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await packets.RenderAsync(request with
    {
        RenderingId = "gm-prep-text-source-entry-near-miss",
        Entries =
        [
            request.Entries[0] with
            {
                Packet = request.Entries[0].Packet with
                {
                    Payload = "governedSourcePackId=governed-pack-001 sourcePackRevisionId=governed-pack-rev-001 packetRef=packet://opposition/alpha sourceEntryId=opposition-alpha-near"
                }
            },
            .. request.Entries.Skip(1)
        ]
    });
    throw new InvalidOperationException("GM prep packet text payload near-miss source-entry validation did not fail.");
}
catch (ArgumentException exception) when (exception.Message.Contains("source entry id", StringComparison.OrdinalIgnoreCase))
{
}

Console.WriteLine("gm prep packet smoke ok");

static GmPrepPacketEntryRenderRequest CreateEntry(
    GmPrepPacketSubjectKind subjectKind,
    string sourceEntryId,
    string packetRef,
    bool includeBriefing,
    string packetCategory,
    string previewCategory,
    string? briefingCategory,
    string packetOutputFormat,
    string previewOutputFormat,
    string? briefingOutputFormat,
    string packetDeduplicationKey,
    string previewDeduplicationKey,
    string? briefingDeduplicationKey)
{
    var scopeJson = $$"""{"governedSourcePackId":"governed-pack-001","sourcePackRevisionId":"governed-pack-rev-001","packetRef":"{{packetRef}}","sourceEntryId":"{{sourceEntryId}}"}""";

    return new GmPrepPacketEntryRenderRequest(
        SubjectKind: subjectKind,
        SourceEntryId: sourceEntryId,
        PacketRef: packetRef,
        Packet: new GmPrepPacketArtifactRenderRequest(
            Category: packetCategory,
            Payload: scopeJson,
            OutputFormat: packetOutputFormat,
            DeduplicationKey: packetDeduplicationKey,
            CacheTtl: TimeSpan.FromMinutes(10),
            MaxBytes: 4096),
        Preview: new GmPrepPacketArtifactRenderRequest(
            Category: previewCategory,
            Payload: scopeJson,
            OutputFormat: previewOutputFormat,
            DeduplicationKey: previewDeduplicationKey,
            CacheTtl: TimeSpan.FromMinutes(10),
            MaxBytes: 4096),
        Briefing: includeBriefing
            ? new GmPrepPacketArtifactRenderRequest(
                Category: briefingCategory ?? throw new InvalidOperationException("Briefing category is required."),
                Payload: scopeJson,
                OutputFormat: briefingOutputFormat ?? throw new InvalidOperationException("Briefing output format is required."),
                DeduplicationKey: briefingDeduplicationKey ?? throw new InvalidOperationException("Briefing dedupe is required."),
                CacheTtl: TimeSpan.FromMinutes(10),
                MaxBytes: 4096)
            : null);
}

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
