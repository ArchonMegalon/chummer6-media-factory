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
            publication.ProvenanceSummary,
            publication.DiscoverySummary,
            $"{publication.Visibility} visibility keeps creator publication subordinate to governed campaign truth."
        ];

        List<string> references =
        [
            publication.CampaignId,
            publication.ArtifactId
        ];

        List<PacketAttachmentRequest> attachments =
        [
            new(PacketAttachmentTargetKind.Export, publication.CampaignId, "Campaign publication shelf")
        ];

        if (!string.IsNullOrWhiteSpace(publication.DossierId))
        {
            attachments.Add(new PacketAttachmentRequest(PacketAttachmentTargetKind.Export, publication.DossierId, "Dossier publication shelf"));
            references.Add(publication.DossierId);
        }

        if (handoff is not null)
        {
            evidenceLines.AddRange(handoff.TradeoffLines.Take(2));
            evidenceLines.AddRange(handoff.ProgressionOutcomes.Take(2));

            foreach (PublicationSafeProjection output in handoff.Outputs)
            {
                if (!string.IsNullOrWhiteSpace(output.ArtifactId))
                {
                    references.Add(output.ArtifactId);
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
}
