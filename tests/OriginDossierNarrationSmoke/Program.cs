using Chummer.Media.Contracts;
using Chummer.Run.AI.Services.Assets;

var assets = new AssetLifecycleService();
var jobs = new MediaRenderJobService(assets);
var narration = new OriginDossierNarrationRenderingService(jobs);

var request = CreateBaseRequest();
var receipt = await narration.RenderAsync(request);

Assert(receipt.Artifacts.Count == 2, "Origin dossier narration rendering should receipt each requested sibling.");
Assert(receipt.PrimaryAudioReceiptIds.Count == 1, "Origin dossier narration primary audio receipt id is required.");
Assert(receipt.AlternateAudioReceiptIds.Count == 1, "Origin dossier narration alternate audio receipt id is required.");
Assert(receipt.JobIds.Count == 2, "Origin dossier narration receipt should expose every media job id directly.");
Assert(receipt.CompanionRefs.Count == 2, "Each origin dossier narration sibling should publish a stable ref.");
Assert(receipt.CompanionReadyRefs.Count == 2, "Each origin dossier narration sibling should publish a structured ready ref.");
Assert(receipt.CompanionRefReceipts.Count == 2, "Each origin dossier narration sibling should publish a first-class companion ref receipt row.");
Assert(receipt.Artifacts.All(static artifact => artifact.JobState == MediaRenderJobState.Succeeded), "Origin dossier narration receipts must wait for completed media jobs.");
Assert(receipt.Artifacts.All(static artifact => !string.IsNullOrWhiteSpace(artifact.Provider)), "Origin dossier narration receipts must preserve provider identity.");
Assert(receipt.CompanionReadyRefs.All(static row => !string.IsNullOrWhiteSpace(row.Ref) && !string.IsNullOrWhiteSpace(row.ReceiptId) && !string.IsNullOrWhiteSpace(row.JobId) && !string.IsNullOrWhiteSpace(row.AssetId) && !string.IsNullOrWhiteSpace(row.AssetUrl)), "Origin dossier narration ready refs must preserve ref, receipt, job, asset id, and asset url.");
Assert(receipt.CompanionReadyRefs.Any(static row => row.Role == OriginDossierNarrationArtifactRole.CanonicalAudio && row.Provider == "Soundmadeseen"), "Origin dossier canonical ready refs must preserve the canonical provider.");
Assert(receipt.CompanionReadyRefs.Any(static row => row.Role == OriginDossierNarrationArtifactRole.AlternateAudio && row.Provider == "Unmixr AI"), "Origin dossier alternate ready refs must preserve the alternate provider.");
Assert(receipt.RoleReceiptGroups.Count == 2, "Each origin dossier narration sibling role should publish a first-class receipt group.");
Assert(receipt.RoleReceiptGroups.All(static group => group.JobIds.Count >= 1 && group.CompanionRefs.Count >= 1 && group.ArtifactReceipts.Count >= 1), "Origin dossier narration role receipt groups must preserve aggregate job ids, grouped companion refs, and grouped artifact rows.");
Assert(receipt.CaptionRefReceipts.Count == 1, "Origin dossier narration caption refs should publish first-class grouped receipt rows.");
Assert(receipt.CaptionRefReceipts[0].ReceiptIds.Count == 2, "Shared origin dossier narration caption ref should point at canonical and alternate audio receipts.");
Assert(receipt.CaptionRefReceipts[0].Providers.Count == 2, "Origin dossier narration caption receipt rows must preserve grouped providers.");
Assert(receipt.PreviewRefReceipts.Count == 1, "Origin dossier narration preview refs should publish first-class grouped receipt rows.");
Assert(receipt.PreviewRefReceipts[0].ReceiptIds.Count == 2, "Shared origin dossier narration preview ref should point at both voice receipts.");
Assert(receipt.PreviewRefReceipts[0].Providers.Count == 2, "Origin dossier narration preview receipt rows must preserve grouped providers.");

foreach (var artifact in receipt.Artifacts)
{
    await WaitForSucceededJobAsync(jobs, artifact.JobId);
}

var replayed = await narration.RenderAsync(request);
Assert(
    receipt.Artifacts.Select(static artifact => artifact.JobId).SequenceEqual(replayed.Artifacts.Select(static artifact => artifact.JobId)),
    "Replay-safe dedupe should keep origin dossier narration jobs stable.");
Assert(receipt.RenderedAtUtc == replayed.RenderedAtUtc, "Replay-safe dedupe should keep origin dossier narration rendered timestamps stable.");

var whitespaceReplay = await narration.RenderAsync(request with
{
    RenderingId = "  origin-dossier-narration-render-001  ",
    ApprovedOriginPacketId = "  approved-origin-packet-001  ",
    OriginRevisionId = "  origin-revision-9  ",
    Source = "  origin-dossier-narration-smoke  "
});
Assert(
    receipt.Artifacts.Select(static artifact => artifact.JobId).SequenceEqual(whitespaceReplay.Artifacts.Select(static artifact => artifact.JobId)),
    "Origin dossier narration job ids should stay stable when only top-level request whitespace changes.");

var reorderedReplay = await narration.RenderAsync(request with
{
    Artifacts =
    [
        request.Artifacts[1],
        request.Artifacts[0]
    ]
});
Assert(
    receipt.Artifacts.Select(static artifact => artifact.ReceiptId).SequenceEqual(reorderedReplay.Artifacts.Select(static artifact => artifact.ReceiptId)),
    "Origin dossier narration receipt ids should stay stable when approved origin packet artifacts reorder.");

var collisionReceipt = await narration.RenderAsync(request with
{
    RenderingId = "origin-dossier-narration-render-collision-proof",
    Artifacts =
    [
        .. request.Artifacts,
        request.Artifacts[1] with
        {
            OutputFormat = "wav",
            CompanionRef = "origin-dossier://packet-001/audio/alt-deluxe",
            DeduplicationKey = "packet-001-alternate"
        }
    ]
});
var collidingAlternateJobs = collisionReceipt.Artifacts
    .Where(static artifact => artifact.Role == OriginDossierNarrationArtifactRole.AlternateAudio)
    .Select(static artifact => artifact.JobId)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();
Assert(collidingAlternateJobs.Length == 2, "Different origin dossier output refs must not collapse onto one narration render job when request dedupe keys collide.");

try
{
    await narration.RenderAsync(request with
    {
        RenderingId = "origin-dossier-narration-duplicate-companion-ref",
        Artifacts =
        [
            request.Artifacts[0],
            request.Artifacts[1] with { CompanionRef = request.Artifacts[0].CompanionRef }
        ]
    });
    throw new InvalidOperationException("Duplicate origin dossier narration companion ref validation did not fail.");
}
catch (ArgumentException ex) when (ex.Message.Contains("must be unique", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await narration.RenderAsync(request with
    {
        RenderingId = "origin-dossier-narration-missing-approved-packet-scope",
        Artifacts =
        [
            request.Artifacts[0] with
            {
                Payload = "{\"approvedOriginPacketId\":\"wrong-packet\",\"originRevisionId\":\"origin-revision-9\"}"
            },
            request.Artifacts[1]
        ]
    });
    throw new InvalidOperationException("Origin dossier narration payload packet scope validation did not fail.");
}
catch (ArgumentException ex) when (ex.Message.Contains("approved origin packet id", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await narration.RenderAsync(request with
    {
        RenderingId = "origin-dossier-narration-missing-revision-scope",
        Artifacts =
        [
            request.Artifacts[0],
            request.Artifacts[1] with
            {
                Payload = "{\"approvedOriginPacketId\":\"approved-origin-packet-001\",\"originRevisionId\":\"wrong-revision\"}"
            }
        ]
    });
    throw new InvalidOperationException("Origin dossier narration payload revision scope validation did not fail.");
}
catch (ArgumentException ex) when (ex.Message.Contains("origin revision id", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await narration.RenderAsync(request with
    {
        RenderingId = "origin-dossier-narration-json-missing-scope-fields",
        Artifacts =
        [
            request.Artifacts[0] with
            {
                Payload = "{\"packet\":\"approved-origin-packet-001\"}"
            },
            request.Artifacts[1]
        ]
    });
    throw new InvalidOperationException("Origin dossier narration JSON payloads without required scope fields should fail closed instead of falling back to substring matching.");
}
catch (ArgumentException ex) when (ex.Message.Contains("approved origin packet id", StringComparison.OrdinalIgnoreCase))
{
}

return;

static OriginDossierNarrationRenderRequest CreateBaseRequest() =>
    new(
        RenderingId: "origin-dossier-narration-render-001",
        ApprovedOriginPacketId: "approved-origin-packet-001",
        OriginRevisionId: "origin-revision-9",
        Source: "origin-dossier-narration-smoke",
        RequestedAtUtc: DateTimeOffset.UtcNow,
        Artifacts:
        [
            new OriginDossierNarrationArtifactRenderRequest(
                Role: OriginDossierNarrationArtifactRole.CanonicalAudio,
                Provider: "Soundmadeseen",
                Category: "origin-dossier/narration/audio/canonical",
                Payload: "{\"approvedOriginPacketId\":\"approved-origin-packet-001\",\"originRevisionId\":\"origin-revision-9\",\"lane\":\"default\"}",
                OutputFormat: "mp3",
                CompanionRef: "origin-dossier://packet-001/audio/default",
                CaptionRefs: ["caption://packet-001/origin/default.vtt"],
                PreviewRefs: ["preview://packet-001/origin/shared"],
                DeduplicationKey: "packet-001-canonical",
                CacheTtl: TimeSpan.FromMinutes(10),
                MaxBytes: 4096),
            new OriginDossierNarrationArtifactRenderRequest(
                Role: OriginDossierNarrationArtifactRole.AlternateAudio,
                Provider: "Unmixr AI",
                Category: "origin-dossier/narration/audio/alternate",
                Payload: "{\"approvedOriginPacketId\":\"approved-origin-packet-001\",\"originRevisionId\":\"origin-revision-9\",\"lane\":\"alternate\"}",
                OutputFormat: "mp3",
                CompanionRef: "origin-dossier://packet-001/audio/alternate",
                CaptionRefs: ["caption://packet-001/origin/default.vtt"],
                PreviewRefs: ["preview://packet-001/origin/shared"],
                DeduplicationKey: "packet-001-alternate",
                CacheTtl: TimeSpan.FromMinutes(10),
                MaxBytes: 4096)
        ]);

static async Task WaitForSucceededJobAsync(IMediaRenderJobService jobs, string jobId)
{
    for (var attempt = 0; attempt < 50; attempt++)
    {
        var status = jobs.Get(jobId);
        if (status?.State == MediaRenderJobState.Succeeded)
        {
            return;
        }

        if (status?.State == MediaRenderJobState.Failed)
        {
            throw new InvalidOperationException($"Render job {jobId} failed: {status.Error}");
        }

        await Task.Delay(20);
    }

    throw new TimeoutException($"Render job {jobId} did not complete.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
