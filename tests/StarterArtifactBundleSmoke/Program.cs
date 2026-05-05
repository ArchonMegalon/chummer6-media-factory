using Chummer.Media.Contracts;
using Chummer.Run.AI.Services.Assets;

var assets = new AssetLifecycleService();
var jobs = new MediaRenderJobService(assets);
var starterBundles = new StarterArtifactBundleService(jobs);

var request = BuildRequest("starter-artifacts-smoke", "en-US", ["es-ES"]);
var receipt = await starterBundles.RenderAsync(request);

Assert(receipt.Artifacts.Count == 18, "Starter artifact rendering should receipt every requested sibling.");
Assert(receipt.FallbackLocales.SequenceEqual(["es-ES"]), "Starter artifact receipts should preserve fallback locales.");
Assert(receipt.StarterPrimerReceiptIds.Count == 6, "Starter primer receipt ids should preserve requested and fallback locale siblings.");
Assert(receipt.FirstSessionBriefingReceiptIds.Count == 6, "First-session briefing receipt ids should preserve requested and fallback locale siblings.");
Assert(receipt.SupportSafeOnboardingReceiptIds.Count == 6, "Support-safe onboarding receipt ids should preserve requested and fallback locale siblings.");
Assert(receipt.RequestedLocaleReceiptIds.Count == 9, "Requested locale receipt ids should stay first-class.");
Assert(receipt.FallbackLocaleReceiptIds.Count == 9, "Fallback locale receipt ids should stay first-class.");
Assert(receipt.ReadyRefs.Count == 18, "Every starter artifact should publish a structured ready ref.");
Assert(receipt.LocaleReceiptGroups.Count == 2, "Starter artifact receipts should group emitted siblings per locale.");
Assert(receipt.BundleLocaleReceiptGroups.Count == 6, "Starter artifact receipts should group emitted siblings per bundle kind and locale.");
Assert(receipt.SupportNoteReceipts.Any(static row => row.Ref == "support://starter/es-ES/fallback" && row.Locales.Contains("es-ES")), "Fallback support notes must stay first-class.");
Assert(receipt.CaptionRefReceipts.Any(static row => row.Ref == "caption://starter/en-US/video.vtt" && row.JobIds.Count == 1), "Caption ref receipts must preserve starter locale siblings.");
Assert(receipt.PreviewRefReceipts.Any(static row => row.Ref == "preview://starter/es-ES/card" && row.Locales.Contains("es-ES")), "Preview ref receipts must preserve fallback locale siblings.");
Assert(receipt.Artifacts.All(static artifact => artifact.JobState == MediaRenderJobState.Succeeded), "Starter artifact receipts must wait for completed media jobs.");
Assert(receipt.Artifacts.All(static artifact => !string.IsNullOrWhiteSpace(artifact.AssetUrl)), "Starter artifact receipts must preserve concrete asset urls.");

var replayed = await starterBundles.RenderAsync(request);
Assert(
    receipt.Artifacts.Select(static artifact => artifact.JobId).SequenceEqual(replayed.Artifacts.Select(static artifact => artifact.JobId)),
    "Replay-safe dedupe should keep starter artifact jobs stable.");
Assert(receipt.RenderedAtUtc == replayed.RenderedAtUtc, "Replay-safe dedupe should keep starter artifact rendered timestamps stable.");

var metadataReplayed = await starterBundles.RenderAsync(request with
{
    RenderingId = "starter-artifacts-smoke-replayed",
    Source = "starter-artifacts-smoke-replayed",
    RequestedAtUtc = request.RequestedAtUtc.AddMinutes(45)
});
Assert(
    receipt.Artifacts.Select(static artifact => artifact.ReceiptId).SequenceEqual(metadataReplayed.Artifacts.Select(static artifact => artifact.ReceiptId)),
    "Starter artifact source and requested timestamp metadata should stay outside receipt identity.");
Assert(
    receipt.ReadyRefs.Select(static row => (row.Ref, row.ReceiptId, row.JobId))
        .SequenceEqual(metadataReplayed.ReadyRefs.Select(static row => (row.Ref, row.ReceiptId, row.JobId))),
    "Starter artifact source and requested timestamp metadata should stay outside ready-ref identity.");

var reorderedReceipt = await starterBundles.RenderAsync(request with
{
    RenderingId = "starter-artifacts-smoke-reordered",
    Artifacts = request.Artifacts
        .Reverse()
        .Select(static artifact => artifact with
        {
            CaptionRefs = artifact.CaptionRefs.Reverse().ToArray(),
            PreviewRefs = artifact.PreviewRefs.Reverse().ToArray(),
            SupportNoteRefs = artifact.SupportNoteRefs.Reverse().ToArray(),
        })
        .ToArray()
});
Assert(
    receipt.BundleLocaleReceiptGroups.Select(static group => (
        group.BundleKind,
        group.Locale,
        string.Join("|", group.ReceiptIds),
        string.Join("|", group.ArtifactRefs)))
    .SequenceEqual(
        reorderedReceipt.BundleLocaleReceiptGroups.Select(static group => (
            group.BundleKind,
            group.Locale,
            string.Join("|", group.ReceiptIds),
            string.Join("|", group.ArtifactRefs)))),
    "Starter artifact bundle-locale groups should stay stable when callers reorder the same siblings.");

var collisionReceipt = await starterBundles.RenderAsync(BuildRequest(
    "starter-artifacts-collision-proof",
    "en-US",
    ["es-ES"],
    extraArtifacts:
    [
        CreateArtifact(
            StarterArtifactBundleKind.StarterPrimer,
            StarterArtifactRole.Video,
            "es-ES",
            "starter/video/web",
            "webm",
            "starter://es-ES/video-web",
            ["caption://starter/es-ES/video-web.vtt"],
            ["preview://starter/es-ES/web-card"],
            [],
            "starter-video")
    ]));
var collidingJobs = collisionReceipt.Artifacts
    .Where(static artifact => artifact.BundleKind == StarterArtifactBundleKind.StarterPrimer &&
                              artifact.Role == StarterArtifactRole.Video &&
                              artifact.Locale == "es-ES")
    .Select(static artifact => artifact.JobId)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();
Assert(collidingJobs.Length == 2, "Different starter artifact output refs must not collapse onto one media job.");

var delimiterCollisionReceipt = await starterBundles.RenderAsync(BuildRequest(
    "starter-artifacts-receipt-collision-proof",
    "en-US",
    ["es-ES"],
    artifactMutator: artifact => artifact.ArtifactRef == "starter://es-ES/video"
        ? artifact with
        {
            ArtifactRef = "starter://es-ES/receipt-delimiter/a",
            CaptionRefs = ["caption://starter/es-ES/receipt-delimiter/a.vtt"],
            PreviewRefs = ["preview://starter/es-ES/receipt-delimiter/a"],
            DeduplicationKey = "starter-receipt-a"
        }
        : artifact.ArtifactRef == "starter://es-ES/audio"
            ? artifact with
            {
                ArtifactRef = "starter://es-ES/receipt-delimiter",
                OutputFormat = "mp3:a",
                CaptionRefs = ["caption://starter/es-ES/receipt-delimiter.vtt", "caption://starter/es-ES/a.vtt"],
                DeduplicationKey = "receipt|delimiter|a"
            }
            : artifact));
var delimiterReceipts = delimiterCollisionReceipt.Artifacts
    .Where(static artifact => artifact.Locale == "es-ES" &&
                              artifact.BundleKind == StarterArtifactBundleKind.StarterPrimer &&
                              artifact.Role is StarterArtifactRole.Video or StarterArtifactRole.Audio)
    .Select(static artifact => artifact.ReceiptId)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();
Assert(delimiterReceipts.Length == 2, "Delimiter-heavy starter locale refs must not collapse onto one receipt id.");

var mixedCaseReceipt = await starterBundles.RenderAsync(BuildRequest(
    "starter-artifacts-mixed-case-ref-normalization",
    "en-US",
    ["es-ES"],
    artifactMutator: artifact => artifact.ArtifactRef == "support://en-US/video"
        ? artifact with
        {
            CaptionRefs = [" caption://starter/en-US/video.vtt ", "CAPTION://starter/en-US/video.vtt"],
            PreviewRefs = [" preview://starter/en-US/card ", "PREVIEW://starter/en-US/card"],
            SupportNoteRefs = [" support://starter/en-US/fallback ", "SUPPORT://starter/en-US/fallback"],
        }
        : artifact));
var mixedCaseReorderedReceipt = await starterBundles.RenderAsync(BuildRequest(
    "starter-artifacts-mixed-case-ref-normalization-reordered",
    "en-US",
    ["es-ES"],
    artifactMutator: artifact => artifact.ArtifactRef == "support://en-US/video"
        ? artifact with
        {
            CaptionRefs = ["CAPTION://starter/en-US/video.vtt", " caption://starter/en-US/video.vtt "],
            PreviewRefs = ["PREVIEW://starter/en-US/card", " preview://starter/en-US/card "],
            SupportNoteRefs = ["SUPPORT://starter/en-US/fallback", " support://starter/en-US/fallback "],
        }
        : artifact));
Assert(
    mixedCaseReceipt.Artifacts.Select(static artifact => artifact.ReceiptId)
        .SequenceEqual(mixedCaseReorderedReceipt.Artifacts.Select(static artifact => artifact.ReceiptId)),
    "Mixed-case caption, preview, and support-note duplicates should keep starter receipt ids stable when callers reorder the same refs.");
Assert(
    mixedCaseReceipt.CaptionRefs.SequenceEqual(mixedCaseReorderedReceipt.CaptionRefs),
    "Mixed-case caption ref duplicates should keep aggregate starter caption refs stable when callers reorder the same refs.");
Assert(
    mixedCaseReceipt.PreviewRefs.SequenceEqual(mixedCaseReorderedReceipt.PreviewRefs),
    "Mixed-case preview ref duplicates should keep aggregate starter preview refs stable when callers reorder the same refs.");
Assert(
    mixedCaseReceipt.SupportNoteRefs.SequenceEqual(mixedCaseReorderedReceipt.SupportNoteRefs),
    "Mixed-case support-note duplicates should keep aggregate starter support-note refs stable when callers reorder the same refs.");
Assert(
    mixedCaseReceipt.CaptionRefReceipts.Select(static row => row.Ref)
        .SequenceEqual(mixedCaseReorderedReceipt.CaptionRefReceipts.Select(static row => row.Ref)),
    "Mixed-case caption ref receipt rows should keep canonical starter ref casing stable when callers reorder the same refs.");
Assert(
    mixedCaseReceipt.PreviewRefReceipts.Select(static row => row.Ref)
        .SequenceEqual(mixedCaseReorderedReceipt.PreviewRefReceipts.Select(static row => row.Ref)),
    "Mixed-case preview ref receipt rows should keep canonical starter ref casing stable when callers reorder the same refs.");
Assert(
    mixedCaseReceipt.SupportNoteReceipts.Select(static row => row.Ref)
        .SequenceEqual(mixedCaseReorderedReceipt.SupportNoteReceipts.Select(static row => row.Ref)),
    "Mixed-case support-note receipt rows should keep canonical starter ref casing stable when callers reorder the same refs.");

var textPayloadReceipt = await starterBundles.RenderAsync(BuildRequest(
    "starter-artifacts-text-payload-scope",
    "en-US",
    ["es-ES"],
    artifactMutator: artifact => artifact.ArtifactRef == "starter://en-US/video"
        ? artifact with
        {
            Payload = "approvedStarterSourcePackId=starter-pack-001 sourcePackRevisionId=starter-pack-rev-001 starterLaneId=starter-lane-001 locale=en-US"
        }
        : artifact));
Assert(
    textPayloadReceipt.Artifacts.Any(static artifact => artifact.ArtifactRef == "starter://en-US/video"),
    "Non-JSON starter artifact payloads should still render when they carry the starter scope text.");

await AssertThrowsAsync(
    () => starterBundles.RenderAsync(BuildRequest("starter-artifacts-missing-scope", "en-US", ["es-ES"], artifactMutator: artifact =>
        artifact.ArtifactRef == "starter://en-US/video"
            ? artifact with { Payload = """{"approvedStarterSourcePackId":"starter-pack-wrong","sourcePackRevisionId":"starter-pack-rev-001","starterLaneId":"starter-lane-001","locale":"en-US"}""" }
            : artifact)),
    "Starter artifact payload source-pack scope validation did not fail.");

await AssertThrowsAsync(
    () => starterBundles.RenderAsync(BuildRequest("starter-artifacts-json-missing-scope-fields", "en-US", ["es-ES"], artifactMutator: artifact =>
        artifact.ArtifactRef == "starter://en-US/audio"
            ? artifact with { Payload = """{"approvedStarterSourcePackId":"starter-pack-001"}""" }
            : artifact)),
    "Starter artifact JSON payloads without required scope fields should fail closed instead of falling back to substring matching.");

await AssertThrowsAsync(
    () => starterBundles.RenderAsync(BuildRequest("starter-artifacts-delimited-scope-spoof", "en-US", ["es-ES"], artifactMutator: artifact =>
        artifact.ArtifactRef == "starter://en-US/preview"
            ? artifact with { Payload = "approvedStarterSourcePackId=starter-pack-001x sourcePackRevisionId=starter-pack-rev-001 starterLaneId=starter-lane-001 locale=en-US" }
            : artifact)),
    "Starter artifact delimited text scope spoof validation did not fail.");

await AssertThrowsAsync(
    () => starterBundles.RenderAsync(BuildRequest("starter-artifacts-too-many-fallbacks", "en-US", ["es-ES", "fr-FR", "de-DE"])),
    "Starter artifact fallback locale bound validation did not fail.");

await AssertThrowsAsync(
    () => starterBundles.RenderAsync(BuildRequest("starter-artifacts-duplicate-artifact-ref", "en-US", ["es-ES"], artifactMutator: artifact =>
        artifact.ArtifactRef == "starter://es-ES/audio"
            ? artifact with { ArtifactRef = "starter://en-US/video" }
            : artifact)),
    "Duplicate starter artifact ref validation did not fail.");

Console.WriteLine("starter artifact bundle smoke ok");

static StarterArtifactBundleRenderRequest BuildRequest(
    string renderingId,
    string requestedLocale,
    IReadOnlyList<string> fallbackLocales,
    IReadOnlyList<StarterArtifactRenderRequest>? extraArtifacts = null,
    Func<StarterArtifactRenderRequest, StarterArtifactRenderRequest>? artifactMutator = null)
{
    var artifacts = new List<StarterArtifactRenderRequest>();
    foreach (var locale in new[] { requestedLocale }.Concat(fallbackLocales))
    {
        artifacts.Add(CreateArtifact(StarterArtifactBundleKind.StarterPrimer, StarterArtifactRole.Video, locale, "starter/video", "mp4", $"starter://{locale}/video", [$"caption://starter/{locale}/video.vtt"], [$"preview://starter/{locale}/card"], [], "starter-video"));
        artifacts.Add(CreateArtifact(StarterArtifactBundleKind.StarterPrimer, StarterArtifactRole.Audio, locale, "starter/audio", "mp3", $"starter://{locale}/audio", [$"caption://starter/{locale}/audio.vtt"], [], [], "starter-audio"));
        artifacts.Add(CreateArtifact(StarterArtifactBundleKind.StarterPrimer, StarterArtifactRole.PreviewCard, locale, "starter/preview", "png", $"starter://{locale}/preview", [], [$"preview://starter/{locale}/card"], [], "starter-preview"));

        artifacts.Add(CreateArtifact(StarterArtifactBundleKind.FirstSessionBriefing, StarterArtifactRole.Video, locale, "briefing/video", "mp4", $"briefing://{locale}/video", [$"caption://briefing/{locale}/video.vtt"], [$"preview://briefing/{locale}/card"], [], "briefing-video"));
        artifacts.Add(CreateArtifact(StarterArtifactBundleKind.FirstSessionBriefing, StarterArtifactRole.Audio, locale, "briefing/audio", "mp3", $"briefing://{locale}/audio", [$"caption://briefing/{locale}/audio.vtt"], [], [], "briefing-audio"));
        artifacts.Add(CreateArtifact(StarterArtifactBundleKind.FirstSessionBriefing, StarterArtifactRole.PreviewCard, locale, "briefing/preview", "png", $"briefing://{locale}/preview", [], [$"preview://briefing/{locale}/card"], [], "briefing-preview"));

        artifacts.Add(CreateArtifact(StarterArtifactBundleKind.SupportSafeOnboarding, StarterArtifactRole.Video, locale, "support/video", "mp4", $"support://{locale}/video", [$"caption://support/{locale}/video.vtt"], [$"preview://support/{locale}/card"], [$"support://starter/{locale}/fallback"], "support-video"));
        artifacts.Add(CreateArtifact(StarterArtifactBundleKind.SupportSafeOnboarding, StarterArtifactRole.Audio, locale, "support/audio", "mp3", $"support://{locale}/audio", [$"caption://support/{locale}/audio.vtt"], [], [$"support://starter/{locale}/fallback"], "support-audio"));
        artifacts.Add(CreateArtifact(StarterArtifactBundleKind.SupportSafeOnboarding, StarterArtifactRole.PreviewCard, locale, "support/preview", "png", $"support://{locale}/preview", [], [$"preview://support/{locale}/card"], [$"support://starter/{locale}/fallback"], "support-preview"));
    }

    if (extraArtifacts is not null)
    {
        artifacts.AddRange(extraArtifacts);
    }

    if (artifactMutator is not null)
    {
        artifacts = artifacts.Select(artifactMutator).ToList();
    }

    return new StarterArtifactBundleRenderRequest(
        RenderingId: renderingId,
        ApprovedStarterSourcePackId: "starter-pack-001",
        SourcePackRevisionId: "starter-pack-rev-001",
        StarterLaneId: "starter-lane-001",
        RequestedLocale: requestedLocale,
        Source: "starter-artifact-bundle-smoke",
        RequestedAtUtc: DateTimeOffset.UtcNow,
        Artifacts: artifacts);
}

static StarterArtifactRenderRequest CreateArtifact(
    StarterArtifactBundleKind bundleKind,
    StarterArtifactRole role,
    string locale,
    string category,
    string outputFormat,
    string artifactRef,
    IReadOnlyList<string> captionRefs,
    IReadOnlyList<string> previewRefs,
    IReadOnlyList<string> supportRefs,
    string deduplicationKey)
{
    var payload = $$"""
    {"approvedStarterSourcePackId":"starter-pack-001","sourcePackRevisionId":"starter-pack-rev-001","starterLaneId":"starter-lane-001","locale":"{{locale}}","artifactRef":"{{artifactRef}}"}
    """;
    return new StarterArtifactRenderRequest(
        BundleKind: bundleKind,
        Role: role,
        Locale: locale,
        Category: category,
        Payload: payload,
        OutputFormat: outputFormat,
        ArtifactRef: artifactRef,
        CaptionRefs: captionRefs,
        PreviewRefs: previewRefs,
        SupportNoteRefs: supportRefs,
        DeduplicationKey: deduplicationKey,
        CacheTtl: TimeSpan.FromMinutes(10),
        MaxBytes: 4096);
}

static async Task AssertThrowsAsync(Func<Task> action, string failureMessage)
{
    try
    {
        await action();
        throw new InvalidOperationException(failureMessage);
    }
    catch (ArgumentException)
    {
    }
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
