using Chummer.Media.Contracts;
using System.Security.Cryptography;
using System.Text;

namespace Chummer.Run.AI.Services.Assets;

public interface IRunsiteOrientationBundleService
{
    Task<RunsiteOrientationBundleReceipt> RenderAsync(
        RunsiteOrientationBundleRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class RunsiteOrientationBundleService : IRunsiteOrientationBundleService
{
    public const string PreviewTruthPosture = "pre-session-orientation-only-not-tactical-truth";

    private readonly IMediaRenderJobService _jobs;

    public RunsiteOrientationBundleService(IMediaRenderJobService jobs)
    {
        _jobs = jobs;
    }

    public async Task<RunsiteOrientationBundleReceipt> RenderAsync(
        RunsiteOrientationBundleRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(request);
        var receipts = new List<RunsiteOrientationArtifactReceipt>(normalized.Artifacts.Count);

        foreach (var artifact in normalized.Artifacts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var status = await _jobs.EnqueueAsync(
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

            receipts.Add(new RunsiteOrientationArtifactReceipt(
                ReceiptId: BuildReceiptId(normalized, artifact),
                Role: artifact.Role,
                Category: artifact.Category,
                RouteSegmentId: artifact.RouteSegmentId,
                OutputFormat: artifact.OutputFormat,
                JobId: status.JobId,
                JobState: status.State,
                AssetId: status.AssetId,
                CacheTtl: status.CacheTtl));
        }

        return new RunsiteOrientationBundleReceipt(
            BundleId: normalized.BundleId,
            ApprovedRunsitePackId: normalized.ApprovedRunsitePackId,
            RouteSummaryId: normalized.RouteSummaryId,
            Source: normalized.Source,
            PreviewTruthPosture: PreviewTruthPosture,
            RequestedAtUtc: normalized.RequestedAtUtc,
            RenderedAtUtc: DateTimeOffset.UtcNow,
            Artifacts: receipts,
            HostClipReceiptIds: ReceiptIdsFor(receipts, RunsiteOrientationArtifactRole.HostClip),
            RoutePreviewReceiptIds: ReceiptIdsFor(receipts, RunsiteOrientationArtifactRole.RoutePreview),
            RoutePreviewArtifactReceipts: receipts
                .Where(static receipt => receipt.Role == RunsiteOrientationArtifactRole.RoutePreview)
                .Select(static receipt => new RunsiteRoutePreviewArtifactReceipt(
                    RouteSegmentId: receipt.RouteSegmentId,
                    ReceiptId: receipt.ReceiptId,
                    JobId: receipt.JobId,
                    JobState: receipt.JobState,
                    AssetId: receipt.AssetId,
                    CacheTtl: receipt.CacheTtl))
                .ToArray(),
            AudioCompanionReceiptIds: ReceiptIdsFor(receipts, RunsiteOrientationArtifactRole.AudioCompanion),
            TourSiblingReceiptIds: ReceiptIdsFor(receipts, RunsiteOrientationArtifactRole.TourSibling));
    }

    private static RunsiteOrientationBundleRequest Normalize(RunsiteOrientationBundleRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireText(request.BundleId, nameof(request.BundleId));
        RequireText(request.ApprovedRunsitePackId, nameof(request.ApprovedRunsitePackId));
        RequireText(request.RouteSummaryId, nameof(request.RouteSummaryId));
        RequireText(request.Source, nameof(request.Source));
        if (request.Artifacts.Count == 0)
        {
            throw new ArgumentException("At least one runsite orientation artifact is required.", nameof(request));
        }

        var artifacts = request.Artifacts.Select(NormalizeArtifact).ToArray();
        if (!artifacts.Any(static artifact => artifact.Role == RunsiteOrientationArtifactRole.HostClip))
        {
            throw new ArgumentException("Runsite orientation bundles require at least one host clip.", nameof(request));
        }

        if (!artifacts.Any(static artifact => artifact.Role == RunsiteOrientationArtifactRole.RoutePreview))
        {
            throw new ArgumentException("Runsite orientation bundles require at least one route preview.", nameof(request));
        }

        return request with
        {
            BundleId = request.BundleId.Trim(),
            ApprovedRunsitePackId = request.ApprovedRunsitePackId.Trim(),
            RouteSummaryId = request.RouteSummaryId.Trim(),
            Source = request.Source.Trim(),
            Artifacts = artifacts
        };
    }

    private static RunsiteOrientationArtifactRenderRequest NormalizeArtifact(RunsiteOrientationArtifactRenderRequest artifact)
    {
        RequireText(artifact.Category, nameof(artifact.Category));
        RequireText(artifact.Payload, nameof(artifact.Payload));
        RequireText(artifact.OutputFormat, nameof(artifact.OutputFormat));
        RequireText(artifact.RouteSegmentId, nameof(artifact.RouteSegmentId));
        RequireText(artifact.DeduplicationKey, nameof(artifact.DeduplicationKey));

        return artifact with
        {
            Category = artifact.Category.Trim(),
            Payload = artifact.Payload.Trim(),
            OutputFormat = artifact.OutputFormat.Trim(),
            RouteSegmentId = artifact.RouteSegmentId.Trim(),
            DeduplicationKey = artifact.DeduplicationKey.Trim()
        };
    }

    private static void RequireText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{name} is required.", name);
        }
    }

    private static MediaRenderJobType ToJobType(RunsiteOrientationArtifactRole role) =>
        role switch
        {
            RunsiteOrientationArtifactRole.HostClip => MediaRenderJobType.RunsiteHostClip,
            RunsiteOrientationArtifactRole.RoutePreview => MediaRenderJobType.RunsiteRoutePreview,
            RunsiteOrientationArtifactRole.AudioCompanion => MediaRenderJobType.RunsiteAudioCompanion,
            RunsiteOrientationArtifactRole.TourSibling => MediaRenderJobType.RunsiteTourSibling,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unsupported runsite orientation artifact role.")
        };

    private static string BuildScopedDeduplicationKey(
        RunsiteOrientationBundleRequest request,
        RunsiteOrientationArtifactRenderRequest artifact)
    {
        var fields = new[]
        {
            "runsite-orientation",
            request.ApprovedRunsitePackId,
            request.RouteSummaryId,
            request.BundleId,
            artifact.Role.ToString(),
            artifact.RouteSegmentId,
            artifact.Category,
            artifact.OutputFormat,
            artifact.DeduplicationKey
        };
        var input = string.Join("\n", fields.Select(static field => $"{field.Length}:{field}"));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return "runsite-orientation:" + Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string BuildReceiptId(
        RunsiteOrientationBundleRequest request,
        RunsiteOrientationArtifactRenderRequest artifact)
    {
        var input = string.Join(
            "\n",
            BuildScopedDeduplicationKey(request, artifact),
            BuildHashSegment("category", artifact.Category),
            BuildHashSegment("output-format", artifact.OutputFormat));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return "runsite_receipt_" + Convert.ToHexString(bytes).ToLowerInvariant()[..16];
    }

    private static string BuildHashSegment(string prefix, string value) =>
        string.Join(
            "\n",
            new[]
            {
                $"{prefix.Length}:{prefix}",
                $"{value.Length}:{value}"
            });

    private static IReadOnlyList<string> ReceiptIdsFor(
        IEnumerable<RunsiteOrientationArtifactReceipt> receipts,
        RunsiteOrientationArtifactRole role) =>
        receipts
            .Where(receipt => receipt.Role == role)
            .Select(static receipt => receipt.ReceiptId)
            .ToArray();
}
