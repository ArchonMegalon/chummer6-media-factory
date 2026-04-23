using Chummer.Media.Contracts;
using System.Security.Cryptography;
using System.Text;

namespace Chummer.Run.AI.Services.Assets;

public interface IStructuredMediaRecipeExecutionService
{
    Task<StructuredMediaRecipeBundleReceipt> RenderAsync(
        StructuredMediaRecipeRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class StructuredMediaRecipeExecutionService : IStructuredMediaRecipeExecutionService
{
    private readonly IMediaRenderJobService _jobs;

    public StructuredMediaRecipeExecutionService(IMediaRenderJobService jobs)
    {
        _jobs = jobs;
    }

    public async Task<StructuredMediaRecipeBundleReceipt> RenderAsync(
        StructuredMediaRecipeRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(request);
        var receipts = new List<StructuredMediaRecipeArtifactReceipt>(normalized.Artifacts.Count);

        foreach (var artifact in normalized.Artifacts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var enqueued = await _jobs.EnqueueAsync(
                new MediaRenderJobEnqueueRequest(
                    JobType: ToJobType(artifact.Role),
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

            receipts.Add(new StructuredMediaRecipeArtifactReceipt(
                ReceiptId: BuildReceiptId(normalized, artifact),
                Role: artifact.Role,
                Category: artifact.Category,
                OutputFormat: artifact.OutputFormat,
                PublicationRef: artifact.PublicationRef,
                CaptionRefs: artifact.CaptionRefs,
                PreviewRefs: artifact.PreviewRefs,
                JobId: status.JobId,
                JobState: status.State,
                AssetId: status.AssetId,
                CacheTtl: status.CacheTtl,
                ApprovalState: status.ApprovalState,
                RetentionState: status.RetentionState,
                StorageClass: status.StorageClass));
        }

        return new StructuredMediaRecipeBundleReceipt(
            RecipeExecutionId: normalized.RecipeExecutionId,
            RecipeFamily: normalized.RecipeFamily,
            ApprovedSourcePackId: normalized.ApprovedSourcePackId,
            Source: normalized.Source,
            RequestedAtUtc: normalized.RequestedAtUtc,
            RenderedAtUtc: DateTimeOffset.UtcNow,
            Artifacts: receipts,
            VideoReceiptIds: ReceiptIdsFor(receipts, StructuredMediaRecipeArtifactRole.Video),
            AudioReceiptIds: ReceiptIdsFor(receipts, StructuredMediaRecipeArtifactRole.Audio),
            PreviewReceiptIds: ReceiptIdsFor(receipts, StructuredMediaRecipeArtifactRole.PreviewCard),
            PacketReceiptIds: ReceiptIdsFor(receipts, StructuredMediaRecipeArtifactRole.PacketBundle),
            JobIds: receipts
                .Select(static receipt => receipt.JobId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            PublicationRefs: receipts.Select(static receipt => receipt.PublicationRef).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            PublicationReadyRefs: BuildPublicationReadyRefs(receipts),
            CaptionRefs: receipts.SelectMany(static receipt => receipt.CaptionRefs).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            PreviewRefs: receipts.SelectMany(static receipt => receipt.PreviewRefs).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            RoleReceiptGroups: BuildRoleReceiptGroups(receipts),
            PublicationRefReceipts: BuildPublicationRefReceipts(receipts),
            CaptionRefReceipts: BuildCaptionRefReceipts(receipts),
            PreviewRefReceipts: BuildPreviewRefReceipts(receipts));
    }

    private static StructuredMediaRecipeRequest Normalize(StructuredMediaRecipeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireText(request.RecipeExecutionId, nameof(request.RecipeExecutionId));
        RequireText(request.ApprovedSourcePackId, nameof(request.ApprovedSourcePackId));
        RequireText(request.Source, nameof(request.Source));
        if (request.Artifacts.Count == 0)
        {
            throw new ArgumentException("At least one structured media recipe artifact is required.", nameof(request));
        }

        var artifacts = request.Artifacts.Select(NormalizeArtifact).ToArray();
        RequireRole(artifacts, StructuredMediaRecipeArtifactRole.Video, request);
        RequireRole(artifacts, StructuredMediaRecipeArtifactRole.Audio, request);
        RequireRole(artifacts, StructuredMediaRecipeArtifactRole.PreviewCard, request);
        RequireRole(artifacts, StructuredMediaRecipeArtifactRole.PacketBundle, request);
        RequireUniquePublicationRefs(artifacts);

        return request with
        {
            RecipeExecutionId = request.RecipeExecutionId.Trim(),
            ApprovedSourcePackId = request.ApprovedSourcePackId.Trim(),
            Source = request.Source.Trim(),
            Artifacts = artifacts
        };
    }

    private static StructuredMediaRecipeArtifactRequest NormalizeArtifact(StructuredMediaRecipeArtifactRequest artifact)
    {
        RequireText(artifact.Category, nameof(artifact.Category));
        RequireText(artifact.Payload, nameof(artifact.Payload));
        RequireText(artifact.OutputFormat, nameof(artifact.OutputFormat));
        RequireText(artifact.PublicationRef, nameof(artifact.PublicationRef));
        RequireText(artifact.DeduplicationKey, nameof(artifact.DeduplicationKey));

        var captionRefs = NormalizeRefs(artifact.CaptionRefs, nameof(artifact.CaptionRefs));
        var previewRefs = NormalizeRefs(artifact.PreviewRefs, nameof(artifact.PreviewRefs));
        if (artifact.Role is StructuredMediaRecipeArtifactRole.Video or StructuredMediaRecipeArtifactRole.Audio && captionRefs.Count == 0)
        {
            throw new ArgumentException("Video and audio recipe artifacts require at least one caption ref.", nameof(artifact));
        }

        if (artifact.Role is StructuredMediaRecipeArtifactRole.Video or StructuredMediaRecipeArtifactRole.PreviewCard or StructuredMediaRecipeArtifactRole.PacketBundle && previewRefs.Count == 0)
        {
            throw new ArgumentException("Video, preview-card, and packet-bundle recipe artifacts require at least one preview ref.", nameof(artifact));
        }

        return artifact with
        {
            Category = artifact.Category.Trim(),
            Payload = artifact.Payload.Trim(),
            OutputFormat = artifact.OutputFormat.Trim(),
            PublicationRef = artifact.PublicationRef.Trim(),
            CaptionRefs = captionRefs,
            PreviewRefs = previewRefs,
            DeduplicationKey = artifact.DeduplicationKey.Trim()
        };
    }

    private static void RequireRole(
        IReadOnlyCollection<StructuredMediaRecipeArtifactRequest> artifacts,
        StructuredMediaRecipeArtifactRole role,
        StructuredMediaRecipeRequest request)
    {
        if (!artifacts.Any(artifact => artifact.Role == role))
        {
            throw new ArgumentException($"Structured media recipes require at least one {role} artifact.", nameof(request));
        }
    }

    private static void RequireUniquePublicationRefs(IReadOnlyCollection<StructuredMediaRecipeArtifactRequest> artifacts)
    {
        var duplicate = artifacts
            .GroupBy(static artifact => artifact.PublicationRef, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"Structured media recipe publication refs must be unique per bundle: {duplicate.Key}.",
                nameof(StructuredMediaRecipeRequest.Artifacts));
        }
    }

    private static IReadOnlyList<StructuredMediaRecipeRoleReceiptGroup> BuildRoleReceiptGroups(
        IEnumerable<StructuredMediaRecipeArtifactReceipt> receipts) =>
        receipts
            .GroupBy(static receipt => receipt.Role)
            .OrderBy(static group => group.Key)
            .Select(static group =>
            {
                var rows = group
                    .OrderBy(static receipt => receipt.PublicationRef, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(static receipt => receipt.ReceiptId, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                return new StructuredMediaRecipeRoleReceiptGroup(
                    Role: group.Key,
                    ReceiptIds: rows
                        .Select(static receipt => receipt.ReceiptId)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                    JobIds: rows
                        .Select(static receipt => receipt.JobId)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                    PublicationRefs: rows
                        .Select(static receipt => receipt.PublicationRef)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                    CaptionRefs: rows
                        .SelectMany(static receipt => receipt.CaptionRefs)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                    PreviewRefs: rows
                        .SelectMany(static receipt => receipt.PreviewRefs)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                    ArtifactReceipts: BuildRefArtifactReceipts(rows));
            })
            .ToArray();

    private static IReadOnlyList<string> NormalizeRefs(IReadOnlyList<string> refs, string name)
    {
        ArgumentNullException.ThrowIfNull(refs, name);
        return refs
            .Select(static value => value?.Trim() ?? string.Empty)
            .Where(static value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void RequireText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{name} is required.", name);
        }
    }

    private static MediaRenderJobType ToJobType(StructuredMediaRecipeArtifactRole role) =>
        role switch
        {
            StructuredMediaRecipeArtifactRole.Video => MediaRenderJobType.StructuredRecipeVideo,
            StructuredMediaRecipeArtifactRole.Audio => MediaRenderJobType.StructuredRecipeAudio,
            StructuredMediaRecipeArtifactRole.PreviewCard => MediaRenderJobType.StructuredRecipePreviewCard,
            StructuredMediaRecipeArtifactRole.PacketBundle => MediaRenderJobType.StructuredRecipePacketBundle,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unsupported structured media recipe artifact role.")
        };

    private static string BuildScopedDeduplicationKey(
        StructuredMediaRecipeRequest request,
        StructuredMediaRecipeArtifactRequest artifact)
    {
        var fields = new[]
        {
            "structured-media-recipe",
            request.RecipeFamily.ToString(),
            request.ApprovedSourcePackId,
            request.RecipeExecutionId,
            artifact.Role.ToString(),
            artifact.Category,
            artifact.OutputFormat,
            artifact.PublicationRef,
            artifact.DeduplicationKey
        };
        var input = string.Join("\n", fields.Select(static field => $"{field.Length}:{field}"));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return "structured-media-recipe:" + Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string BuildReceiptId(
        StructuredMediaRecipeRequest request,
        StructuredMediaRecipeArtifactRequest artifact)
    {
        var input = string.Join(
            "\n",
            BuildScopedDeduplicationKey(request, artifact),
            string.Join("|", artifact.CaptionRefs),
            string.Join("|", artifact.PreviewRefs));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return "recipe_receipt_" + Convert.ToHexString(bytes).ToLowerInvariant()[..16];
    }

    private static IReadOnlyList<string> ReceiptIdsFor(
        IEnumerable<StructuredMediaRecipeArtifactReceipt> receipts,
        StructuredMediaRecipeArtifactRole role) =>
        receipts
            .Where(receipt => receipt.Role == role)
            .Select(static receipt => receipt.ReceiptId)
            .ToArray();

    private static IReadOnlyList<StructuredMediaRecipePublicationRefReceipt> BuildPublicationRefReceipts(
        IEnumerable<StructuredMediaRecipeArtifactReceipt> receipts) =>
        receipts
            .OrderBy(static receipt => receipt.Role)
            .ThenBy(static receipt => receipt.PublicationRef, StringComparer.OrdinalIgnoreCase)
            .Select(static receipt => new StructuredMediaRecipePublicationRefReceipt(
                Ref: receipt.PublicationRef,
                Role: receipt.Role,
                ReceiptId: receipt.ReceiptId,
                JobId: receipt.JobId,
                JobState: receipt.JobState,
                OutputFormat: receipt.OutputFormat,
                AssetId: receipt.AssetId,
                CacheTtl: receipt.CacheTtl,
                ApprovalState: receipt.ApprovalState,
                RetentionState: receipt.RetentionState,
                StorageClass: receipt.StorageClass))
            .ToArray();

    private static IReadOnlyList<StructuredMediaRecipePublicationReadyRef> BuildPublicationReadyRefs(
        IEnumerable<StructuredMediaRecipeArtifactReceipt> receipts) =>
        receipts
            .OrderBy(static receipt => receipt.Role)
            .ThenBy(static receipt => receipt.PublicationRef, StringComparer.OrdinalIgnoreCase)
            .Select(static receipt => new StructuredMediaRecipePublicationReadyRef(
                Ref: receipt.PublicationRef,
                Role: receipt.Role,
                ReceiptId: receipt.ReceiptId,
                JobId: receipt.JobId,
                JobState: receipt.JobState,
                OutputFormat: receipt.OutputFormat,
                CaptionRefs: receipt.CaptionRefs,
                PreviewRefs: receipt.PreviewRefs,
                AssetId: receipt.AssetId,
                CacheTtl: receipt.CacheTtl,
                ApprovalState: receipt.ApprovalState,
                RetentionState: receipt.RetentionState,
                StorageClass: receipt.StorageClass))
            .ToArray();

    private static IReadOnlyList<StructuredMediaRecipeCaptionRefReceipt> BuildCaptionRefReceipts(
        IEnumerable<StructuredMediaRecipeArtifactReceipt> receipts) =>
        receipts
            .SelectMany(static receipt => receipt.CaptionRefs.Select(captionRef => (captionRef, receipt)))
            .GroupBy(static item => item.captionRef, StringComparer.OrdinalIgnoreCase)
            .OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(static group => new StructuredMediaRecipeCaptionRefReceipt(
                Ref: group.Key,
                ReceiptIds: group
                    .Select(static item => item.receipt.ReceiptId)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                Roles: group
                    .Select(static item => item.receipt.Role)
                    .Distinct()
                    .OrderBy(static role => role)
                    .ToArray(),
                ArtifactReceipts: BuildRefArtifactReceipts(group.Select(static item => item.receipt))))
            .ToArray();

    private static IReadOnlyList<StructuredMediaRecipePreviewRefReceipt> BuildPreviewRefReceipts(
        IEnumerable<StructuredMediaRecipeArtifactReceipt> receipts) =>
        receipts
            .SelectMany(static receipt => receipt.PreviewRefs.Select(previewRef => (previewRef, receipt)))
            .GroupBy(static item => item.previewRef, StringComparer.OrdinalIgnoreCase)
            .OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(static group => new StructuredMediaRecipePreviewRefReceipt(
                Ref: group.Key,
                ReceiptIds: group
                    .Select(static item => item.receipt.ReceiptId)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                Roles: group
                    .Select(static item => item.receipt.Role)
                    .Distinct()
                    .OrderBy(static role => role)
                    .ToArray(),
                ArtifactReceipts: BuildRefArtifactReceipts(group.Select(static item => item.receipt))))
            .ToArray();

    private static IReadOnlyList<StructuredMediaRecipeRefArtifactReceipt> BuildRefArtifactReceipts(
        IEnumerable<StructuredMediaRecipeArtifactReceipt> receipts) =>
        receipts
            .OrderBy(static receipt => receipt.Role)
            .ThenBy(static receipt => receipt.PublicationRef, StringComparer.OrdinalIgnoreCase)
            .Select(static receipt => new StructuredMediaRecipeRefArtifactReceipt(
                ReceiptId: receipt.ReceiptId,
                Role: receipt.Role,
                PublicationRef: receipt.PublicationRef,
                JobId: receipt.JobId,
                JobState: receipt.JobState,
                OutputFormat: receipt.OutputFormat,
                AssetId: receipt.AssetId,
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
            var status = _jobs.Get(jobId);
            if (status is null)
            {
                throw new InvalidOperationException($"Structured media recipe job {jobId} disappeared before receipt emission.");
            }

            if (status.State is MediaRenderJobState.Succeeded)
            {
                return status;
            }

            if (status.State is MediaRenderJobState.Failed or MediaRenderJobState.Expired)
            {
                throw new InvalidOperationException($"Structured media recipe job {jobId} ended as {status.State}: {status.Error ?? "unknown"}");
            }

            await Task.Delay(20, cancellationToken);
        }

        throw new TimeoutException($"Structured media recipe job {jobId} did not finish before receipt emission.");
    }
}
