using Chummer.Media.Contracts;
using System.Security.Cryptography;
using System.Text;

namespace Chummer.Run.AI.Services.Assets;

public interface ICampaignBriefingBundleService
{
    Task<CampaignBriefingBundleReceipt> RenderAsync(
        CampaignBriefingBundleRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class CampaignBriefingBundleService : ICampaignBriefingBundleService
{
    public const int MaxFallbackLocales = 2;

    private readonly IMediaRenderJobService _jobs;

    public CampaignBriefingBundleService(IMediaRenderJobService jobs)
    {
        _jobs = jobs;
    }

    public async Task<CampaignBriefingBundleReceipt> RenderAsync(
        CampaignBriefingBundleRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(request);
        var localeReceipts = new List<CampaignBriefingLocaleReceipt>(normalized.Entries.Count);
        var artifactReceipts = new List<CampaignBriefingArtifactReceipt>(normalized.Entries.Count * 3);
        DateTimeOffset? renderedAtUtc = null;

        foreach (var entry in normalized.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var media = await RenderArtifactAsync(normalized, entry, CampaignBriefingBundleArtifactKind.Media, entry.Media, cancellationToken);
            var caption = await RenderArtifactAsync(normalized, entry, CampaignBriefingBundleArtifactKind.Caption, entry.Caption, cancellationToken);
            var preview = await RenderArtifactAsync(normalized, entry, CampaignBriefingBundleArtifactKind.Preview, entry.Preview, cancellationToken);
            var entryArtifacts = new[] { media, caption, preview };

            artifactReceipts.AddRange(entryArtifacts.Select(static artifact => artifact.Receipt));
            foreach (var artifact in entryArtifacts)
            {
                renderedAtUtc = renderedAtUtc is { } currentRenderedAtUtc
                    ? MaxTimestamp(currentRenderedAtUtc, artifact.RenderedAtUtc)
                    : artifact.RenderedAtUtc;
            }

            localeReceipts.Add(new CampaignBriefingLocaleReceipt(
                EntryReceiptId: BuildEntryReceiptId(normalized, entry),
                Slot: entry.Slot,
                Locale: entry.Locale,
                IsFallbackSibling: entry.IsFallbackSibling,
                MediaReceiptId: media.Receipt.ReceiptId,
                CaptionReceiptId: caption.Receipt.ReceiptId,
                PreviewReceiptId: preview.Receipt.ReceiptId,
                JobIds: entryArtifacts
                    .Select(static artifact => artifact.Receipt.JobId)
                    .OrderBy(static jobId => jobId, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                ArtifactReceipts: entryArtifacts.Select(static artifact => artifact.Receipt).ToArray()));
        }

        var localeBundleReceipts = BuildLocaleBundleReceipts(localeReceipts);
        var requestedLocaleBundleReceipt = localeBundleReceipts.Single(static receipt => !receipt.IsFallbackSibling);
        var fallbackLocales = localeBundleReceipts
            .Where(static receipt => receipt.IsFallbackSibling)
            .OrderBy(static receipt => receipt.Locale, StringComparer.OrdinalIgnoreCase)
            .Select(static receipt => receipt.Locale)
            .ToArray();
        var fallbackLocaleBundleReceiptIds = localeBundleReceipts
            .Where(static receipt => receipt.IsFallbackSibling)
            .OrderBy(static receipt => receipt.Locale, StringComparer.OrdinalIgnoreCase)
            .Select(static receipt => receipt.ReceiptId)
            .ToArray();
        var fallbackSiblingReceipts = localeBundleReceipts
            .Where(static receipt => receipt.IsFallbackSibling)
            .OrderBy(static receipt => receipt.Locale, StringComparer.OrdinalIgnoreCase)
            .Select(receipt => new CampaignBriefingFallbackSiblingReceipt(
                ReceiptId: BuildFallbackSiblingReceiptId(normalized, receipt),
                Locale: receipt.Locale,
                ColdOpenEntryReceiptId: receipt.ColdOpenEntryReceiptId,
                MissionBriefingEntryReceiptId: receipt.MissionBriefingEntryReceiptId,
                ColdOpenMediaReceiptId: receipt.ColdOpenMediaReceiptId,
                MissionBriefingMediaReceiptId: receipt.MissionBriefingMediaReceiptId,
                ColdOpenCaptionReceiptId: receipt.ColdOpenCaptionReceiptId,
                MissionBriefingCaptionReceiptId: receipt.MissionBriefingCaptionReceiptId,
                ColdOpenPreviewReceiptId: receipt.ColdOpenPreviewReceiptId,
                MissionBriefingPreviewReceiptId: receipt.MissionBriefingPreviewReceiptId,
                CaptionReceiptIds: receipt.CaptionReceiptIds,
                PreviewReceiptIds: receipt.PreviewReceiptIds,
                JobIds: receipt.JobIds,
                ArtifactReceipts: receipt.ArtifactReceipts))
            .ToArray();

        return new CampaignBriefingBundleReceipt(
            BundleId: normalized.BundleId,
            CampaignPrimerId: normalized.CampaignPrimerId,
            MissionBriefingId: normalized.MissionBriefingId,
            RequestedLocale: normalized.RequestedLocale,
            Source: normalized.Source,
            RequestedAtUtc: normalized.RequestedAtUtc,
            RenderedAtUtc: renderedAtUtc ?? normalized.RequestedAtUtc,
            LocaleReceipts: localeReceipts,
            LocaleBundleReceipts: localeBundleReceipts,
            ArtifactReceipts: artifactReceipts,
            RequestedLocaleBundleReceiptId: requestedLocaleBundleReceipt.ReceiptId,
            FallbackLocales: fallbackLocales,
            FallbackLocaleBundleReceiptIds: fallbackLocaleBundleReceiptIds,
            ColdOpenReceiptIds: MediaReceiptIdsFor(normalized, localeReceipts, CampaignBriefingBundleSlot.ColdOpen),
            MissionBriefingReceiptIds: MediaReceiptIdsFor(normalized, localeReceipts, CampaignBriefingBundleSlot.MissionBriefing),
            CaptionReceiptIds: ArtifactReceiptIdsFor(normalized, artifactReceipts, CampaignBriefingBundleArtifactKind.Caption),
            PreviewReceiptIds: ArtifactReceiptIdsFor(normalized, artifactReceipts, CampaignBriefingBundleArtifactKind.Preview),
            FallbackSiblingReceipts: fallbackSiblingReceipts,
            JobIds: artifactReceipts
                .OrderBy(artifact => artifact.IsFallbackSibling)
                .ThenBy(artifact => IsNonRequestedLocale(normalized, artifact.Locale))
                .ThenBy(static artifact => artifact.Slot)
                .ThenBy(static artifact => artifact.Kind)
                .ThenBy(static artifact => artifact.Locale, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static artifact => artifact.ReceiptId, StringComparer.OrdinalIgnoreCase)
                .Select(static artifact => artifact.JobId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    private async Task<RenderedCampaignBriefingArtifact> RenderArtifactAsync(
        CampaignBriefingBundleRequest request,
        CampaignBriefingBundleEntryRequest entry,
        CampaignBriefingBundleArtifactKind kind,
        CampaignBriefingBundleArtifactRequest artifact,
        CancellationToken cancellationToken)
    {
        var enqueued = await _jobs.EnqueueAsync(
            new MediaRenderJobEnqueueRequest(
                JobType: ToJobType(entry.Slot, kind),
                DeduplicationKey: BuildScopedDeduplicationKey(request, entry, kind, artifact),
                Category: artifact.Category,
                Payload: artifact.Payload,
                Source: request.Source,
                CacheTtl: artifact.CacheTtl,
                MaxBytes: artifact.MaxBytes,
                RequiresApproval: artifact.RequiresApproval,
                PersistOnApproval: artifact.PersistOnApproval,
                AllowPersistentPinning: artifact.AllowPersistentPinning),
            cancellationToken);
        var status = await WaitForTerminalStatusAsync(enqueued.JobId, cancellationToken);
        ValidateReceiptStatus(status);

        return new RenderedCampaignBriefingArtifact(
            Receipt: new CampaignBriefingArtifactReceipt(
                ReceiptId: BuildArtifactReceiptId(request, entry, kind, artifact),
                Slot: entry.Slot,
                Kind: kind,
                Locale: entry.Locale,
                IsFallbackSibling: entry.IsFallbackSibling,
                Category: artifact.Category,
                OutputFormat: artifact.OutputFormat,
                JobId: status.JobId,
                JobState: status.State,
                AssetId: status.AssetId,
                AssetUrl: status.AssetUrl,
                CacheTtl: status.CacheTtl,
                ApprovalState: status.ApprovalState,
                RetentionState: status.RetentionState,
                StorageClass: status.StorageClass),
            RenderedAtUtc: status.CompletedAtUtc ?? status.CreatedAtUtc);
    }

    private static CampaignBriefingBundleRequest Normalize(CampaignBriefingBundleRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireText(request.BundleId, nameof(request.BundleId));
        RequireText(request.CampaignPrimerId, nameof(request.CampaignPrimerId));
        RequireText(request.MissionBriefingId, nameof(request.MissionBriefingId));
        RequireText(request.RequestedLocale, nameof(request.RequestedLocale));
        RequireText(request.Source, nameof(request.Source));
        if (request.Entries.Count == 0)
        {
            throw new ArgumentException("At least one campaign briefing bundle entry is required.", nameof(request));
        }

        var bundleId = request.BundleId.Trim();
        var campaignPrimerId = request.CampaignPrimerId.Trim();
        var missionBriefingId = request.MissionBriefingId.Trim();
        var requestedLocale = request.RequestedLocale.Trim();
        var source = request.Source.Trim();
        var entries = request.Entries.Select(NormalizeEntry).ToArray();
        RequireLocalePosture(entries, requestedLocale, request);
        RequireRequestedLocale(entries, requestedLocale, CampaignBriefingBundleSlot.ColdOpen, request);
        RequireRequestedLocale(entries, requestedLocale, CampaignBriefingBundleSlot.MissionBriefing, request);

        var fallbackLocales = entries
            .Where(static entry => entry.IsFallbackSibling)
            .Select(static entry => entry.Locale)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (fallbackLocales.Length > MaxFallbackLocales)
        {
            throw new ArgumentException(
                $"Campaign briefing bundles allow at most {MaxFallbackLocales} fallback locales.",
                nameof(request));
        }
        RequireUniqueLocaleSlotEntries(entries, request);
        RequireLocaleBundles(entries, request);

        return request with
        {
            BundleId = bundleId,
            CampaignPrimerId = campaignPrimerId,
            MissionBriefingId = missionBriefingId,
            RequestedLocale = requestedLocale,
            Source = source,
            Entries = OrderEntries(entries, requestedLocale)
        };
    }

    private static CampaignBriefingBundleEntryRequest NormalizeEntry(CampaignBriefingBundleEntryRequest entry)
    {
        RequireText(entry.Locale, nameof(entry.Locale));

        return entry with
        {
            Locale = entry.Locale.Trim(),
            Media = NormalizeArtifact(entry.Media, nameof(entry.Media)),
            Caption = NormalizeArtifact(entry.Caption, nameof(entry.Caption)),
            Preview = NormalizeArtifact(entry.Preview, nameof(entry.Preview))
        };
    }

    private static CampaignBriefingBundleArtifactRequest NormalizeArtifact(
        CampaignBriefingBundleArtifactRequest artifact,
        string name)
    {
        ArgumentNullException.ThrowIfNull(artifact, name);
        RequireText(artifact.Category, $"{name}.Category");
        RequireText(artifact.Payload, $"{name}.Payload");
        RequireText(artifact.OutputFormat, $"{name}.OutputFormat");
        RequireText(artifact.DeduplicationKey, $"{name}.DeduplicationKey");

        return artifact with
        {
            Category = artifact.Category.Trim(),
            Payload = artifact.Payload.Trim(),
            OutputFormat = artifact.OutputFormat.Trim(),
            DeduplicationKey = artifact.DeduplicationKey.Trim()
        };
    }

    private static void RequireRequestedLocale(
        IReadOnlyCollection<CampaignBriefingBundleEntryRequest> entries,
        string requestedLocale,
        CampaignBriefingBundleSlot slot,
        CampaignBriefingBundleRequest request)
    {
        if (!entries.Any(entry =>
                entry.Slot == slot
                && !entry.IsFallbackSibling
                && string.Equals(entry.Locale, requestedLocale, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException(
                $"Campaign briefing bundles require a requested-locale {slot} entry.",
                nameof(request));
        }
    }

    private static void RequireLocalePosture(
        IReadOnlyCollection<CampaignBriefingBundleEntryRequest> entries,
        string requestedLocale,
        CampaignBriefingBundleRequest request)
    {
        var requestedLocaleFallback = entries.FirstOrDefault(
            entry => entry.IsFallbackSibling &&
                     string.Equals(entry.Locale, requestedLocale, StringComparison.OrdinalIgnoreCase));
        if (requestedLocaleFallback is not null)
        {
            throw new ArgumentException(
                "Campaign briefing bundles keep the requested locale as the primary sibling instead of a fallback sibling.",
                nameof(request));
        }

        var nonRequestedPrimary = entries.FirstOrDefault(
            entry => !entry.IsFallbackSibling &&
                     !string.Equals(entry.Locale, requestedLocale, StringComparison.OrdinalIgnoreCase));
        if (nonRequestedPrimary is not null)
        {
            throw new ArgumentException(
                $"Campaign briefing bundles require non-requested locale {nonRequestedPrimary.Locale} to be marked as a fallback sibling.",
                nameof(request));
        }
    }

    private static void RequireUniqueLocaleSlotEntries(
        IReadOnlyCollection<CampaignBriefingBundleEntryRequest> entries,
        CampaignBriefingBundleRequest request)
    {
        var duplicate = entries
            .GroupBy(
                static entry => (entry.Locale, entry.Slot, entry.IsFallbackSibling),
                ValueTupleLocaleSlotComparer.Instance)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"Campaign briefing bundles allow only one {(duplicate.Key.IsFallbackSibling ? "fallback" : "primary")} {duplicate.Key.Slot} entry for locale {duplicate.Key.Locale}.",
                nameof(request));
        }
    }

    private static void RequireLocaleBundles(
        IReadOnlyCollection<CampaignBriefingBundleEntryRequest> entries,
        CampaignBriefingBundleRequest request)
    {
        foreach (var group in entries
                     .GroupBy(static entry => (entry.Locale, entry.IsFallbackSibling), ValueTupleLocaleSiblingComparer.Instance))
        {
            if (!group.Any(static entry => entry.Slot == CampaignBriefingBundleSlot.ColdOpen) ||
                !group.Any(static entry => entry.Slot == CampaignBriefingBundleSlot.MissionBriefing))
            {
                var bundleType = group.Key.IsFallbackSibling ? "fallback sibling" : "requested-locale";
                throw new ArgumentException(
                    $"Campaign briefing bundles require locale-matched cold-open and mission briefing siblings for {bundleType} locale {group.Key.Locale}.",
                    nameof(request));
            }
        }
    }

    private static void RequireText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{name} is required.", name);
        }
    }

    private static MediaRenderJobType ToJobType(
        CampaignBriefingBundleSlot slot,
        CampaignBriefingBundleArtifactKind kind) =>
        (slot, kind) switch
        {
            (_, CampaignBriefingBundleArtifactKind.Caption) => MediaRenderJobType.CampaignCaption,
            (_, CampaignBriefingBundleArtifactKind.Preview) => MediaRenderJobType.CampaignPreview,
            (CampaignBriefingBundleSlot.ColdOpen, CampaignBriefingBundleArtifactKind.Media) => MediaRenderJobType.CampaignColdOpen,
            (CampaignBriefingBundleSlot.MissionBriefing, CampaignBriefingBundleArtifactKind.Media) => MediaRenderJobType.CampaignMissionBriefing,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported campaign briefing artifact kind.")
        };

    private static string BuildScopedDeduplicationKey(
        CampaignBriefingBundleRequest request,
        CampaignBriefingBundleEntryRequest entry,
        CampaignBriefingBundleArtifactKind kind,
        CampaignBriefingBundleArtifactRequest artifact)
    {
        var fields = new[]
        {
            "campaign-briefing",
            request.CampaignPrimerId,
            request.MissionBriefingId,
            request.BundleId,
            request.RequestedLocale,
            entry.Slot.ToString(),
            entry.Locale,
            entry.IsFallbackSibling ? "fallback" : "primary",
            kind.ToString(),
            artifact.Category,
            artifact.OutputFormat,
            artifact.DeduplicationKey
        };
        var input = string.Join("\n", fields.Select(static field => $"{field.Length}:{field}"));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return "campaign-briefing:" + Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string BuildArtifactReceiptId(
        CampaignBriefingBundleRequest request,
        CampaignBriefingBundleEntryRequest entry,
        CampaignBriefingBundleArtifactKind kind,
        CampaignBriefingBundleArtifactRequest artifact)
    {
        var input = string.Join(
            "\n",
            BuildScopedDeduplicationKey(request, entry, kind, artifact),
            BuildHashSegment("locale", entry.Locale),
            BuildHashSegment("kind", kind.ToString()),
            BuildHashSegment("output-format", artifact.OutputFormat));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return "campaign_receipt_" + Convert.ToHexString(bytes).ToLowerInvariant()[..16];
    }

    private static string BuildEntryReceiptId(
        CampaignBriefingBundleRequest request,
        CampaignBriefingBundleEntryRequest entry)
    {
        var fields = new[]
        {
            "campaign-briefing-entry",
            request.BundleId,
            entry.Slot.ToString(),
            entry.Locale,
            entry.IsFallbackSibling ? "fallback" : "primary"
        };
        var input = string.Join("\n", fields.Select(static field => $"{field.Length}:{field}"));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return "campaign_bundle_" + Convert.ToHexString(bytes).ToLowerInvariant()[..16];
    }

    private static string BuildHashSegment(string prefix, string value) =>
        string.Join(
            "\n",
            new[]
            {
                $"{prefix.Length}:{prefix}",
                $"{value.Length}:{value}"
            });

    private static IReadOnlyList<CampaignBriefingBundleEntryRequest> OrderEntries(
        IEnumerable<CampaignBriefingBundleEntryRequest> entries,
        string requestedLocale) =>
        entries
            .OrderBy(static entry => entry.IsFallbackSibling)
            .ThenBy(entry => IsNonRequestedLocale(requestedLocale, entry.Locale))
            .ThenBy(static entry => entry.Locale, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static entry => entry.Slot)
            .ToArray();

    private static IReadOnlyList<CampaignBriefingLocaleBundleReceipt> BuildLocaleBundleReceipts(
        IEnumerable<CampaignBriefingLocaleReceipt> receipts) =>
        receipts
            .GroupBy(static receipt => (receipt.Locale, receipt.IsFallbackSibling), ValueTupleLocaleSiblingComparer.Instance)
            .OrderBy(static group => group.Key.IsFallbackSibling)
            .ThenBy(static group => group.Key.Locale, StringComparer.OrdinalIgnoreCase)
            .Select(static group =>
            {
                var coldOpen = group.Single(static receipt => receipt.Slot == CampaignBriefingBundleSlot.ColdOpen);
                var missionBriefing = group.Single(static receipt => receipt.Slot == CampaignBriefingBundleSlot.MissionBriefing);
                var orderedArtifacts = group
                    .SelectMany(static receipt => receipt.ArtifactReceipts)
                    .OrderBy(static receipt => receipt.Slot)
                    .ThenBy(static receipt => receipt.Kind)
                    .ThenBy(static receipt => receipt.ReceiptId, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                return new CampaignBriefingLocaleBundleReceipt(
                    ReceiptId: BuildLocaleBundleReceiptId(coldOpen, missionBriefing),
                    Locale: group.Key.Locale,
                    IsFallbackSibling: group.Key.IsFallbackSibling,
                    ColdOpenEntryReceiptId: coldOpen.EntryReceiptId,
                    MissionBriefingEntryReceiptId: missionBriefing.EntryReceiptId,
                    ColdOpenMediaReceiptId: coldOpen.MediaReceiptId,
                    MissionBriefingMediaReceiptId: missionBriefing.MediaReceiptId,
                    ColdOpenCaptionReceiptId: coldOpen.CaptionReceiptId,
                    MissionBriefingCaptionReceiptId: missionBriefing.CaptionReceiptId,
                    ColdOpenPreviewReceiptId: coldOpen.PreviewReceiptId,
                    MissionBriefingPreviewReceiptId: missionBriefing.PreviewReceiptId,
                    CaptionReceiptIds: OrderedDistinct(new[] { coldOpen.CaptionReceiptId, missionBriefing.CaptionReceiptId }),
                    PreviewReceiptIds: OrderedDistinct(new[] { coldOpen.PreviewReceiptId, missionBriefing.PreviewReceiptId }),
                    JobIds: OrderedDistinct(group.SelectMany(static receipt => receipt.JobIds)),
                    ArtifactReceipts: orderedArtifacts);
            })
            .ToArray();

    private static string BuildLocaleBundleReceiptId(
        CampaignBriefingLocaleReceipt coldOpen,
        CampaignBriefingLocaleReceipt missionBriefing)
    {
        var fields = new[]
        {
            "campaign-briefing-locale-bundle",
            coldOpen.Locale,
            coldOpen.IsFallbackSibling ? "fallback" : "primary",
            coldOpen.EntryReceiptId,
            missionBriefing.EntryReceiptId,
            coldOpen.MediaReceiptId,
            missionBriefing.MediaReceiptId
        };
        var input = string.Join("\n", fields.Select(static field => $"{field.Length}:{field}"));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return "campaign_locale_bundle_" + Convert.ToHexString(bytes).ToLowerInvariant()[..16];
    }

    private static string BuildFallbackSiblingReceiptId(
        CampaignBriefingBundleRequest request,
        CampaignBriefingLocaleBundleReceipt localeBundleReceipt)
    {
        var fields = new[]
        {
            "campaign-briefing-fallback-sibling",
            request.BundleId,
            request.RequestedLocale,
            localeBundleReceipt.Locale,
            localeBundleReceipt.ReceiptId,
            localeBundleReceipt.ColdOpenEntryReceiptId,
            localeBundleReceipt.MissionBriefingEntryReceiptId
        };
        var input = string.Join("\n", fields.Select(static field => $"{field.Length}:{field}"));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return "campaign_fallback_bundle_" + Convert.ToHexString(bytes).ToLowerInvariant()[..16];
    }

    private static IReadOnlyList<string> MediaReceiptIdsFor(
        CampaignBriefingBundleRequest request,
        IEnumerable<CampaignBriefingLocaleReceipt> receipts,
        CampaignBriefingBundleSlot slot) =>
        receipts
            .Where(receipt => receipt.Slot == slot)
            .OrderBy(static receipt => receipt.IsFallbackSibling)
            .ThenBy(receipt => IsNonRequestedLocale(request, receipt.Locale))
            .ThenBy(static receipt => receipt.Locale, StringComparer.OrdinalIgnoreCase)
            .Select(static receipt => receipt.MediaReceiptId)
            .ToArray();

    private static IReadOnlyList<string> ArtifactReceiptIdsFor(
        CampaignBriefingBundleRequest request,
        IEnumerable<CampaignBriefingArtifactReceipt> receipts,
        CampaignBriefingBundleArtifactKind kind) =>
        receipts
            .Where(receipt => receipt.Kind == kind)
            .OrderBy(static receipt => receipt.IsFallbackSibling)
            .ThenBy(receipt => IsNonRequestedLocale(request, receipt.Locale))
            .ThenBy(static receipt => receipt.Slot)
            .ThenBy(static receipt => receipt.Locale, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static receipt => receipt.ReceiptId, StringComparer.OrdinalIgnoreCase)
            .Select(static receipt => receipt.ReceiptId)
            .ToArray();

    private static IReadOnlyList<string> OrderedDistinct(IEnumerable<string> values) =>
        values
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private async Task<MediaRenderJobStatus> WaitForTerminalStatusAsync(
        string jobId,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var status = _jobs.Get(jobId);
            if (status is null)
            {
                throw new InvalidOperationException($"Campaign briefing job {jobId} disappeared before receipt emission.");
            }

            if (status.State is MediaRenderJobState.Succeeded)
            {
                return status;
            }

            if (status.State is MediaRenderJobState.Failed or MediaRenderJobState.Expired)
            {
                throw new InvalidOperationException($"Campaign briefing job {jobId} ended as {status.State}: {status.Error ?? "unknown"}");
            }

            await Task.Delay(20, cancellationToken);
        }

        throw new TimeoutException($"Campaign briefing job {jobId} did not finish before receipt emission.");
    }

    private static void ValidateReceiptStatus(MediaRenderJobStatus status)
    {
        if (string.IsNullOrWhiteSpace(status.AssetId) ||
            string.IsNullOrWhiteSpace(status.AssetUrl) ||
            status.ApprovalState is null ||
            status.RetentionState is null ||
            status.StorageClass is null)
        {
            throw new InvalidOperationException(
                $"Campaign briefing job {status.JobId} succeeded without asset and lifecycle truth required for receipt emission.");
        }
    }

    private static DateTimeOffset MaxTimestamp(DateTimeOffset left, DateTimeOffset right) =>
        left >= right ? left : right;

    private static bool IsNonRequestedLocale(CampaignBriefingBundleRequest request, string locale) =>
        !string.Equals(locale, request.RequestedLocale, StringComparison.OrdinalIgnoreCase);

    private static bool IsNonRequestedLocale(string requestedLocale, string locale) =>
        !string.Equals(locale, requestedLocale, StringComparison.OrdinalIgnoreCase);

    private sealed record RenderedCampaignBriefingArtifact(
        CampaignBriefingArtifactReceipt Receipt,
        DateTimeOffset RenderedAtUtc);

    private sealed class ValueTupleLocaleSiblingComparer : IEqualityComparer<(string Locale, bool IsFallbackSibling)>
    {
        public static ValueTupleLocaleSiblingComparer Instance { get; } = new();

        public bool Equals((string Locale, bool IsFallbackSibling) x, (string Locale, bool IsFallbackSibling) y) =>
            StringComparer.OrdinalIgnoreCase.Equals(x.Locale, y.Locale) &&
            x.IsFallbackSibling == y.IsFallbackSibling;

        public int GetHashCode((string Locale, bool IsFallbackSibling) obj) =>
            HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Locale), obj.IsFallbackSibling);
    }

    private sealed class ValueTupleLocaleSlotComparer : IEqualityComparer<(string Locale, CampaignBriefingBundleSlot Slot, bool IsFallbackSibling)>
    {
        public static ValueTupleLocaleSlotComparer Instance { get; } = new();

        public bool Equals(
            (string Locale, CampaignBriefingBundleSlot Slot, bool IsFallbackSibling) x,
            (string Locale, CampaignBriefingBundleSlot Slot, bool IsFallbackSibling) y) =>
            StringComparer.OrdinalIgnoreCase.Equals(x.Locale, y.Locale) &&
            x.Slot == y.Slot &&
            x.IsFallbackSibling == y.IsFallbackSibling;

        public int GetHashCode((string Locale, CampaignBriefingBundleSlot Slot, bool IsFallbackSibling) obj) =>
            HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Locale), obj.Slot, obj.IsFallbackSibling);
    }
}
