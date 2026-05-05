using Chummer.Media.Contracts;
using Chummer.Run.AI.Services.Assets;

var assets = new AssetLifecycleService();
var jobs = new MediaRenderJobService(assets);
var concierge = new InstallAwareConciergeBundleService(jobs);

var request = new InstallAwareConciergeRenderRequest(
    RenderingId: "install-aware-concierge-render-001",
    InstallAwarePacketId: "install-aware-packet-001",
    InstalledBuildReceiptId: "installed-build-receipt-001",
    ArtifactIdentityId: "artifact-identity-001",
    Source: "install-aware-concierge-smoke",
    RequestedAtUtc: DateTimeOffset.UtcNow,
    Artifacts:
    [
        CreateArtifact(InstallAwareConciergeBundleKind.ReleaseExplainer, InstallAwareConciergeArtifactRole.Video, "release/video", "mp4", "install-aware://release/video", ["caption://release/en-US.vtt"], ["preview://release/card"], ["note://release/what-changed"], "release-video"),
        CreateArtifact(InstallAwareConciergeBundleKind.ReleaseExplainer, InstallAwareConciergeArtifactRole.Audio, "release/audio", "mp3", "install-aware://release/audio", ["caption://release/en-US.vtt"], [], ["note://release/what-changed"], "release-audio"),
        CreateArtifact(InstallAwareConciergeBundleKind.ReleaseExplainer, InstallAwareConciergeArtifactRole.PreviewCard, "release/preview", "png", "install-aware://release/preview", [], ["preview://release/card"], ["note://release/what-changed"], "release-preview"),
        CreateArtifact(InstallAwareConciergeBundleKind.SupportClosure, InstallAwareConciergeArtifactRole.Video, "support/video", "mp4", "install-aware://support/video", ["caption://support/en-US.vtt"], ["preview://support/card"], ["note://support/fixed", "note://support/next-step"], "support-video"),
        CreateArtifact(InstallAwareConciergeBundleKind.SupportClosure, InstallAwareConciergeArtifactRole.Audio, "support/audio", "mp3", "install-aware://support/audio", ["caption://support/en-US.vtt"], [], ["note://support/fixed", "note://support/next-step"], "support-audio"),
        CreateArtifact(InstallAwareConciergeBundleKind.SupportClosure, InstallAwareConciergeArtifactRole.PreviewCard, "support/preview", "png", "install-aware://support/preview", [], ["preview://support/card"], ["note://support/fixed"], "support-preview"),
        CreateArtifact(InstallAwareConciergeBundleKind.PublicConcierge, InstallAwareConciergeArtifactRole.Video, "public/video", "mp4", "install-aware://public/video", ["caption://public/en-US.vtt"], ["preview://public/card"], ["note://public/fallback"], "public-video"),
        CreateArtifact(InstallAwareConciergeBundleKind.PublicConcierge, InstallAwareConciergeArtifactRole.Audio, "public/audio", "mp3", "install-aware://public/audio", ["caption://public/en-US.vtt"], [], ["note://public/fallback"], "public-audio"),
        CreateArtifact(InstallAwareConciergeBundleKind.PublicConcierge, InstallAwareConciergeArtifactRole.PreviewCard, "public/preview", "png", "install-aware://public/preview", [], ["preview://public/card"], ["note://public/fallback"], "public-preview"),
    ]);

var receipt = await concierge.RenderAsync(request);
Assert(receipt.Artifacts.Count == 9, "Install-aware concierge rendering should receipt each requested sibling.");
Assert(receipt.ReleaseExplainerReceiptIds.Count == 3, "Release explainer receipt ids are required.");
Assert(receipt.SupportClosureReceiptIds.Count == 3, "Support closure receipt ids are required.");
Assert(receipt.PublicConciergeReceiptIds.Count == 3, "Public concierge receipt ids are required.");
Assert(receipt.JobIds.Count == 9, "Install-aware concierge bundle receipt should expose every media job id directly.");
Assert(receipt.CompanionRefs.Count == 9, "Each concierge sibling should publish a stable ref.");
Assert(receipt.CompanionReadyRefs.Count == 9, "Each concierge sibling should publish a structured ready ref.");
Assert(receipt.CompanionRefReceipts.Count == 9, "Each concierge sibling should publish a first-class companion ref receipt row.");
Assert(receipt.Artifacts.All(static artifact => artifact.JobState == MediaRenderJobState.Succeeded), "Install-aware concierge receipts must wait for completed media jobs.");
Assert(receipt.Artifacts.All(static artifact => !string.IsNullOrWhiteSpace(artifact.AssetId)), "Install-aware concierge receipts must preserve concrete asset ids.");
Assert(receipt.Artifacts.All(static artifact => !string.IsNullOrWhiteSpace(artifact.AssetUrl)), "Install-aware concierge receipts must preserve concrete asset urls.");
Assert(receipt.BundleReceiptGroups.Count == 3, "Each install-aware sibling bundle should publish a first-class receipt group.");
Assert(receipt.RoleReceiptGroups.Count == 9, "Each concierge bundle kind and role should publish a first-class receipt group.");
Assert(receipt.BundleReceiptGroups.Any(static group => group.BundleKind == InstallAwareConciergeBundleKind.ReleaseExplainer && group.ReceiptIds.Count == 3), "Release explainer bundle groups must preserve aggregate receipt ids.");
Assert(receipt.BundleReceiptGroups.Any(static group => group.BundleKind == InstallAwareConciergeBundleKind.SupportClosure && group.PreviewRefs.Contains("preview://support/card")), "Support closure bundle groups must preserve preview refs.");
Assert(receipt.BundleReceiptGroups.Any(static group => group.BundleKind == InstallAwareConciergeBundleKind.PublicConcierge && group.SiblingNoteRefs.Contains("note://public/fallback")), "Public concierge bundle groups must preserve sibling notes.");
Assert(receipt.CaptionRefReceipts.Any(static row => row.Ref == "caption://release/en-US.vtt" && row.JobIds.Count == 2), "Release caption refs must preserve aggregate job ids.");
Assert(
    receipt.CaptionRefReceipts.Any(static row =>
        row.Ref == "caption://support/en-US.vtt" &&
        row.BundleKinds.Contains(InstallAwareConciergeBundleKind.SupportClosure) &&
        row.Roles.Contains(InstallAwareConciergeArtifactRole.Video) &&
        row.Roles.Contains(InstallAwareConciergeArtifactRole.Audio) &&
        row.ArtifactReceipts.Count == 2 &&
        row.ArtifactReceipts.All(static artifact => !string.IsNullOrWhiteSpace(artifact.AssetUrl))),
    "Support caption refs must preserve bundle kinds, roles, and grouped asset urls.");
Assert(receipt.PreviewRefReceipts.Any(static row => row.Ref == "preview://support/card" && row.BundleKinds.Contains(InstallAwareConciergeBundleKind.SupportClosure)), "Support preview refs must preserve bundle-kind grouping.");
Assert(
    receipt.PreviewRefReceipts.Any(static row =>
        row.Ref == "preview://public/card" &&
        row.BundleKinds.Contains(InstallAwareConciergeBundleKind.PublicConcierge) &&
        row.Roles.Contains(InstallAwareConciergeArtifactRole.Video) &&
        row.Roles.Contains(InstallAwareConciergeArtifactRole.PreviewCard) &&
        row.ArtifactReceipts.Count == 2 &&
        row.ArtifactReceipts.All(static artifact => !string.IsNullOrWhiteSpace(artifact.AssetUrl))),
    "Public preview refs must preserve bundle kinds, roles, and grouped asset urls.");
Assert(receipt.SiblingNoteReceipts.Any(static row => row.Ref == "note://support/fixed" && row.ReceiptIds.Count == 3), "Shared support sibling notes must preserve aggregate receipt ids.");
Assert(
    receipt.SiblingNoteReceipts.Any(static row =>
        row.Ref == "note://support/fixed" &&
        row.BundleKinds.Contains(InstallAwareConciergeBundleKind.SupportClosure) &&
        row.Roles.Contains(InstallAwareConciergeArtifactRole.Video) &&
        row.Roles.Contains(InstallAwareConciergeArtifactRole.Audio) &&
        row.Roles.Contains(InstallAwareConciergeArtifactRole.PreviewCard) &&
        row.ArtifactReceipts.Count == 3 &&
        row.ArtifactReceipts.All(static artifact => !string.IsNullOrWhiteSpace(artifact.AssetUrl))),
    "Shared support sibling notes must preserve bundle kinds, roles, and grouped asset urls.");
Assert(receipt.CompanionReadyRefs.All(static row => row.SiblingNoteRefs.Count >= 1 && row.SiblingNoteRefs.Count <= 2), "Companion ready refs must preserve bounded sibling notes.");

foreach (var artifact in receipt.Artifacts)
{
    await WaitForSucceededJobAsync(jobs, artifact.JobId);
}

var replayed = await concierge.RenderAsync(request);
Assert(
    receipt.Artifacts.Select(static artifact => artifact.JobId).SequenceEqual(replayed.Artifacts.Select(static artifact => artifact.JobId)),
    "Replay-safe dedupe should keep install-aware concierge jobs stable.");
Assert(receipt.RenderedAtUtc == replayed.RenderedAtUtc, "Replay-safe dedupe should keep install-aware concierge rendered timestamps stable.");

var metadataReplayed = await concierge.RenderAsync(request with
{
    Source = "install-aware-concierge-smoke-replayed",
    RequestedAtUtc = request.RequestedAtUtc.AddMinutes(45)
});
Assert(
    receipt.Artifacts.Select(static artifact => artifact.JobId).SequenceEqual(metadataReplayed.Artifacts.Select(static artifact => artifact.JobId)),
    "Install-aware concierge source and requested timestamps should stay outside replay-safe dedupe.");
Assert(
    receipt.Artifacts.Select(static artifact => artifact.ReceiptId).SequenceEqual(metadataReplayed.Artifacts.Select(static artifact => artifact.ReceiptId)),
    "Install-aware concierge source and requested timestamps should stay outside receipt identity.");
Assert(
    receipt.CompanionReadyRefs.Select(static row => (row.Ref, row.ReceiptId, row.JobId))
        .SequenceEqual(metadataReplayed.CompanionReadyRefs.Select(static row => (row.Ref, row.ReceiptId, row.JobId))),
    "Install-aware concierge source and requested timestamps should stay outside companion ready identity.");

var reorderedReceipt = await concierge.RenderAsync(request with
{
    Artifacts = request.Artifacts
        .Reverse()
        .Select(static artifact => artifact with
        {
            CaptionRefs = artifact.CaptionRefs.Reverse().ToArray(),
            PreviewRefs = artifact.PreviewRefs.Reverse().ToArray(),
            SiblingNoteRefs = artifact.SiblingNoteRefs.Reverse().ToArray()
        })
        .ToArray()
});
Assert(
    receipt.ReleaseExplainerReceiptIds.SequenceEqual(reorderedReceipt.ReleaseExplainerReceiptIds),
    "Release explainer receipt ids should stay stable when callers reorder install-aware concierge siblings.");
Assert(
    receipt.SupportClosureReceiptIds.SequenceEqual(reorderedReceipt.SupportClosureReceiptIds),
    "Support closure receipt ids should stay stable when callers reorder install-aware concierge siblings.");
Assert(
    receipt.PublicConciergeReceiptIds.SequenceEqual(reorderedReceipt.PublicConciergeReceiptIds),
    "Public concierge receipt ids should stay stable when callers reorder install-aware concierge siblings.");
Assert(
    receipt.CompanionRefs.SequenceEqual(reorderedReceipt.CompanionRefs),
    "Companion refs should stay stable when callers reorder install-aware concierge siblings.");
Assert(
    receipt.CaptionRefs.SequenceEqual(reorderedReceipt.CaptionRefs),
    "Caption refs should stay stable when callers reorder install-aware concierge siblings.");
Assert(
    receipt.PreviewRefs.SequenceEqual(reorderedReceipt.PreviewRefs),
    "Preview refs should stay stable when callers reorder install-aware concierge siblings.");
Assert(
    receipt.SiblingNoteRefs.SequenceEqual(reorderedReceipt.SiblingNoteRefs),
    "Sibling note refs should stay stable when callers reorder install-aware concierge siblings.");
Assert(
    receipt.CompanionReadyRefs.Select(static row => (
        row.BundleKind,
        row.Role,
        row.Ref,
        row.ReceiptId,
        row.JobId,
        string.Join("|", row.CaptionRefs),
        string.Join("|", row.PreviewRefs),
        string.Join("|", row.SiblingNoteRefs)))
    .SequenceEqual(
        reorderedReceipt.CompanionReadyRefs.Select(static row => (
            row.BundleKind,
            row.Role,
            row.Ref,
            row.ReceiptId,
            row.JobId,
            string.Join("|", row.CaptionRefs),
            string.Join("|", row.PreviewRefs),
            string.Join("|", row.SiblingNoteRefs)))),
    "Companion ready refs should stay stable when callers reorder install-aware concierge siblings.");
Assert(
    receipt.BundleReceiptGroups.Select(static group => (
        group.BundleKind,
        string.Join("|", group.ReceiptIds),
        string.Join("|", group.JobIds),
        string.Join("|", group.CompanionRefs),
        string.Join("|", group.CaptionRefs),
        string.Join("|", group.PreviewRefs),
        string.Join("|", group.SiblingNoteRefs),
        string.Join("|", group.Roles),
        string.Join("|", group.ArtifactReceipts.Select(static artifact => artifact.ReceiptId))))
    .SequenceEqual(
        reorderedReceipt.BundleReceiptGroups.Select(static group => (
            group.BundleKind,
            string.Join("|", group.ReceiptIds),
            string.Join("|", group.JobIds),
            string.Join("|", group.CompanionRefs),
            string.Join("|", group.CaptionRefs),
            string.Join("|", group.PreviewRefs),
            string.Join("|", group.SiblingNoteRefs),
            string.Join("|", group.Roles),
            string.Join("|", group.ArtifactReceipts.Select(static artifact => artifact.ReceiptId))))),
    "Bundle receipt groups should stay stable when callers reorder install-aware concierge siblings.");
Assert(
    receipt.RoleReceiptGroups.Select(static group => (
        group.BundleKind,
        group.Role,
        string.Join("|", group.ReceiptIds),
        string.Join("|", group.JobIds),
        string.Join("|", group.CompanionRefs),
        string.Join("|", group.CaptionRefs),
        string.Join("|", group.PreviewRefs),
        string.Join("|", group.SiblingNoteRefs)))
    .SequenceEqual(
        reorderedReceipt.RoleReceiptGroups.Select(static group => (
            group.BundleKind,
            group.Role,
            string.Join("|", group.ReceiptIds),
            string.Join("|", group.JobIds),
            string.Join("|", group.CompanionRefs),
            string.Join("|", group.CaptionRefs),
            string.Join("|", group.PreviewRefs),
            string.Join("|", group.SiblingNoteRefs)))),
    "Role receipt groups should stay stable when callers reorder install-aware concierge siblings.");
Assert(
    receipt.CaptionRefReceipts.Select(static row => (
        row.Ref,
        string.Join("|", row.ReceiptIds),
        string.Join("|", row.JobIds),
        string.Join("|", row.CompanionRefs)))
    .SequenceEqual(
        reorderedReceipt.CaptionRefReceipts.Select(static row => (
            row.Ref,
            string.Join("|", row.ReceiptIds),
            string.Join("|", row.JobIds),
            string.Join("|", row.CompanionRefs)))),
    "Caption ref receipt rows should stay stable when callers reorder install-aware concierge siblings.");
Assert(
    receipt.PreviewRefReceipts.Select(static row => (
        row.Ref,
        string.Join("|", row.ReceiptIds),
        string.Join("|", row.JobIds),
        string.Join("|", row.CompanionRefs)))
    .SequenceEqual(
        reorderedReceipt.PreviewRefReceipts.Select(static row => (
            row.Ref,
            string.Join("|", row.ReceiptIds),
            string.Join("|", row.JobIds),
            string.Join("|", row.CompanionRefs)))),
    "Preview ref receipt rows should stay stable when callers reorder install-aware concierge siblings.");
Assert(
    receipt.SiblingNoteReceipts.Select(static row => (
        row.Ref,
        string.Join("|", row.ReceiptIds),
        string.Join("|", row.JobIds),
        string.Join("|", row.CompanionRefs)))
    .SequenceEqual(
        reorderedReceipt.SiblingNoteReceipts.Select(static row => (
            row.Ref,
            string.Join("|", row.ReceiptIds),
            string.Join("|", row.JobIds),
            string.Join("|", row.CompanionRefs)))),
    "Sibling note receipt rows should stay stable when callers reorder install-aware concierge siblings.");

var mixedCaseRefReceipt = await concierge.RenderAsync(request with
{
    RenderingId = "install-aware-mixed-case-ref-normalization",
    Artifacts =
    [
        request.Artifacts[0] with
        {
            CaptionRefs = ["caption://release/en-US.vtt", "CAPTION://RELEASE/EN-us.vtt"],
            PreviewRefs = ["preview://release/card", "PREVIEW://RELEASE/CARD"],
            SiblingNoteRefs = ["note://release/what-changed", "NOTE://RELEASE/WHAT-CHANGED"]
        },
        .. request.Artifacts.Skip(1)
    ]
});
var mixedCaseRefReorderedReceipt = await concierge.RenderAsync(request with
{
    RenderingId = " install-aware-mixed-case-ref-normalization ",
    Artifacts =
    [
        request.Artifacts[0] with
        {
            CaptionRefs = ["CAPTION://RELEASE/EN-us.vtt", "caption://release/en-US.vtt"],
            PreviewRefs = ["PREVIEW://RELEASE/CARD", "preview://release/card"],
            SiblingNoteRefs = ["NOTE://RELEASE/WHAT-CHANGED", "note://release/what-changed"]
        },
        .. request.Artifacts.Skip(1)
    ]
});
Assert(
    mixedCaseRefReceipt.ReleaseExplainerReceiptIds.SequenceEqual(mixedCaseRefReorderedReceipt.ReleaseExplainerReceiptIds),
    "Mixed-case caption, preview, and sibling-note duplicates should keep release receipt ids stable when callers reorder the same refs.");
Assert(
    mixedCaseRefReceipt.CaptionRefs.SequenceEqual(mixedCaseRefReorderedReceipt.CaptionRefs),
    "Mixed-case caption ref duplicates should keep aggregate caption refs stable when callers reorder the same refs.");
Assert(
    mixedCaseRefReceipt.PreviewRefs.SequenceEqual(mixedCaseRefReorderedReceipt.PreviewRefs),
    "Mixed-case preview ref duplicates should keep aggregate preview refs stable when callers reorder the same refs.");
Assert(
    mixedCaseRefReceipt.SiblingNoteRefs.SequenceEqual(mixedCaseRefReorderedReceipt.SiblingNoteRefs),
    "Mixed-case sibling-note duplicates should keep aggregate sibling-note refs stable when callers reorder the same refs.");
Assert(
    mixedCaseRefReceipt.CaptionRefReceipts.Select(static row => row.Ref)
        .SequenceEqual(mixedCaseRefReorderedReceipt.CaptionRefReceipts.Select(static row => row.Ref)),
    "Mixed-case caption ref receipt rows should keep canonical ref casing stable when callers reorder the same refs.");
Assert(
    mixedCaseRefReceipt.PreviewRefReceipts.Select(static row => row.Ref)
        .SequenceEqual(mixedCaseRefReorderedReceipt.PreviewRefReceipts.Select(static row => row.Ref)),
    "Mixed-case preview ref receipt rows should keep canonical ref casing stable when callers reorder the same refs.");
Assert(
    mixedCaseRefReceipt.SiblingNoteReceipts.Select(static row => row.Ref)
        .SequenceEqual(mixedCaseRefReorderedReceipt.SiblingNoteReceipts.Select(static row => row.Ref)),
    "Mixed-case sibling-note receipt rows should keep canonical ref casing stable when callers reorder the same refs.");

var collisionReceipt = await concierge.RenderAsync(request with
{
    RenderingId = "install-aware-concierge-render-collision-proof",
    Artifacts =
    [
        .. request.Artifacts,
        request.Artifacts[0] with
        {
            OutputFormat = "webm",
            CompanionRef = "install-aware://release/video-web",
            CaptionRefs = ["caption://release/en-US.web.vtt"],
            PreviewRefs = ["preview://release/web-card"],
            SiblingNoteRefs = ["note://release/web"],
            DeduplicationKey = "release-video"
        }
    ]
});
var collidingReleaseJobs = collisionReceipt.Artifacts
    .Where(static artifact => artifact.BundleKind == InstallAwareConciergeBundleKind.ReleaseExplainer && artifact.Role == InstallAwareConciergeArtifactRole.Video)
    .Select(static artifact => artifact.JobId)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();
Assert(collidingReleaseJobs.Length == 2, "Different install-aware release output refs must not collapse onto one concierge render job.");

var receiptDelimiterCollision = await concierge.RenderAsync(request with
{
    RenderingId = "install-aware-concierge-render-receipt-collision-proof",
    Artifacts =
    [
        .. request.Artifacts,
        request.Artifacts[0] with
        {
            OutputFormat = "mov",
            CompanionRef = "install-aware://release/receipt-delimiter/a",
            CaptionRefs = ["caption", "variant|one"],
            PreviewRefs = ["preview://release/receipt/card-a"],
            SiblingNoteRefs = ["note", "variant|one"],
            DeduplicationKey = "release-video-receipt-a"
        },
        request.Artifacts[0] with
        {
            OutputFormat = "avi",
            CompanionRef = "install-aware://release/receipt-delimiter/b",
            CaptionRefs = ["caption|variant", "one"],
            PreviewRefs = ["preview://release/receipt/card-b"],
            SiblingNoteRefs = ["note|variant", "one"],
            DeduplicationKey = "release-video-receipt-b"
        }
    ]
});
var delimiterReceiptIds = receiptDelimiterCollision.Artifacts
    .Where(static artifact => artifact.CompanionRef.StartsWith("install-aware://release/receipt-delimiter/", StringComparison.OrdinalIgnoreCase))
    .Select(static artifact => artifact.ReceiptId)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();
Assert(delimiterReceiptIds.Length == 2, "Delimiter-heavy install-aware concierge refs must not collapse onto one receipt id.");

try
{
    await concierge.RenderAsync(request with
    {
        RenderingId = "install-aware-duplicate-companion-ref",
        Artifacts =
        [
            request.Artifacts[0],
            request.Artifacts[1] with { CompanionRef = request.Artifacts[0].CompanionRef },
            .. request.Artifacts.Skip(2)
        ]
    });
    throw new InvalidOperationException("Duplicate install-aware concierge companion ref validation did not fail.");
}
catch (ArgumentException ex) when (ex.Message.Contains("must be unique", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await concierge.RenderAsync(request with
    {
        RenderingId = "install-aware-missing-packet-scope",
        Artifacts =
        [
            request.Artifacts[0] with
            {
                Payload = "{\"installAwarePacketId\":\"wrong-packet\",\"installedBuildReceiptId\":\"installed-build-receipt-001\",\"artifactIdentityId\":\"artifact-identity-001\",\"artifact\":\"video\"}"
            },
            .. request.Artifacts.Skip(1)
        ]
    });
    throw new InvalidOperationException("Install-aware concierge payload packet scope validation did not fail.");
}
catch (ArgumentException ex) when (ex.Message.Contains("packet id", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await concierge.RenderAsync(request with
    {
        RenderingId = "install-aware-json-scope-spoof",
        Artifacts =
        [
            request.Artifacts[0] with
            {
                Payload = "{\"installAwarePacketId\":\"wrong-packet\",\"installedBuildReceiptId\":\"wrong-build\",\"artifactIdentityId\":\"wrong-artifact\",\"note\":\"install-aware-packet-001 installed-build-receipt-001 artifact-identity-001\"}"
            },
            .. request.Artifacts.Skip(1)
        ]
    });
    throw new InvalidOperationException("Install-aware concierge JSON scope spoof validation did not fail.");
}
catch (ArgumentException ex) when (ex.Message.Contains("packet id", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await concierge.RenderAsync(request with
    {
        RenderingId = "install-aware-json-missing-scope-fields",
        Artifacts =
        [
            request.Artifacts[0] with
            {
                Payload = "{\"artifact\":\"release-video\",\"note\":\"installAwarePacketId=install-aware-packet-001 installedBuildReceiptId=installed-build-receipt-001 artifactIdentityId=artifact-identity-001\"}"
            },
            .. request.Artifacts.Skip(1)
        ]
    });
    throw new InvalidOperationException("Install-aware concierge JSON payload missing required scope fields did not fail.");
}
catch (ArgumentException ex) when (ex.Message.Contains("packet id", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await concierge.RenderAsync(request with
    {
        RenderingId = "install-aware-json-string-scope-spoof",
        Artifacts =
        [
            request.Artifacts[0] with
            {
                Payload = "\"installAwarePacketId=install-aware-packet-001 installedBuildReceiptId=installed-build-receipt-001 artifactIdentityId=artifact-identity-001\""
            },
            .. request.Artifacts.Skip(1)
        ]
    });
    throw new InvalidOperationException("Install-aware concierge JSON string payload scope spoof did not fail.");
}
catch (ArgumentException ex) when (ex.Message.Contains("packet id", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await concierge.RenderAsync(request with
    {
        RenderingId = "install-aware-delimited-scope-spoof",
        Artifacts =
        [
            request.Artifacts[0] with
            {
                Payload = "installAwarePacketId=install-aware-packet-001-shadow installedBuildReceiptId=installed-build-receipt-001-shadow artifactIdentityId=artifact-identity-001-shadow artifact=release-video"
            },
            .. request.Artifacts.Skip(1)
        ]
    });
    throw new InvalidOperationException("Install-aware concierge delimited text scope spoof validation did not fail.");
}
catch (ArgumentException ex) when (ex.Message.Contains("packet id", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await concierge.RenderAsync(request with
    {
        RenderingId = "install-aware-unbounded-sibling-notes",
        Artifacts =
        [
            request.Artifacts[0] with
            {
                SiblingNoteRefs = ["note://one", "note://two", "note://three"]
            },
            .. request.Artifacts.Skip(1)
        ]
    });
    throw new InvalidOperationException("Install-aware concierge sibling note bounds validation did not fail.");
}
catch (ArgumentException ex) when (ex.Message.Contains("bounded", StringComparison.OrdinalIgnoreCase))
{
}

var nonJsonPayloadReceipt = await concierge.RenderAsync(request with
{
    RenderingId = "install-aware-non-json-scope-fallback",
    Artifacts = request.Artifacts
        .Select(static artifact => artifact with
        {
            Payload = $"installAwarePacketId=install-aware-packet-001 installedBuildReceiptId=installed-build-receipt-001 artifactIdentityId=artifact-identity-001 artifact={artifact.CompanionRef}"
        })
        .ToArray()
});
Assert(nonJsonPayloadReceipt.Artifacts.Count == 9, "Non-JSON install-aware concierge payloads should still render when they carry the install-aware scope text.");

var mixedCaseKeyedPayloadReceipt = await concierge.RenderAsync(request with
{
    RenderingId = "install-aware-mixed-case-keyed-scope",
    Artifacts = request.Artifacts
        .Select(static artifact => artifact with
        {
            Payload = $"InstallAwarePacketId=install-aware-packet-001 InstalledBuildReceiptId=installed-build-receipt-001 ArtifactIdentityId=artifact-identity-001 artifact={artifact.CompanionRef}"
        })
        .ToArray()
});
Assert(
    mixedCaseKeyedPayloadReceipt.Artifacts.Count == 9,
    "Mixed-case keyed install-aware payloads should still render when they carry the exact install-aware scope values.");

var paddedPayloadReceipt = await concierge.RenderAsync(request with
{
    RenderingId = "install-aware-padded-payload-scope",
    Artifacts = request.Artifacts
        .Select(static artifact => artifact with
        {
            Payload = $"{{\"installAwarePacketId\":\" install-aware-packet-001 \",\"installedBuildReceiptId\":\" installed-build-receipt-001 \",\"artifactIdentityId\":\" artifact-identity-001 \",\"artifact\":\"{artifact.CompanionRef}\"}}"
        })
        .ToArray()
});
Assert(paddedPayloadReceipt.Artifacts.Count == 9, "Install-aware concierge JSON payload scope values should normalize surrounding whitespace.");

var whitespaceNormalizedReceipt = await concierge.RenderAsync(request with
{
    RenderingId = " install-aware-whitespace-normalization ",
    InstallAwarePacketId = " install-aware-packet-001 ",
    InstalledBuildReceiptId = " installed-build-receipt-001 ",
    ArtifactIdentityId = " artifact-identity-001 ",
    Source = " install-aware-concierge-smoke ",
});
Assert(whitespaceNormalizedReceipt.RenderingId == "install-aware-whitespace-normalization", "Install-aware concierge rendering ids should normalize surrounding whitespace.");
Assert(whitespaceNormalizedReceipt.InstallAwarePacketId == "install-aware-packet-001", "Install-aware concierge packet ids should normalize surrounding whitespace before scope validation.");
Assert(whitespaceNormalizedReceipt.InstalledBuildReceiptId == "installed-build-receipt-001", "Install-aware concierge installed build receipt ids should normalize surrounding whitespace before scope validation.");
Assert(whitespaceNormalizedReceipt.ArtifactIdentityId == "artifact-identity-001", "Install-aware concierge artifact identity ids should normalize surrounding whitespace before scope validation.");
Assert(whitespaceNormalizedReceipt.Source == "install-aware-concierge-smoke", "Install-aware concierge sources should normalize surrounding whitespace.");
Assert(whitespaceNormalizedReceipt.Artifacts.Count == 9, "Whitespace-normalized install-aware concierge requests should still render every sibling.");

Console.WriteLine("Install-aware concierge smoke passed.");

static InstallAwareConciergeArtifactRenderRequest CreateArtifact(
    InstallAwareConciergeBundleKind bundleKind,
    InstallAwareConciergeArtifactRole role,
    string category,
    string outputFormat,
    string companionRef,
    IReadOnlyList<string> captionRefs,
    IReadOnlyList<string> previewRefs,
    IReadOnlyList<string> siblingNoteRefs,
    string deduplicationKey)
{
    return new InstallAwareConciergeArtifactRenderRequest(
        BundleKind: bundleKind,
        Role: role,
        Category: category,
        Payload: $"{{\"installAwarePacketId\":\"install-aware-packet-001\",\"installedBuildReceiptId\":\"installed-build-receipt-001\",\"artifactIdentityId\":\"artifact-identity-001\",\"artifact\":\"{bundleKind}-{role}\"}}",
        OutputFormat: outputFormat,
        CompanionRef: companionRef,
        CaptionRefs: captionRefs,
        PreviewRefs: previewRefs,
        SiblingNoteRefs: siblingNoteRefs,
        DeduplicationKey: deduplicationKey,
        CacheTtl: TimeSpan.FromMinutes(10),
        MaxBytes: 4096);
}

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
