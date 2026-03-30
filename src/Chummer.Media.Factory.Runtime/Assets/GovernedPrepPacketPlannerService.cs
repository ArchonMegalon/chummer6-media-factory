using Chummer.Campaign.Contracts;
using Chummer.Media.Contracts;

namespace Chummer.Run.AI.Services.Assets;

public sealed record GovernedPrepPacketProjection(
    string WorkspaceId,
    string CampaignId,
    string PacketId,
    string Kind,
    string Title,
    string Summary,
    string BindingSummary,
    bool Reusable,
    IReadOnlyList<string> SearchTerms,
    IReadOnlyList<string> EvidenceLines,
    DateTimeOffset UpdatedAtUtc);

public sealed record GovernedPrepPacketPlan(
    string WorkspaceId,
    string CampaignId,
    string PacketId,
    string PacketKind,
    string Title,
    bool Reusable,
    string NextAction,
    PacketFactoryRequest PacketRequest,
    PacketAttachmentBatchRequest AttachmentBatch,
    IReadOnlyList<string> EvidenceLines);

public interface IGovernedPrepPacketPlannerService
{
    GovernedPrepPacketPlan BuildPlan(GovernedPrepPacketProjection packet, GovernedPrepLaunchProjection? launch = null);
}

public sealed class GovernedPrepPacketPlannerService : IGovernedPrepPacketPlannerService
{
    public GovernedPrepPacketPlan BuildPlan(GovernedPrepPacketProjection packet, GovernedPrepLaunchProjection? launch = null)
    {
        ArgumentNullException.ThrowIfNull(packet);

        List<string> evidenceLines =
        [
            $"Binding: {packet.BindingSummary}",
            $"Reusable: {(packet.Reusable ? "Yes" : "No")}"
        ];

        string[] searchTerms = packet.SearchTerms
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToArray();
        if (searchTerms.Length > 0)
        {
            evidenceLines.Add($"Search terms: {string.Join(", ", searchTerms)}");
        }

        if (packet.EvidenceLines is { Count: > 0 })
        {
            evidenceLines.AddRange(packet.EvidenceLines.Take(3));
        }

        List<string> references =
        [
            packet.WorkspaceId,
            packet.CampaignId,
            packet.PacketId
        ];

        List<PacketAttachmentRequest> attachments =
        [
            new(PacketAttachmentTargetKind.Route, packet.CampaignId, "Campaign workspace"),
            new(PacketAttachmentTargetKind.Export, packet.WorkspaceId, "Workspace prep library"),
            new(PacketAttachmentTargetKind.Export, packet.PacketId, "Governed prep packet")
        ];

        if (launch is not null)
        {
            references.Add(launch.LaunchId);

            if (!string.IsNullOrWhiteSpace(launch.TargetRunId))
            {
                references.Add(launch.TargetRunId);
                attachments.Add(new PacketAttachmentRequest(
                    PacketAttachmentTargetKind.Route,
                    launch.TargetRunId,
                    string.IsNullOrWhiteSpace(launch.TargetRunTitle) ? "Target run" : launch.TargetRunTitle));
            }

            if (!string.IsNullOrWhiteSpace(launch.TargetSceneId))
            {
                references.Add(launch.TargetSceneId);
                attachments.Add(new PacketAttachmentRequest(
                    PacketAttachmentTargetKind.Route,
                    launch.TargetSceneId,
                    string.IsNullOrWhiteSpace(launch.TargetSceneTitle) ? "Target scene" : launch.TargetSceneTitle));
            }

            evidenceLines.Add($"Launch: {launch.Summary}");
            if (launch.AuditLines is { Count: > 0 })
            {
                evidenceLines.Add($"Audit: {launch.AuditLines[0]}");
            }

            string? launchTarget = BuildLaunchTargetSummary(launch);
            if (!string.IsNullOrWhiteSpace(launchTarget))
            {
                evidenceLines.Add($"Bound target: {launchTarget}");
            }

            attachments.Add(new PacketAttachmentRequest(
                PacketAttachmentTargetKind.Export,
                launch.LaunchId,
                "Governed prep launch receipt"));
        }
        else
        {
            evidenceLines.Add("Launch posture: No governed launch receipt is attached yet, so this packet stays reusable for the next campaign bind.");
        }

        PacketFactoryRequest packetRequest = new(
            Title: packet.Title,
            Subject: packet.Summary,
            References: references
                .Where(static item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            Attachments: attachments
                .Distinct()
                .ToArray());

        string nextAction = launch is not null
            ? "refresh_binding_posture"
            : packet.Reusable
                ? "launch_governed_packet"
                : "review_binding_scope";

        return new GovernedPrepPacketPlan(
            WorkspaceId: packet.WorkspaceId,
            CampaignId: packet.CampaignId,
            PacketId: packet.PacketId,
            PacketKind: packet.Kind,
            Title: packet.Title,
            Reusable: packet.Reusable,
            NextAction: nextAction,
            PacketRequest: packetRequest,
            AttachmentBatch: new PacketAttachmentBatchRequest(packetRequest.Attachments ?? Array.Empty<PacketAttachmentRequest>()),
            EvidenceLines: evidenceLines
                .Where(static item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    private static string? BuildLaunchTargetSummary(GovernedPrepLaunchProjection launch)
    {
        if (!string.IsNullOrWhiteSpace(launch.TargetRunTitle) && !string.IsNullOrWhiteSpace(launch.TargetSceneTitle))
        {
            return $"{launch.TargetRunTitle} / {launch.TargetSceneTitle}";
        }

        if (!string.IsNullOrWhiteSpace(launch.TargetRunTitle))
        {
            return launch.TargetRunTitle;
        }

        if (!string.IsNullOrWhiteSpace(launch.TargetSceneTitle))
        {
            return launch.TargetSceneTitle;
        }

        if (!string.IsNullOrWhiteSpace(launch.TargetRunId) && !string.IsNullOrWhiteSpace(launch.TargetSceneId))
        {
            return $"{launch.TargetRunId} / {launch.TargetSceneId}";
        }

        return !string.IsNullOrWhiteSpace(launch.TargetRunId)
            ? launch.TargetRunId
            : launch.TargetSceneId;
    }
}
