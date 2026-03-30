using Chummer.Campaign.Contracts;
using Chummer.Media.Contracts;

namespace Chummer.Run.AI.Services.Assets;

public sealed record CreatorPublicationPlan(
    string PublicationId,
    string Title,
    string ArtifactId,
    string Visibility,
    string PublicationStatus,
    string NextAction,
    PacketFactoryRequest PacketRequest,
    PacketAttachmentBatchRequest AttachmentBatch,
    IReadOnlyList<string> EvidenceLines);

public interface ICreatorPublicationPlannerService
{
    CreatorPublicationPlan BuildPlan(CreatorPublicationProjection publication, BuildLabHandoffProjection? handoff = null);
}

public sealed class CreatorPublicationPlannerService : ICreatorPublicationPlannerService
{
    public CreatorPublicationPlan BuildPlan(CreatorPublicationProjection publication, BuildLabHandoffProjection? handoff = null)
    {
        ArgumentNullException.ThrowIfNull(publication);

        List<string> evidenceLines =
        [
            $"Provenance: {publication.ProvenanceSummary}",
            $"Trust band: {HumanizeTrustBand(publication.TrustBand)}",
            $"Discovery: {publication.DiscoverySummary}",
            $"Discoverable: {(publication.Discoverable ? "Yes" : "No")}",
            $"Ownership: {BuildOwnershipSummary(publication)}",
            $"State: {HumanizePublicationStatus(publication.PublicationStatus)}"
        ];

        if (!string.IsNullOrWhiteSpace(publication.TrustSummary))
        {
            evidenceLines.Add($"Trust posture: {publication.TrustSummary}");
        }

        if (!string.IsNullOrWhiteSpace(publication.ComparisonSummary))
        {
            evidenceLines.Add($"Compare by: {publication.ComparisonSummary}");
        }

        if (!string.IsNullOrWhiteSpace(publication.LineageSummary))
        {
            evidenceLines.Add($"Lineage: {publication.LineageSummary}");
        }

        if (!string.IsNullOrWhiteSpace(publication.NextSafeAction))
        {
            evidenceLines.Add($"Next safe action: {publication.NextSafeAction}");
        }

        if (!string.IsNullOrWhiteSpace(publication.CampaignReturnSummary))
        {
            evidenceLines.Add($"Campaign return: {publication.CampaignReturnSummary}");
        }

        if (!string.IsNullOrWhiteSpace(publication.SupportClosureSummary))
        {
            evidenceLines.Add($"Support closure: {publication.SupportClosureSummary}");
        }

        if (!string.IsNullOrWhiteSpace(publication.ModerationSummary))
        {
            evidenceLines.Add($"Moderation: {publication.ModerationSummary}");
        }

        if (publication.Watchouts is { Count: > 0 })
        {
            evidenceLines.AddRange(publication.Watchouts.Take(2).Select(static item => $"Watchout: {item}"));
        }

        List<string> references =
        [
            publication.PublicationId,
            publication.CampaignId,
            publication.ArtifactId
        ];

        List<PacketAttachmentRequest> attachments =
        [
            new(
                PacketAttachmentTargetKind.Export,
                publication.PublicationId,
                IsPublicCreatorPacket(publication) ? "Public creator packet" : "Creator publication status"),
            new(PacketAttachmentTargetKind.Export, publication.CampaignId, "Campaign publication shelf")
        ];

        if (IsPublicCreatorPacket(publication))
        {
            evidenceLines.Add($"Public route: /artifacts/creator/{publication.PublicationId}");
        }

        if (!string.IsNullOrWhiteSpace(publication.DossierId))
        {
            attachments.Add(new PacketAttachmentRequest(PacketAttachmentTargetKind.Export, publication.DossierId, "Dossier publication shelf"));
            references.Add(publication.DossierId);
        }

        if (handoff is not null)
        {
            references.Add(handoff.HandoffId);
            references.Add(handoff.ExplainEntryId);
            evidenceLines.AddRange(handoff.TradeoffLines.Take(2));
            evidenceLines.AddRange(handoff.ProgressionOutcomes.Take(2));
            if (!string.IsNullOrWhiteSpace(handoff.PlannerCoverageSummary))
            {
                evidenceLines.Add($"Planner coverage: {handoff.PlannerCoverageSummary}");
            }

            if (handoff.PlannerCoverageLines is { Count: > 0 })
            {
                evidenceLines.AddRange(handoff.PlannerCoverageLines.Take(2));
            }

            if (!string.IsNullOrWhiteSpace(handoff.NextSafeAction))
            {
                evidenceLines.Add($"Next safe action: {handoff.NextSafeAction}");
            }

            if (!string.IsNullOrWhiteSpace(handoff.CampaignReturnSummary))
            {
                evidenceLines.Add($"Campaign return: {handoff.CampaignReturnSummary}");
            }

            if (!string.IsNullOrWhiteSpace(handoff.SupportClosureSummary))
            {
                evidenceLines.Add($"Support closure: {handoff.SupportClosureSummary}");
            }

            foreach (PublicationSafeProjection output in handoff.Outputs)
            {
                if (!string.IsNullOrWhiteSpace(output.ArtifactId))
                {
                    references.Add(output.ArtifactId);
                }

                evidenceLines.Add($"Output kind ({output.Label}): {DescribeOutputKind(output)}");

                if (!string.IsNullOrWhiteSpace(output.OwnershipSummary))
                {
                    evidenceLines.Add($"Output ownership ({output.Label}): {output.OwnershipSummary}");
                }

                if (!string.IsNullOrWhiteSpace(output.PublicationSummary))
                {
                    evidenceLines.Add($"Output publication ({output.Label}): {output.PublicationSummary}");
                }

                if (!string.IsNullOrWhiteSpace(output.NextSafeAction))
                {
                    evidenceLines.Add($"Output next safe action ({output.Label}): {output.NextSafeAction}");
                }

                if (!string.IsNullOrWhiteSpace(output.ProvenanceSummary))
                {
                    evidenceLines.Add($"Output provenance ({output.Label}): {output.ProvenanceSummary}");
                }

                if (!string.IsNullOrWhiteSpace(output.AuditSummary))
                {
                    evidenceLines.Add($"Output audit ({output.Label}): {output.AuditSummary}");
                }

                attachments.Add(new PacketAttachmentRequest(
                    PacketAttachmentTargetKind.Export,
                    output.ProjectionId,
                    output.Label));
            }
        }

        PacketFactoryRequest packetRequest = new(
            Title: publication.Title,
            Subject: publication.Summary,
            References: references
                .Where(static item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            Attachments: attachments
                .Distinct()
                .ToArray());

        string nextAction = string.Equals(publication.PublicationStatus, "preview_ready", StringComparison.OrdinalIgnoreCase)
            ? "queue_review"
            : IsPublicCreatorPacket(publication)
                ? "share_public_creator_packet"
                : "refresh_publication_posture";

        return new CreatorPublicationPlan(
            PublicationId: publication.PublicationId,
            Title: publication.Title,
            ArtifactId: publication.ArtifactId,
            Visibility: publication.Visibility,
            PublicationStatus: publication.PublicationStatus,
            NextAction: nextAction,
            PacketRequest: packetRequest,
            AttachmentBatch: new PacketAttachmentBatchRequest(packetRequest.Attachments ?? Array.Empty<PacketAttachmentRequest>()),
            EvidenceLines: evidenceLines
                .Where(static item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    private static string BuildOwnershipSummary(CreatorPublicationProjection publication)
        => string.Equals(publication.Visibility, "private", StringComparison.OrdinalIgnoreCase)
            || string.Equals(publication.Visibility, "local_only", StringComparison.OrdinalIgnoreCase)
            ? "Ownership stays on the originating creator lane until the creator deliberately widens visibility."
            : $"{publication.Visibility} visibility keeps creator publication on one governed creator lane instead of forking a separate discovery record.";

    private static bool IsPublicCreatorPacket(CreatorPublicationProjection publication)
        => publication.Discoverable
            && string.Equals(publication.PublicationStatus, "published", StringComparison.OrdinalIgnoreCase);

    private static string HumanizePublicationStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Published";
        }

        return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(
            value.Replace('_', ' ').Replace('-', ' '));
    }

    private static string HumanizeTrustBand(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Draft";
        }

        return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(
            value.Replace('_', ' ').Replace('-', ' '));
    }

    private static string DescribeOutputKind(PublicationSafeProjection output)
    {
        string normalizedKind = output.Kind.Trim().ToLowerInvariant();

        if (normalizedKind.Contains("replay", StringComparison.Ordinal))
        {
            return "Replay timeline stays attached to the same governed creator packet for contested-turn review.";
        }

        if (normalizedKind.Contains("recap", StringComparison.Ordinal)
            || normalizedKind.Contains("after", StringComparison.Ordinal)
            || normalizedKind.Contains("downtime", StringComparison.Ordinal))
        {
            return "Recap-safe package stays attached to the same governed creator packet for return and publication follow-through.";
        }

        if (normalizedKind.Contains("dossier", StringComparison.Ordinal))
        {
            return "Living dossier handoff stays attached to the same governed creator packet.";
        }

        return $"{HumanizePublicationStatus(output.Kind)} output stays attached to the same governed creator packet.";
    }
}
