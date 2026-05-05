using Chummer.Media.Contracts;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Chummer.Run.AI.Services.Assets;

public interface IGmPrepPacketBundleService
{
    Task<GmPrepPacketBundleReceipt> RenderAsync(
        GmPrepPacketRenderRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class GmPrepPacketBundleService : IGmPrepPacketBundleService
{
    private readonly IMediaRenderJobService _jobs;

    public GmPrepPacketBundleService(IMediaRenderJobService jobs)
    {
        _jobs = jobs;
    }

    public async Task<GmPrepPacketBundleReceipt> RenderAsync(
        GmPrepPacketRenderRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(request);
        var entryReceipts = new List<GmPrepPacketEntryReceipt>(normalized.Entries.Count);
        var artifactReceipts = new List<GmPrepPacketArtifactReceipt>(normalized.Entries.Count * 3);
        DateTimeOffset? renderedAtUtc = null;

        foreach (var entry in normalized.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var packet = await RenderArtifactAsync(normalized, entry, GmPrepPacketArtifactRole.Packet, entry.Packet, cancellationToken);
            var preview = await RenderArtifactAsync(normalized, entry, GmPrepPacketArtifactRole.Preview, entry.Preview, cancellationToken);
            var artifacts = new List<RenderedGmPrepPacketArtifact>(3)
            {
                packet,
                preview
            };

            if (entry.Briefing is not null)
            {
                artifacts.Add(await RenderArtifactAsync(normalized, entry, GmPrepPacketArtifactRole.Briefing, entry.Briefing, cancellationToken));
            }

            artifactReceipts.AddRange(artifacts.Select(static artifact => artifact.Receipt));
            foreach (var artifact in artifacts)
            {
                renderedAtUtc = renderedAtUtc is { } currentRenderedAtUtc
                    ? MaxTimestamp(currentRenderedAtUtc, artifact.RenderedAtUtc)
                    : artifact.RenderedAtUtc;
            }

            entryReceipts.Add(new GmPrepPacketEntryReceipt(
                EntryReceiptId: BuildEntryReceiptId(normalized, entry),
                SubjectKind: entry.SubjectKind,
                SourceEntryId: entry.SourceEntryId,
                PacketRef: entry.PacketRef,
                PacketReceiptId: packet.Receipt.ReceiptId,
                PreviewReceiptId: preview.Receipt.ReceiptId,
                BriefingReceiptId: artifacts
                    .Where(static artifact => artifact.Receipt.Role == GmPrepPacketArtifactRole.Briefing)
                    .Select(static artifact => artifact.Receipt.ReceiptId)
                    .SingleOrDefault(),
                JobIds: artifacts
                    .Select(static artifact => artifact.Receipt.JobId)
                    .OrderBy(static jobId => jobId, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                ArtifactReceipts: artifacts
                    .OrderBy(static artifact => artifact.Receipt.Role)
                    .Select(static artifact => artifact.Receipt)
                    .ToArray()));
        }

        var subjectReceiptGroups = BuildSubjectReceiptGroups(normalized, entryReceipts);

        return new GmPrepPacketBundleReceipt(
            RenderingId: normalized.RenderingId,
            GovernedSourcePackId: normalized.GovernedSourcePackId,
            SourcePackRevisionId: normalized.SourcePackRevisionId,
            Source: normalized.Source,
            RequestedAtUtc: normalized.RequestedAtUtc,
            RenderedAtUtc: renderedAtUtc ?? normalized.RequestedAtUtc,
            Artifacts: artifactReceipts,
            EntryReceipts: entryReceipts,
            SubjectReceiptGroups: subjectReceiptGroups,
            PacketReceiptIds: ReceiptIdsFor(artifactReceipts, GmPrepPacketArtifactRole.Packet),
            PreviewReceiptIds: ReceiptIdsFor(artifactReceipts, GmPrepPacketArtifactRole.Preview),
            BriefingReceiptIds: ReceiptIdsFor(artifactReceipts, GmPrepPacketArtifactRole.Briefing),
            OppositionPacketReceiptIds: SubjectPacketReceiptIds(entryReceipts, GmPrepPacketSubjectKind.Opposition),
            ScenePacketReceiptIds: SubjectPacketReceiptIds(entryReceipts, GmPrepPacketSubjectKind.Scene),
            PrepLibraryPacketReceiptIds: SubjectPacketReceiptIds(entryReceipts, GmPrepPacketSubjectKind.PrepLibraryEntry),
            PacketRefs: OrderedDistinct(entryReceipts.Select(static receipt => receipt.PacketRef)),
            JobIds: artifactReceipts
                .Select(static artifact => artifact.JobId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static jobId => jobId, StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    private async Task<RenderedGmPrepPacketArtifact> RenderArtifactAsync(
        GmPrepPacketRenderRequest request,
        GmPrepPacketEntryRenderRequest entry,
        GmPrepPacketArtifactRole role,
        GmPrepPacketArtifactRenderRequest artifact,
        CancellationToken cancellationToken)
    {
        var enqueued = await _jobs.EnqueueAsync(
            new MediaRenderJobEnqueueRequest(
                JobType: ToJobType(entry.SubjectKind, role),
                DeduplicationKey: BuildScopedDeduplicationKey(request, entry, role, artifact),
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
        var jobRenderedAtUtc = status.CompletedAtUtc ?? status.CreatedAtUtc;

        return new RenderedGmPrepPacketArtifact(
            Receipt: new GmPrepPacketArtifactReceipt(
                ReceiptId: BuildArtifactReceiptId(request, entry, role, artifact),
                SubjectKind: entry.SubjectKind,
                Role: role,
                SourceEntryId: entry.SourceEntryId,
                PacketRef: entry.PacketRef,
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
            RenderedAtUtc: jobRenderedAtUtc);
    }

    private static GmPrepPacketRenderRequest Normalize(GmPrepPacketRenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireText(request.RenderingId, nameof(request.RenderingId));
        RequireText(request.GovernedSourcePackId, nameof(request.GovernedSourcePackId));
        RequireText(request.SourcePackRevisionId, nameof(request.SourcePackRevisionId));
        RequireText(request.Source, nameof(request.Source));
        if (request.Entries is null)
        {
            throw new ArgumentException("GM prep packet entries are required.", nameof(request));
        }

        if (request.Entries.Count == 0)
        {
            throw new ArgumentException("At least one GM prep packet entry is required.", nameof(request));
        }

        if (request.Entries.Any(static entry => entry is null))
        {
            throw new ArgumentException("GM prep packet render entries cannot contain null entries.", nameof(request));
        }

        var entries = request.Entries.Select(NormalizeEntry).ToArray();
        var renderingId = request.RenderingId.Trim();
        var governedSourcePackId = request.GovernedSourcePackId.Trim();
        var sourcePackRevisionId = request.SourcePackRevisionId.Trim();
        var source = request.Source.Trim();
        var normalizedRequest = request with
        {
            RenderingId = renderingId,
            GovernedSourcePackId = governedSourcePackId,
            SourcePackRevisionId = sourcePackRevisionId,
            Source = source
        };
        RequireOppositionEntry(entries, normalizedRequest);
        RequireUniqueSourceEntries(entries, normalizedRequest);
        RequireUniquePacketRefs(entries, normalizedRequest);
        RequirePayloadScope(entries, normalizedRequest);

        return normalizedRequest with
        {
            Entries = entries
                .OrderBy(static entry => entry.SubjectKind)
                .ThenBy(static entry => entry.PacketRef, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static entry => entry.SourceEntryId, StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
    }

    private static GmPrepPacketEntryRenderRequest NormalizeEntry(GmPrepPacketEntryRenderRequest entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        RequireText(entry.SourceEntryId, nameof(entry.SourceEntryId));
        RequireText(entry.PacketRef, nameof(entry.PacketRef));
        if (entry.Packet is null || entry.Preview is null)
        {
            throw new ArgumentException(
                "GM prep packet entries require packet and preview artifacts.",
                nameof(entry));
        }

        return entry with
        {
            SourceEntryId = entry.SourceEntryId.Trim(),
            PacketRef = entry.PacketRef.Trim(),
            Packet = NormalizeArtifact(entry.Packet, nameof(entry.Packet)),
            Preview = NormalizeArtifact(entry.Preview, nameof(entry.Preview)),
            Briefing = entry.Briefing is null ? null : NormalizeArtifact(entry.Briefing, nameof(entry.Briefing))
        };
    }

    private static GmPrepPacketArtifactRenderRequest NormalizeArtifact(
        GmPrepPacketArtifactRenderRequest artifact,
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

    private static void RequireOppositionEntry(
        IReadOnlyCollection<GmPrepPacketEntryRenderRequest> entries,
        GmPrepPacketRenderRequest request)
    {
        if (!entries.Any(static entry => entry.SubjectKind == GmPrepPacketSubjectKind.Opposition))
        {
            throw new ArgumentException(
                "GM prep packet renders require at least one opposition entry.",
                nameof(request));
        }
    }

    private static void RequireUniqueSourceEntries(
        IReadOnlyCollection<GmPrepPacketEntryRenderRequest> entries,
        GmPrepPacketRenderRequest request)
    {
        var duplicate = entries
            .GroupBy(static entry => (entry.SubjectKind, entry.SourceEntryId), SubjectEntryComparer.Instance)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"GM prep packet renders require a unique source entry per subject kind: {duplicate.Key.SubjectKind}/{duplicate.Key.SourceEntryId}.",
                nameof(request));
        }
    }

    private static void RequireUniquePacketRefs(
        IReadOnlyCollection<GmPrepPacketEntryRenderRequest> entries,
        GmPrepPacketRenderRequest request)
    {
        var duplicate = entries
            .GroupBy(static entry => entry.PacketRef, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"GM prep packet renders require packet refs to stay unique per render request: {duplicate.Key}.",
                nameof(request));
        }
    }

    private static void RequirePayloadScope(
        IReadOnlyCollection<GmPrepPacketEntryRenderRequest> entries,
        GmPrepPacketRenderRequest request)
    {
        foreach (var entry in entries)
        {
            foreach (var artifact in EnumerateArtifacts(entry))
            {
                if (!PayloadMatchesGovernedSourcePackId(artifact.Payload, request.GovernedSourcePackId))
                {
                    throw new ArgumentException(
                        "GM prep packet payloads must stay scoped to the governed source pack id.",
                        nameof(request));
                }

                if (!PayloadMatchesSourcePackRevisionId(artifact.Payload, request.SourcePackRevisionId))
                {
                    throw new ArgumentException(
                        "GM prep packet payloads must stay scoped to the source pack revision id.",
                        nameof(request));
                }

                if (!PayloadMatchesPacketRef(artifact.Payload, entry.PacketRef))
                {
                    throw new ArgumentException(
                        "GM prep packet payloads must stay scoped to the packet ref.",
                        nameof(request));
                }

                if (!PayloadMatchesSourceEntryId(artifact.Payload, entry.SourceEntryId))
                {
                    throw new ArgumentException(
                        "GM prep packet payloads must stay scoped to the source entry id.",
                        nameof(request));
                }
            }
        }
    }

    private static IEnumerable<GmPrepPacketArtifactRenderRequest> EnumerateArtifacts(GmPrepPacketEntryRenderRequest entry)
    {
        yield return entry.Packet;
        yield return entry.Preview;
        if (entry.Briefing is not null)
        {
            yield return entry.Briefing;
        }
    }

    private static bool PayloadMatchesGovernedSourcePackId(string payload, string governedSourcePackId)
    {
        var jsonScope = ParseJsonScopePayload(payload);
        if (jsonScope.IsJsonPayload)
        {
            return jsonScope.HasScopeFields &&
                   string.Equals(jsonScope.GovernedSourcePackId, governedSourcePackId, StringComparison.Ordinal);
        }

        string scopedGovernedSourcePackId;
        if (TryParseScopeFromTextPayload(payload, "governedSourcePackId", out scopedGovernedSourcePackId))
        {
            return string.Equals(scopedGovernedSourcePackId, governedSourcePackId, StringComparison.Ordinal);
        }

        return ContainsDelimitedScopeValue(payload, governedSourcePackId);
    }

    private static bool PayloadMatchesSourcePackRevisionId(string payload, string sourcePackRevisionId)
    {
        var jsonScope = ParseJsonScopePayload(payload);
        if (jsonScope.IsJsonPayload)
        {
            return jsonScope.HasScopeFields &&
                   string.Equals(jsonScope.SourcePackRevisionId, sourcePackRevisionId, StringComparison.Ordinal);
        }

        string scopedSourcePackRevisionId;
        if (TryParseScopeFromTextPayload(payload, "sourcePackRevisionId", out scopedSourcePackRevisionId))
        {
            return string.Equals(scopedSourcePackRevisionId, sourcePackRevisionId, StringComparison.Ordinal);
        }

        return ContainsDelimitedScopeValue(payload, sourcePackRevisionId);
    }

    private static bool PayloadMatchesPacketRef(string payload, string packetRef)
    {
        var jsonScope = ParseJsonScopePayload(payload);
        if (jsonScope.IsJsonPayload)
        {
            return jsonScope.HasScopeFields &&
                   string.Equals(jsonScope.PacketRef, packetRef, StringComparison.Ordinal);
        }

        string scopedPacketRef;
        if (TryParseScopeFromTextPayload(payload, "packetRef", out scopedPacketRef))
        {
            return string.Equals(scopedPacketRef, packetRef, StringComparison.Ordinal);
        }

        return ContainsDelimitedScopeValue(payload, packetRef);
    }

    private static bool PayloadMatchesSourceEntryId(string payload, string sourceEntryId)
    {
        var jsonScope = ParseJsonScopePayload(payload);
        if (jsonScope.IsJsonPayload)
        {
            return jsonScope.HasScopeFields &&
                   string.Equals(jsonScope.SourceEntryId, sourceEntryId, StringComparison.Ordinal);
        }

        string scopedSourceEntryId;
        if (TryParseScopeFromTextPayload(payload, "sourceEntryId", out scopedSourceEntryId))
        {
            return string.Equals(scopedSourceEntryId, sourceEntryId, StringComparison.Ordinal);
        }

        return ContainsDelimitedScopeValue(payload, sourceEntryId);
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

            if (!TryGetJsonStringProperty(document.RootElement, "governedSourcePackId", out var governedSourcePackId) ||
                !TryGetJsonStringProperty(document.RootElement, "sourcePackRevisionId", out var sourcePackRevisionId) ||
                !TryGetJsonStringProperty(document.RootElement, "packetRef", out var packetRef) ||
                !TryGetJsonStringProperty(document.RootElement, "sourceEntryId", out var sourceEntryId))
            {
                return JsonScopePayload.JsonPayloadMissingScopeFields;
            }

            return new JsonScopePayload(
                IsJsonPayload: true,
                HasScopeFields: true,
                GovernedSourcePackId: governedSourcePackId,
                SourcePackRevisionId: sourcePackRevisionId,
                PacketRef: packetRef,
                SourceEntryId: sourceEntryId);
        }
        catch (JsonException)
        {
            return JsonScopePayload.NotJson;
        }
    }

    private static bool TryGetJsonStringProperty(JsonElement element, string propertyName, out string value)
    {
        if (element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind is JsonValueKind.String)
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

    private static bool TryParseScopeFromTextPayload(
        string payload,
        string propertyName,
        out string value)
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

    private static bool ContainsDelimitedScopeValue(string payload, string expected)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(expected);

        var searchIndex = 0;
        while (searchIndex < payload.Length)
        {
            var matchIndex = payload.IndexOf(expected, searchIndex, StringComparison.Ordinal);
            if (matchIndex < 0)
            {
                return false;
            }

            var beforeIndex = matchIndex - 1;
            var afterIndex = matchIndex + expected.Length;
            if (IsScopeDelimiter(payload, beforeIndex) && IsScopeDelimiter(payload, afterIndex))
            {
                return true;
            }

            searchIndex = matchIndex + expected.Length;
        }

        return false;
    }

    private static bool IsScopeDelimiter(string payload, int index)
    {
        if (index < 0 || index >= payload.Length)
        {
            return true;
        }

        return !IsScopeTokenCharacter(payload[index]);
    }

    private static bool IsScopeTokenCharacter(char value) =>
        char.IsLetterOrDigit(value) || value is '-' or '_' or '/' or '.' or ':';

    private static string TrimScopeValue(string? value) => value?.Trim() ?? string.Empty;

    private static void RequireText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{name} is required.", name);
        }
    }

    private readonly record struct JsonScopePayload(
        bool IsJsonPayload,
        bool HasScopeFields,
        string GovernedSourcePackId,
        string SourcePackRevisionId,
        string PacketRef,
        string SourceEntryId)
    {
        public static JsonScopePayload NotJson => new(
            IsJsonPayload: false,
            HasScopeFields: false,
            GovernedSourcePackId: string.Empty,
            SourcePackRevisionId: string.Empty,
            PacketRef: string.Empty,
            SourceEntryId: string.Empty);

        public static JsonScopePayload JsonPayloadMissingScopeFields => new(
            IsJsonPayload: true,
            HasScopeFields: false,
            GovernedSourcePackId: string.Empty,
            SourcePackRevisionId: string.Empty,
            PacketRef: string.Empty,
            SourceEntryId: string.Empty);
    }

    private static MediaRenderJobType ToJobType(
        GmPrepPacketSubjectKind subjectKind,
        GmPrepPacketArtifactRole role) =>
        (subjectKind, role) switch
        {
            (GmPrepPacketSubjectKind.Opposition, GmPrepPacketArtifactRole.Packet) => MediaRenderJobType.GmPrepOppositionPacket,
            (GmPrepPacketSubjectKind.Opposition, GmPrepPacketArtifactRole.Preview) => MediaRenderJobType.GmPrepOppositionPreview,
            (GmPrepPacketSubjectKind.Opposition, GmPrepPacketArtifactRole.Briefing) => MediaRenderJobType.GmPrepOppositionBriefing,
            (GmPrepPacketSubjectKind.Scene, GmPrepPacketArtifactRole.Packet) => MediaRenderJobType.GmPrepScenePacket,
            (GmPrepPacketSubjectKind.Scene, GmPrepPacketArtifactRole.Preview) => MediaRenderJobType.GmPrepScenePreview,
            (GmPrepPacketSubjectKind.Scene, GmPrepPacketArtifactRole.Briefing) => MediaRenderJobType.GmPrepSceneBriefing,
            (GmPrepPacketSubjectKind.PrepLibraryEntry, GmPrepPacketArtifactRole.Packet) => MediaRenderJobType.GmPrepLibraryPacket,
            (GmPrepPacketSubjectKind.PrepLibraryEntry, GmPrepPacketArtifactRole.Preview) => MediaRenderJobType.GmPrepLibraryPreview,
            (GmPrepPacketSubjectKind.PrepLibraryEntry, GmPrepPacketArtifactRole.Briefing) => MediaRenderJobType.GmPrepLibraryBriefing,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unsupported GM prep packet artifact role.")
        };

    private static string BuildScopedDeduplicationKey(
        GmPrepPacketRenderRequest request,
        GmPrepPacketEntryRenderRequest entry,
        GmPrepPacketArtifactRole role,
        GmPrepPacketArtifactRenderRequest artifact)
    {
        var fields = new[]
        {
            "gm-prep-packet",
            request.GovernedSourcePackId,
            request.SourcePackRevisionId,
            request.RenderingId,
            entry.SubjectKind.ToString(),
            entry.SourceEntryId,
            entry.PacketRef,
            role.ToString(),
            artifact.Category,
            artifact.OutputFormat,
            artifact.DeduplicationKey
        };
        var input = string.Join("\n", fields.Select(static field => $"{field.Length}:{field}"));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return "gm-prep-packet:" + Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string BuildArtifactReceiptId(
        GmPrepPacketRenderRequest request,
        GmPrepPacketEntryRenderRequest entry,
        GmPrepPacketArtifactRole role,
        GmPrepPacketArtifactRenderRequest artifact)
    {
        var input = string.Join(
            "\n",
            BuildScopedDeduplicationKey(request, entry, role, artifact),
            BuildHashSegment("subject-kind", entry.SubjectKind.ToString()),
            BuildHashSegment("artifact-role", role.ToString()),
            BuildHashSegment("output-format", artifact.OutputFormat));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return "gm_prep_packet_receipt_" + Convert.ToHexString(bytes).ToLowerInvariant()[..16];
    }

    private static string BuildEntryReceiptId(
        GmPrepPacketRenderRequest request,
        GmPrepPacketEntryRenderRequest entry)
    {
        var fields = new[]
        {
            "gm-prep-packet-entry",
            request.GovernedSourcePackId,
            request.SourcePackRevisionId,
            request.RenderingId,
            entry.SubjectKind.ToString(),
            entry.SourceEntryId,
            entry.PacketRef
        };
        var input = string.Join("\n", fields.Select(static field => $"{field.Length}:{field}"));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return "gm_prep_packet_entry_" + Convert.ToHexString(bytes).ToLowerInvariant()[..16];
    }

    private static string BuildSubjectReceiptGroupId(
        GmPrepPacketRenderRequest request,
        GmPrepPacketSubjectKind subjectKind,
        IReadOnlyCollection<GmPrepPacketEntryReceipt> receipts)
    {
        var fields = new[]
        {
            "gm-prep-subject-group",
            request.GovernedSourcePackId,
            request.SourcePackRevisionId,
            request.RenderingId,
            subjectKind.ToString(),
            receipts.Count.ToString()
        }.Concat(receipts.Select(static receipt => receipt.EntryReceiptId));
        var input = string.Join("\n", fields.Select(static field => $"{field.Length}:{field}"));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return "gm_prep_subject_group_" + Convert.ToHexString(bytes).ToLowerInvariant()[..16];
    }

    private static string BuildHashSegment(string prefix, string value) =>
        string.Join(
            "\n",
            new[]
            {
                $"{prefix.Length}:{prefix}",
                $"{value.Length}:{value}"
            });

    private static IReadOnlyList<GmPrepPacketSubjectReceiptGroup> BuildSubjectReceiptGroups(
        GmPrepPacketRenderRequest request,
        IEnumerable<GmPrepPacketEntryReceipt> receipts) =>
        receipts
            .GroupBy(static receipt => receipt.SubjectKind)
            .OrderBy(static group => group.Key)
            .Select(group =>
            {
                var ordered = group
                    .OrderBy(static receipt => receipt.PacketRef, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(static receipt => receipt.SourceEntryId, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                return new GmPrepPacketSubjectReceiptGroup(
                    ReceiptId: BuildSubjectReceiptGroupId(request, group.Key, ordered),
                    SubjectKind: group.Key,
                    EntryReceiptIds: ordered.Select(static receipt => receipt.EntryReceiptId).ToArray(),
                    PacketRefs: OrderedDistinct(ordered.Select(static receipt => receipt.PacketRef)),
                    PacketReceiptIds: ordered.Select(static receipt => receipt.PacketReceiptId).ToArray(),
                    PreviewReceiptIds: ordered.Select(static receipt => receipt.PreviewReceiptId).ToArray(),
                    BriefingReceiptIds: ordered
                        .Select(static receipt => receipt.BriefingReceiptId)
                        .Where(static receiptId => !string.IsNullOrWhiteSpace(receiptId))
                        .Cast<string>()
                        .OrderBy(static receiptId => receiptId, StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                    JobIds: OrderedDistinct(ordered.SelectMany(static receipt => receipt.JobIds)),
                    ArtifactReceipts: ordered
                        .SelectMany(static receipt => receipt.ArtifactReceipts)
                        .OrderBy(static receipt => receipt.PacketRef, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(static receipt => receipt.Role)
                        .ThenBy(static receipt => receipt.ReceiptId, StringComparer.OrdinalIgnoreCase)
                        .ToArray());
            })
            .ToArray();

    private static IReadOnlyList<string> OrderedDistinct(IEnumerable<string> values) =>
        values
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IReadOnlyList<string> ReceiptIdsFor(
        IEnumerable<GmPrepPacketArtifactReceipt> receipts,
        GmPrepPacketArtifactRole role) =>
        receipts
            .Where(receipt => receipt.Role == role)
            .Select(static receipt => receipt.ReceiptId)
            .OrderBy(static receiptId => receiptId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IReadOnlyList<string> SubjectPacketReceiptIds(
        IEnumerable<GmPrepPacketEntryReceipt> receipts,
        GmPrepPacketSubjectKind subjectKind) =>
        receipts
            .Where(receipt => receipt.SubjectKind == subjectKind)
            .Select(static receipt => receipt.PacketReceiptId)
            .OrderBy(static receiptId => receiptId, StringComparer.OrdinalIgnoreCase)
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
                throw new InvalidOperationException($"GM prep packet job {jobId} disappeared before receipt emission.");
            }

            if (status.State is MediaRenderJobState.Succeeded)
            {
                return status;
            }

            if (status.State is MediaRenderJobState.Failed or MediaRenderJobState.Expired)
            {
                throw new InvalidOperationException($"GM prep packet job {jobId} ended as {status.State}: {status.Error ?? "unknown"}");
            }

            await Task.Delay(20, cancellationToken);
        }

        throw new TimeoutException($"GM prep packet job {jobId} did not finish before receipt emission.");
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
                $"GM prep packet job {status.JobId} succeeded without asset and lifecycle truth required for receipt emission.");
        }
    }

    private static DateTimeOffset MaxTimestamp(DateTimeOffset left, DateTimeOffset right) =>
        left >= right ? left : right;

    private sealed record RenderedGmPrepPacketArtifact(
        GmPrepPacketArtifactReceipt Receipt,
        DateTimeOffset RenderedAtUtc);

    private sealed class SubjectEntryComparer : IEqualityComparer<(GmPrepPacketSubjectKind SubjectKind, string SourceEntryId)>
    {
        public static SubjectEntryComparer Instance { get; } = new();

        public bool Equals(
            (GmPrepPacketSubjectKind SubjectKind, string SourceEntryId) x,
            (GmPrepPacketSubjectKind SubjectKind, string SourceEntryId) y) =>
            x.SubjectKind == y.SubjectKind &&
            StringComparer.OrdinalIgnoreCase.Equals(x.SourceEntryId, y.SourceEntryId);

        public int GetHashCode((GmPrepPacketSubjectKind SubjectKind, string SourceEntryId) obj) =>
            HashCode.Combine(obj.SubjectKind, StringComparer.OrdinalIgnoreCase.GetHashCode(obj.SourceEntryId));
    }
}
