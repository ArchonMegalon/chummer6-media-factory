using Chummer.Media.Contracts;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Chummer.Run.AI.Services.Assets;

public interface IStarterArtifactBundleService
{
    Task<StarterArtifactBundleReceipt> RenderAsync(
        StarterArtifactBundleRenderRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class StarterArtifactBundleService : IStarterArtifactBundleService
{
    public const int MaxFallbackLocales = 2;
    public const int MaxSupportNotesPerArtifact = 2;

    private readonly IMediaRenderJobService _jobs;

    public StarterArtifactBundleService(IMediaRenderJobService jobs)
    {
        _jobs = jobs;
    }

    public async Task<StarterArtifactBundleReceipt> RenderAsync(
        StarterArtifactBundleRenderRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(request);
        var receipts = new List<StarterArtifactReceipt>(normalized.Artifacts.Count);
        DateTimeOffset? renderedAtUtc = null;

        foreach (var artifact in normalized.Artifacts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var enqueued = await _jobs.EnqueueAsync(
                new MediaRenderJobEnqueueRequest(
                    JobType: ToJobType(artifact.BundleKind, artifact.Role),
                    DeduplicationKey: BuildScopedDeduplicationKey(normalized, artifact),
                    Category: artifact.Category,
                    Payload: artifact.Payload,
                    Source: normalized.Source,
                    CacheTtl: artifact.CacheTtl,
                    MaxBytes: artifact.MaxBytes,
                    RequiresApproval: artifact.RequiresApproval,
                    PersistOnApproval: artifact.PersistOnApproval,
                    AllowPersistentPinning: artifact.AllowPersistentPinning),
                cancellationToken);
            var status = await WaitForTerminalStatusAsync(enqueued.JobId, cancellationToken);
            ValidateReceiptStatus(status);
            var jobRenderedAtUtc = status.CompletedAtUtc ?? status.CreatedAtUtc;
            renderedAtUtc = renderedAtUtc is { } currentRenderedAtUtc
                ? MaxTimestamp(currentRenderedAtUtc, jobRenderedAtUtc)
                : jobRenderedAtUtc;

            receipts.Add(new StarterArtifactReceipt(
                ReceiptId: BuildReceiptId(normalized, artifact),
                BundleKind: artifact.BundleKind,
                Role: artifact.Role,
                Locale: artifact.Locale,
                Category: artifact.Category,
                OutputFormat: artifact.OutputFormat,
                ArtifactRef: artifact.ArtifactRef,
                CaptionRefs: artifact.CaptionRefs,
                PreviewRefs: artifact.PreviewRefs,
                SupportNoteRefs: artifact.SupportNoteRefs,
                JobId: status.JobId,
                JobState: status.State,
                AssetId: status.AssetId,
                AssetUrl: status.AssetUrl,
                CacheTtl: status.CacheTtl,
                ApprovalState: status.ApprovalState,
                RetentionState: status.RetentionState,
                StorageClass: status.StorageClass));
        }

        var fallbackLocales = OrderedDistinct(
            receipts
                .Select(static receipt => receipt.Locale)
                .Where(locale => !string.Equals(locale, normalized.RequestedLocale, StringComparison.OrdinalIgnoreCase)));

        return new StarterArtifactBundleReceipt(
            RenderingId: normalized.RenderingId,
            ApprovedStarterSourcePackId: normalized.ApprovedStarterSourcePackId,
            SourcePackRevisionId: normalized.SourcePackRevisionId,
            StarterLaneId: normalized.StarterLaneId,
            RequestedLocale: normalized.RequestedLocale,
            FallbackLocales: fallbackLocales,
            Source: normalized.Source,
            RequestedAtUtc: normalized.RequestedAtUtc,
            RenderedAtUtc: renderedAtUtc ?? normalized.RequestedAtUtc,
            Artifacts: receipts,
            StarterPrimerReceiptIds: ReceiptIdsFor(receipts, StarterArtifactBundleKind.StarterPrimer),
            FirstSessionBriefingReceiptIds: ReceiptIdsFor(receipts, StarterArtifactBundleKind.FirstSessionBriefing),
            SupportSafeOnboardingReceiptIds: ReceiptIdsFor(receipts, StarterArtifactBundleKind.SupportSafeOnboarding),
            RequestedLocaleReceiptIds: ReceiptIdsForLocale(receipts, normalized.RequestedLocale),
            FallbackLocaleReceiptIds: receipts
                .Where(receipt => !string.Equals(receipt.Locale, normalized.RequestedLocale, StringComparison.OrdinalIgnoreCase))
                .Select(static receipt => receipt.ReceiptId)
                .ToArray(),
            JobIds: receipts
                .Select(static receipt => receipt.JobId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            ArtifactRefs: OrderedDistinct(receipts.Select(static receipt => receipt.ArtifactRef)),
            ReadyRefs: BuildReadyRefs(receipts),
            CaptionRefs: OrderedDistinct(receipts.SelectMany(static receipt => receipt.CaptionRefs)),
            PreviewRefs: OrderedDistinct(receipts.SelectMany(static receipt => receipt.PreviewRefs)),
            SupportNoteRefs: OrderedDistinct(receipts.SelectMany(static receipt => receipt.SupportNoteRefs)),
            LocaleReceiptGroups: BuildLocaleReceiptGroups(receipts),
            BundleLocaleReceiptGroups: BuildBundleLocaleReceiptGroups(receipts),
            ArtifactRefReceipts: BuildArtifactRefReceipts(receipts),
            CaptionRefReceipts: BuildCaptionRefReceipts(receipts),
            PreviewRefReceipts: BuildPreviewRefReceipts(receipts),
            SupportNoteReceipts: BuildSupportNoteReceipts(receipts));
    }

    private static StarterArtifactBundleRenderRequest Normalize(StarterArtifactBundleRenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireText(request.RenderingId, nameof(request.RenderingId));
        RequireText(request.ApprovedStarterSourcePackId, nameof(request.ApprovedStarterSourcePackId));
        RequireText(request.SourcePackRevisionId, nameof(request.SourcePackRevisionId));
        RequireText(request.StarterLaneId, nameof(request.StarterLaneId));
        RequireText(request.RequestedLocale, nameof(request.RequestedLocale));
        RequireText(request.Source, nameof(request.Source));
        ArgumentNullException.ThrowIfNull(request.Artifacts);

        if (request.Artifacts.Count == 0)
        {
            throw new ArgumentException("At least one starter artifact is required.", nameof(request));
        }

        var artifacts = request.Artifacts
            .Select((artifact, index) => NormalizeArtifact(artifact, index))
            .ToArray();
        var normalizedRequest = request with
        {
            RenderingId = request.RenderingId.Trim(),
            ApprovedStarterSourcePackId = request.ApprovedStarterSourcePackId.Trim(),
            SourcePackRevisionId = request.SourcePackRevisionId.Trim(),
            StarterLaneId = request.StarterLaneId.Trim(),
            RequestedLocale = request.RequestedLocale.Trim(),
            Source = request.Source.Trim(),
        };

        RequirePayloadScope(artifacts, normalizedRequest);
        RequireLocaleBundles(artifacts, normalizedRequest);
        RequireUniqueArtifactRefs(artifacts);

        return normalizedRequest with
        {
            Artifacts = artifacts
                .OrderBy(static artifact => artifact.BundleKind)
                .ThenBy(static artifact => artifact.Locale, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static artifact => artifact.Role)
                .ThenBy(static artifact => artifact.ArtifactRef, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static artifact => artifact.OutputFormat, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static artifact => artifact.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static artifact => artifact.DeduplicationKey, StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
    }

    private static StarterArtifactRenderRequest NormalizeArtifact(StarterArtifactRenderRequest? artifact, int index)
    {
        if (artifact is null)
        {
            throw new ArgumentException($"Starter artifacts[{index}] is required.", nameof(StarterArtifactBundleRenderRequest.Artifacts));
        }

        RequireText(artifact.Locale, nameof(artifact.Locale));
        RequireText(artifact.Category, nameof(artifact.Category));
        RequireText(artifact.Payload, nameof(artifact.Payload));
        RequireText(artifact.OutputFormat, nameof(artifact.OutputFormat));
        RequireText(artifact.ArtifactRef, nameof(artifact.ArtifactRef));
        RequireText(artifact.DeduplicationKey, nameof(artifact.DeduplicationKey));

        var captionRefs = NormalizeRefs(artifact.CaptionRefs, nameof(artifact.CaptionRefs));
        var previewRefs = NormalizeRefs(artifact.PreviewRefs, nameof(artifact.PreviewRefs));
        var supportNoteRefs = NormalizeRefs(artifact.SupportNoteRefs, nameof(artifact.SupportNoteRefs));

        if (artifact.Role is StarterArtifactRole.Video or StarterArtifactRole.Audio && captionRefs.Count == 0)
        {
            throw new ArgumentException("Starter primer and first-session video/audio artifacts require at least one caption ref.", nameof(artifact));
        }

        if (artifact.Role is StarterArtifactRole.Video or StarterArtifactRole.PreviewCard && previewRefs.Count == 0)
        {
            throw new ArgumentException("Starter artifacts require at least one preview ref for video and preview-card siblings.", nameof(artifact));
        }

        if (artifact.BundleKind == StarterArtifactBundleKind.SupportSafeOnboarding && supportNoteRefs.Count == 0)
        {
            throw new ArgumentException("Support-safe onboarding artifacts require at least one support note ref.", nameof(artifact));
        }

        if (supportNoteRefs.Count > MaxSupportNotesPerArtifact)
        {
            throw new ArgumentException(
                $"Support-safe onboarding note refs stay bounded to at most {MaxSupportNotesPerArtifact} refs per artifact.",
                nameof(artifact));
        }

        return artifact with
        {
            Locale = artifact.Locale.Trim(),
            Category = artifact.Category.Trim(),
            Payload = artifact.Payload.Trim(),
            OutputFormat = artifact.OutputFormat.Trim(),
            ArtifactRef = artifact.ArtifactRef.Trim(),
            CaptionRefs = captionRefs,
            PreviewRefs = previewRefs,
            SupportNoteRefs = supportNoteRefs,
            DeduplicationKey = artifact.DeduplicationKey.Trim(),
        };
    }

    private static void RequireLocaleBundles(
        IReadOnlyCollection<StarterArtifactRenderRequest> artifacts,
        StarterArtifactBundleRenderRequest request)
    {
        var locales = artifacts
            .Select(static artifact => artifact.Locale)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static locale => locale, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (!locales.Contains(request.RequestedLocale, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Starter artifact rendering requires the requested locale bundle.", nameof(request));
        }

        var fallbackLocales = locales
            .Where(locale => !string.Equals(locale, request.RequestedLocale, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (fallbackLocales.Length > MaxFallbackLocales)
        {
            throw new ArgumentException(
                $"Starter artifact fallback locales stay bounded to at most {MaxFallbackLocales} locales.",
                nameof(request));
        }

        foreach (var locale in locales)
        {
            foreach (var bundleKind in Enum.GetValues<StarterArtifactBundleKind>())
            {
                foreach (var role in Enum.GetValues<StarterArtifactRole>())
                {
                    if (!artifacts.Any(artifact =>
                            artifact.BundleKind == bundleKind &&
                            artifact.Role == role &&
                            string.Equals(artifact.Locale, locale, StringComparison.OrdinalIgnoreCase)))
                    {
                        throw new ArgumentException(
                            $"Starter artifact locale bundles require {bundleKind} {role} siblings for locale {locale}.",
                            nameof(request));
                    }
                }
            }
        }
    }

    private static void RequireUniqueArtifactRefs(IReadOnlyCollection<StarterArtifactRenderRequest> artifacts)
    {
        var duplicate = artifacts
            .GroupBy(static artifact => artifact.ArtifactRef, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"Starter artifact refs must be unique per starter lane render request: {duplicate.Key}.",
                nameof(StarterArtifactBundleRenderRequest.Artifacts));
        }
    }

    private static void RequirePayloadScope(
        IReadOnlyCollection<StarterArtifactRenderRequest> artifacts,
        StarterArtifactBundleRenderRequest request)
    {
        var missingSourcePackId = artifacts.FirstOrDefault(
            artifact => !PayloadMatchesField(artifact.Payload, "approvedStarterSourcePackId", request.ApprovedStarterSourcePackId));
        if (missingSourcePackId is not null)
        {
            throw new ArgumentException(
                "Starter artifact payloads must stay scoped to the approved starter source pack id.",
                nameof(StarterArtifactBundleRenderRequest.Artifacts));
        }

        var missingRevisionId = artifacts.FirstOrDefault(
            artifact => !PayloadMatchesField(artifact.Payload, "sourcePackRevisionId", request.SourcePackRevisionId));
        if (missingRevisionId is not null)
        {
            throw new ArgumentException(
                "Starter artifact payloads must stay scoped to the source pack revision id.",
                nameof(StarterArtifactBundleRenderRequest.Artifacts));
        }

        var missingStarterLaneId = artifacts.FirstOrDefault(
            artifact => !PayloadMatchesField(artifact.Payload, "starterLaneId", request.StarterLaneId));
        if (missingStarterLaneId is not null)
        {
            throw new ArgumentException(
                "Starter artifact payloads must stay scoped to the starter lane id.",
                nameof(StarterArtifactBundleRenderRequest.Artifacts));
        }

        var missingLocale = artifacts.FirstOrDefault(
            artifact => !PayloadMatchesField(artifact.Payload, "locale", artifact.Locale));
        if (missingLocale is not null)
        {
            throw new ArgumentException(
                "Starter artifact payloads must stay scoped to each artifact locale.",
                nameof(StarterArtifactBundleRenderRequest.Artifacts));
        }
    }

    private static bool PayloadMatchesField(string payload, string propertyName, string expectedValue)
    {
        var jsonScope = ParseJsonScopePayload(payload);
        if (jsonScope.IsJsonPayload)
        {
            return jsonScope.Scope.TryGetValue(propertyName, out var value) &&
                   string.Equals(value, expectedValue, StringComparison.Ordinal);
        }

        string scopedValue;
        if (TryParseScopeFromTextPayload(payload, propertyName, out scopedValue))
        {
            return string.Equals(scopedValue, expectedValue, StringComparison.Ordinal);
        }

        return ContainsDelimitedScopeValue(payload, expectedValue);
    }

    private static JsonScopePayload ParseJsonScopePayload(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.ValueKind is not JsonValueKind.Object)
            {
                return JsonScopePayload.JsonPayloadMissingScopeFields;
            }

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var propertyName in new[] { "approvedStarterSourcePackId", "sourcePackRevisionId", "starterLaneId", "locale" })
            {
                if (!TryGetJsonStringProperty(document.RootElement, propertyName, out var value))
                {
                    return JsonScopePayload.JsonPayloadMissingScopeFields;
                }

                values[propertyName] = value;
            }

            return new JsonScopePayload(true, values);
        }
        catch (JsonException)
        {
            return JsonScopePayload.NotJson;
        }
    }

    private static bool TryGetJsonStringProperty(JsonElement element, string propertyName, out string value)
    {
        if (element.TryGetProperty(propertyName, out var property) && property.ValueKind is JsonValueKind.String)
        {
            value = TrimScopeValue(property.GetString());
            return value.Length > 0;
        }

        foreach (var candidate in element.EnumerateObject())
        {
            if (string.Equals(candidate.Name, propertyName, StringComparison.OrdinalIgnoreCase) &&
                candidate.Value.ValueKind is JsonValueKind.String)
            {
                value = TrimScopeValue(candidate.Value.GetString());
                return value.Length > 0;
            }
        }

        value = string.Empty;
        return false;
    }

    private static bool TryParseScopeFromTextPayload(string payload, string propertyName, out string value)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(propertyName);

        var pattern = $@"(?<![A-Za-z0-9_\-]){Regex.Escape(propertyName)}\s*[:=]\s*(?:""(?<value>[^""]+)""|'(?<value>[^']+)'|(?<value>[^\s,;|&]+))";
        var match = Regex.Match(payload, pattern, RegexOptions.CultureInvariant);
        if (match.Success)
        {
            value = TrimScopeValue(match.Groups["value"].Value);
            return value.Length > 0;
        }

        value = string.Empty;
        return false;
    }

    private static bool ContainsDelimitedScopeValue(string payload, string expectedValue)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(expectedValue);

        if (expectedValue.Length == 0)
        {
            return false;
        }

        for (var index = payload.IndexOf(expectedValue, StringComparison.Ordinal);
             index >= 0;
             index = payload.IndexOf(expectedValue, index + expectedValue.Length, StringComparison.Ordinal))
        {
            var beforeIndex = index - 1;
            var afterIndex = index + expectedValue.Length;
            var beforeOk = beforeIndex < 0 || IsScopeDelimiter(payload[beforeIndex]);
            var afterOk = afterIndex >= payload.Length || IsScopeDelimiter(payload[afterIndex]);
            if (beforeOk && afterOk)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsScopeDelimiter(char value) =>
        !IsScopeTokenCharacter(value);

    private static bool IsScopeTokenCharacter(char value) =>
        char.IsLetterOrDigit(value) || value is '-' or '_' or ':' or '/' or '.';

    private static string TrimScopeValue(string? value) =>
        value?.Trim() ?? string.Empty;

    private static IReadOnlyList<string> NormalizeRefs(IReadOnlyList<string> refs, string name)
    {
        ArgumentNullException.ThrowIfNull(refs, name);
        return refs
            .Select(TrimScopeValue)
            .Where(static value => value.Length > 0)
            .GroupBy(CanonicalizeGroupedRef, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.OrderBy(static value => value, StringComparer.Ordinal).First())
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static string CanonicalizeGroupedRef(string value) => value.Trim();

    private static void RequireText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{name} is required.", name);
        }
    }

    private static MediaRenderJobType ToJobType(StarterArtifactBundleKind bundleKind, StarterArtifactRole role) =>
        (bundleKind, role) switch
        {
            (StarterArtifactBundleKind.StarterPrimer, StarterArtifactRole.Video) => MediaRenderJobType.StarterPrimerVideo,
            (StarterArtifactBundleKind.StarterPrimer, StarterArtifactRole.Audio) => MediaRenderJobType.StarterPrimerAudio,
            (StarterArtifactBundleKind.StarterPrimer, StarterArtifactRole.PreviewCard) => MediaRenderJobType.StarterPrimerPreviewCard,
            (StarterArtifactBundleKind.FirstSessionBriefing, StarterArtifactRole.Video) => MediaRenderJobType.FirstSessionBriefingVideo,
            (StarterArtifactBundleKind.FirstSessionBriefing, StarterArtifactRole.Audio) => MediaRenderJobType.FirstSessionBriefingAudio,
            (StarterArtifactBundleKind.FirstSessionBriefing, StarterArtifactRole.PreviewCard) => MediaRenderJobType.FirstSessionBriefingPreviewCard,
            (StarterArtifactBundleKind.SupportSafeOnboarding, StarterArtifactRole.Video) => MediaRenderJobType.SupportSafeOnboardingVideo,
            (StarterArtifactBundleKind.SupportSafeOnboarding, StarterArtifactRole.Audio) => MediaRenderJobType.SupportSafeOnboardingAudio,
            (StarterArtifactBundleKind.SupportSafeOnboarding, StarterArtifactRole.PreviewCard) => MediaRenderJobType.SupportSafeOnboardingPreviewCard,
            _ => throw new ArgumentOutOfRangeException(nameof(bundleKind), bundleKind, "Unsupported starter artifact bundle role.")
        };

    private static string BuildScopedDeduplicationKey(
        StarterArtifactBundleRenderRequest request,
        StarterArtifactRenderRequest artifact)
    {
        var fields = new[]
        {
            "starter-artifacts",
            request.ApprovedStarterSourcePackId,
            request.SourcePackRevisionId,
            request.StarterLaneId,
            artifact.BundleKind.ToString(),
            artifact.Role.ToString(),
            artifact.Locale,
            artifact.Category,
            artifact.OutputFormat,
            artifact.ArtifactRef,
            artifact.DeduplicationKey
        };
        var input = string.Join("\n", fields.Select(static field => $"{field.Length}:{field}"));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return "starter-artifacts:" + Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string BuildReceiptId(
        StarterArtifactBundleRenderRequest request,
        StarterArtifactRenderRequest artifact)
    {
        var input = string.Join(
            "\n",
            BuildScopedDeduplicationKey(request, artifact),
            BuildHashSegment("locale", artifact.Locale),
            BuildHashSegment("output-format", artifact.OutputFormat),
            BuildRefHashSegment("caption", artifact.CaptionRefs),
            BuildRefHashSegment("preview", artifact.PreviewRefs),
            BuildRefHashSegment("support-note", artifact.SupportNoteRefs));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return "starter_receipt_" + Convert.ToHexString(bytes).ToLowerInvariant()[..16];
    }

    private static string BuildHashSegment(string prefix, string value) =>
        string.Join(
            "\n",
            new[]
            {
                $"{prefix.Length}:{prefix}",
                $"{value.Length}:{value}"
            });

    private static string BuildRefHashSegment(string prefix, IReadOnlyList<string> values)
    {
        var segments = new List<string>(values.Count + 1)
        {
            $"{prefix.Length}:{prefix}"
        };
        segments.AddRange(values.Select(static value => $"{value.Length}:{value}"));
        return string.Join("\n", segments);
    }

    private static IReadOnlyList<string> ReceiptIdsFor(
        IEnumerable<StarterArtifactReceipt> receipts,
        StarterArtifactBundleKind bundleKind) =>
        receipts
            .Where(receipt => receipt.BundleKind == bundleKind)
            .Select(static receipt => receipt.ReceiptId)
            .ToArray();

    private static IReadOnlyList<string> ReceiptIdsForLocale(
        IEnumerable<StarterArtifactReceipt> receipts,
        string locale) =>
        receipts
            .Where(receipt => string.Equals(receipt.Locale, locale, StringComparison.OrdinalIgnoreCase))
            .Select(static receipt => receipt.ReceiptId)
            .ToArray();

    private static IReadOnlyList<string> OrderedDistinct(IEnumerable<string> values) =>
        values
            .GroupBy(CanonicalizeGroupedRef, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.OrderBy(static value => value, StringComparer.Ordinal).First())
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static value => value, StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<StarterArtifactReadyRef> BuildReadyRefs(IReadOnlyList<StarterArtifactReceipt> receipts) =>
        receipts
            .OrderBy(static receipt => receipt.BundleKind)
            .ThenBy(static receipt => receipt.Locale, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static receipt => receipt.Role)
            .ThenBy(static receipt => receipt.ArtifactRef, StringComparer.OrdinalIgnoreCase)
            .Select(static receipt => new StarterArtifactReadyRef(
                Ref: receipt.ArtifactRef,
                BundleKind: receipt.BundleKind,
                Role: receipt.Role,
                Locale: receipt.Locale,
                Category: receipt.Category,
                ReceiptId: receipt.ReceiptId,
                JobId: receipt.JobId,
                JobState: receipt.JobState,
                OutputFormat: receipt.OutputFormat,
                CaptionRefs: receipt.CaptionRefs,
                PreviewRefs: receipt.PreviewRefs,
                SupportNoteRefs: receipt.SupportNoteRefs,
                AssetId: receipt.AssetId,
                AssetUrl: receipt.AssetUrl,
                CacheTtl: receipt.CacheTtl,
                ApprovalState: receipt.ApprovalState,
                RetentionState: receipt.RetentionState,
                StorageClass: receipt.StorageClass))
            .ToArray();

    private static IReadOnlyList<StarterArtifactLocaleReceiptGroup> BuildLocaleReceiptGroups(IReadOnlyList<StarterArtifactReceipt> receipts) =>
        receipts
            .GroupBy(static receipt => receipt.Locale, StringComparer.OrdinalIgnoreCase)
            .OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var ordered = group.OrderBy(static receipt => receipt.BundleKind)
                    .ThenBy(static receipt => receipt.Role)
                    .ThenBy(static receipt => receipt.ArtifactRef, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                return new StarterArtifactLocaleReceiptGroup(
                    Locale: ordered[0].Locale,
                    ReceiptIds: ordered.Select(static receipt => receipt.ReceiptId).ToArray(),
                    JobIds: ordered.Select(static receipt => receipt.JobId).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(static value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
                    ArtifactRefs: OrderedDistinct(ordered.Select(static receipt => receipt.ArtifactRef)),
                    BundleKinds: ordered.Select(static receipt => receipt.BundleKind).Distinct().ToArray(),
                    Roles: ordered.Select(static receipt => receipt.Role).Distinct().ToArray(),
                    CaptionRefs: OrderedDistinct(ordered.SelectMany(static receipt => receipt.CaptionRefs)),
                    PreviewRefs: OrderedDistinct(ordered.SelectMany(static receipt => receipt.PreviewRefs)),
                    SupportNoteRefs: OrderedDistinct(ordered.SelectMany(static receipt => receipt.SupportNoteRefs)),
                    ArtifactReceipts: BuildGroupedReceipts(ordered));
            })
            .ToArray();

    private static IReadOnlyList<StarterArtifactBundleLocaleReceiptGroup> BuildBundleLocaleReceiptGroups(IReadOnlyList<StarterArtifactReceipt> receipts) =>
        receipts
            .GroupBy(static receipt => (receipt.BundleKind, receipt.Locale))
            .OrderBy(static group => group.Key.BundleKind)
            .ThenBy(static group => group.Key.Locale, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var ordered = group.OrderBy(static receipt => receipt.Role)
                    .ThenBy(static receipt => receipt.ArtifactRef, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                return new StarterArtifactBundleLocaleReceiptGroup(
                    BundleKind: group.Key.BundleKind,
                    Locale: ordered[0].Locale,
                    ReceiptIds: ordered.Select(static receipt => receipt.ReceiptId).ToArray(),
                    JobIds: ordered.Select(static receipt => receipt.JobId).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(static value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
                    ArtifactRefs: OrderedDistinct(ordered.Select(static receipt => receipt.ArtifactRef)),
                    Roles: ordered.Select(static receipt => receipt.Role).Distinct().ToArray(),
                    CaptionRefs: OrderedDistinct(ordered.SelectMany(static receipt => receipt.CaptionRefs)),
                    PreviewRefs: OrderedDistinct(ordered.SelectMany(static receipt => receipt.PreviewRefs)),
                    SupportNoteRefs: OrderedDistinct(ordered.SelectMany(static receipt => receipt.SupportNoteRefs)),
                    ArtifactReceipts: BuildGroupedReceipts(ordered));
            })
            .ToArray();

    private static IReadOnlyList<StarterArtifactArtifactRefReceipt> BuildArtifactRefReceipts(IReadOnlyList<StarterArtifactReceipt> receipts) =>
        receipts
            .OrderBy(static receipt => receipt.BundleKind)
            .ThenBy(static receipt => receipt.Locale, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static receipt => receipt.Role)
            .ThenBy(static receipt => receipt.ArtifactRef, StringComparer.OrdinalIgnoreCase)
            .Select(static receipt => new StarterArtifactArtifactRefReceipt(
                Ref: receipt.ArtifactRef,
                BundleKind: receipt.BundleKind,
                Role: receipt.Role,
                Locale: receipt.Locale,
                Category: receipt.Category,
                ReceiptId: receipt.ReceiptId,
                JobId: receipt.JobId,
                JobState: receipt.JobState,
                OutputFormat: receipt.OutputFormat,
                CaptionRefs: receipt.CaptionRefs,
                PreviewRefs: receipt.PreviewRefs,
                SupportNoteRefs: receipt.SupportNoteRefs,
                AssetId: receipt.AssetId,
                AssetUrl: receipt.AssetUrl,
                CacheTtl: receipt.CacheTtl,
                ApprovalState: receipt.ApprovalState,
                RetentionState: receipt.RetentionState,
                StorageClass: receipt.StorageClass))
            .ToArray();

    private static IReadOnlyList<StarterArtifactCaptionRefReceipt> BuildCaptionRefReceipts(IReadOnlyList<StarterArtifactReceipt> receipts) =>
        BuildRefReceipts(
            receipts,
            static receipt => receipt.CaptionRefs,
            static (key, ordered) => new StarterArtifactCaptionRefReceipt(
                Ref: key,
                ReceiptIds: ordered.Select(static receipt => receipt.ReceiptId).ToArray(),
                JobIds: ordered.Select(static receipt => receipt.JobId).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(static value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
                ArtifactRefs: OrderedDistinct(ordered.Select(static receipt => receipt.ArtifactRef)),
                BundleKinds: ordered.Select(static receipt => receipt.BundleKind).Distinct().ToArray(),
                Roles: ordered.Select(static receipt => receipt.Role).Distinct().ToArray(),
                Locales: OrderedDistinct(ordered.Select(static receipt => receipt.Locale)),
                ArtifactReceipts: BuildGroupedReceipts(ordered)));

    private static IReadOnlyList<StarterArtifactPreviewRefReceipt> BuildPreviewRefReceipts(IReadOnlyList<StarterArtifactReceipt> receipts) =>
        BuildRefReceipts(
            receipts,
            static receipt => receipt.PreviewRefs,
            static (key, ordered) => new StarterArtifactPreviewRefReceipt(
                Ref: key,
                ReceiptIds: ordered.Select(static receipt => receipt.ReceiptId).ToArray(),
                JobIds: ordered.Select(static receipt => receipt.JobId).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(static value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
                ArtifactRefs: OrderedDistinct(ordered.Select(static receipt => receipt.ArtifactRef)),
                BundleKinds: ordered.Select(static receipt => receipt.BundleKind).Distinct().ToArray(),
                Roles: ordered.Select(static receipt => receipt.Role).Distinct().ToArray(),
                Locales: OrderedDistinct(ordered.Select(static receipt => receipt.Locale)),
                ArtifactReceipts: BuildGroupedReceipts(ordered)));

    private static IReadOnlyList<StarterArtifactSupportNoteReceipt> BuildSupportNoteReceipts(IReadOnlyList<StarterArtifactReceipt> receipts) =>
        BuildRefReceipts(
            receipts,
            static receipt => receipt.SupportNoteRefs,
            static (key, ordered) => new StarterArtifactSupportNoteReceipt(
                Ref: key,
                ReceiptIds: ordered.Select(static receipt => receipt.ReceiptId).ToArray(),
                JobIds: ordered.Select(static receipt => receipt.JobId).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(static value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
                ArtifactRefs: OrderedDistinct(ordered.Select(static receipt => receipt.ArtifactRef)),
                BundleKinds: ordered.Select(static receipt => receipt.BundleKind).Distinct().ToArray(),
                Roles: ordered.Select(static receipt => receipt.Role).Distinct().ToArray(),
                Locales: OrderedDistinct(ordered.Select(static receipt => receipt.Locale)),
                ArtifactReceipts: BuildGroupedReceipts(ordered)));

    private static IReadOnlyList<TReceipt> BuildRefReceipts<TReceipt>(
        IReadOnlyList<StarterArtifactReceipt> receipts,
        Func<StarterArtifactReceipt, IReadOnlyList<string>> selector,
        Func<string, StarterArtifactReceipt[], TReceipt> projector) =>
        receipts
            .SelectMany(receipt => selector(receipt).Select(refValue => (Ref: refValue, Receipt: receipt)))
            .GroupBy(static entry => entry.Ref, StringComparer.OrdinalIgnoreCase)
            .OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var ordered = group
                    .Select(static entry => entry.Receipt)
                    .OrderBy(static receipt => receipt.BundleKind)
                    .ThenBy(static receipt => receipt.Locale, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(static receipt => receipt.Role)
                    .ThenBy(static receipt => receipt.ArtifactRef, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var canonicalRef = group.OrderBy(static entry => entry.Ref, StringComparer.Ordinal).First().Ref;
                return projector(canonicalRef, ordered);
            })
            .ToArray();

    private static IReadOnlyList<StarterArtifactGroupedReceipt> BuildGroupedReceipts(IEnumerable<StarterArtifactReceipt> receipts) =>
        receipts
            .Select(static receipt => new StarterArtifactGroupedReceipt(
                ReceiptId: receipt.ReceiptId,
                BundleKind: receipt.BundleKind,
                Role: receipt.Role,
                Locale: receipt.Locale,
                Category: receipt.Category,
                ArtifactRef: receipt.ArtifactRef,
                JobId: receipt.JobId,
                JobState: receipt.JobState,
                OutputFormat: receipt.OutputFormat,
                CaptionRefs: receipt.CaptionRefs,
                PreviewRefs: receipt.PreviewRefs,
                SupportNoteRefs: receipt.SupportNoteRefs,
                AssetId: receipt.AssetId,
                AssetUrl: receipt.AssetUrl,
                CacheTtl: receipt.CacheTtl,
                ApprovalState: receipt.ApprovalState,
                RetentionState: receipt.RetentionState,
                StorageClass: receipt.StorageClass))
            .ToArray();

    private async Task<MediaRenderJobStatus> WaitForTerminalStatusAsync(
        string jobId,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var status = _jobs.Get(jobId) ?? throw new InvalidOperationException($"Media job {jobId} was not found.");
            if (status.State is MediaRenderJobState.Succeeded or MediaRenderJobState.Failed or MediaRenderJobState.Expired)
            {
                return status;
            }

            await Task.Delay(20, cancellationToken);
        }

        throw new TimeoutException($"Media job {jobId} did not reach a terminal state in time.");
    }

    private static void ValidateReceiptStatus(MediaRenderJobStatus status)
    {
        if (status.State is MediaRenderJobState.Failed or MediaRenderJobState.Expired)
        {
            throw new InvalidOperationException($"Starter artifact job {status.JobId} did not succeed: {status.State}.");
        }
    }

    private static DateTimeOffset MaxTimestamp(DateTimeOffset left, DateTimeOffset right) =>
        left >= right ? left : right;

    private sealed record JsonScopePayload(bool IsJsonPayload, IReadOnlyDictionary<string, string> Scope)
    {
        public static JsonScopePayload NotJson { get; } = new(false, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        public static JsonScopePayload JsonPayloadMissingScopeFields { get; } =
            new(true, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }
}
