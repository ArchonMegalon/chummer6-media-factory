from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[1]


def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8")


class StructuredMediaRecipeExecutionTests(unittest.TestCase):
    def test_recipe_contracts_emit_role_receipts_and_public_refs(self):
        contracts = read("src/Chummer.Media.Contracts/Compatibility/RunServices/MediaFactoryContracts.cs")
        runtime = read("src/Chummer.Media.Factory.Runtime/Assets/StructuredMediaRecipeExecutionService.cs")

        for token in (
            "StructuredMediaRecipeRequest",
            "StructuredMediaRecipeBundleReceipt",
            "StructuredMediaRecipeArtifactReceipt",
            "StructuredMediaRecipeArtifactRole.Video",
            "StructuredMediaRecipeArtifactRole.Audio",
            "StructuredMediaRecipeArtifactRole.PreviewCard",
            "StructuredMediaRecipeArtifactRole.PacketBundle",
            "VideoReceiptIds",
            "AudioReceiptIds",
            "PreviewReceiptIds",
            "PacketReceiptIds",
            "JobIds",
            "PublicationRefs",
            "CaptionRefs",
            "PreviewRefs",
            "StructuredMediaRecipePublicationRefReceipt",
            "StructuredMediaRecipePublicationReadyRef",
            "StructuredMediaRecipeCaptionRefReceipt",
            "StructuredMediaRecipePreviewRefReceipt",
            "StructuredMediaRecipeRefArtifactReceipt",
            "StructuredMediaRecipeRoleReceiptGroup",
            "PublicationRefReceipts",
            "PublicationReadyRefs",
            "CaptionRefReceipts",
            "PreviewRefReceipts",
            "RoleReceiptGroups",
            "ArtifactReceipts",
            "ApprovalState",
            "RetentionState",
            "StorageClass",
            "AssetUrl",
            "JobIds",
            "PublicationRefs",
        ):
            self.assertTrue(token in contracts or token in runtime, token)

    def test_recipe_execution_maps_all_required_siblings_to_media_jobs(self):
        runtime = read("src/Chummer.Media.Factory.Runtime/Assets/StructuredMediaRecipeExecutionService.cs")

        for token in (
            "MediaRenderJobType.StructuredRecipeVideo",
            "MediaRenderJobType.StructuredRecipeAudio",
            "MediaRenderJobType.StructuredRecipePreviewCard",
            "MediaRenderJobType.StructuredRecipePacketBundle",
            "MediaRenderJobEnqueueRequest",
            "recipe_receipt_",
            "BuildPublicationRefReceipts",
            "BuildPublicationReadyRefs",
            "BuildCaptionRefReceipts",
            "BuildPreviewRefReceipts",
            "BuildRoleReceiptGroups",
            "BuildRefArtifactReceipts",
            "WaitForTerminalStatusAsync",
            "RequireUniquePublicationRefs",
            "publication refs must be unique",
            "PacketBundle && previewRefs.Count == 0",
            "artifact.OutputFormat",
            "artifact.PublicationRef",
            "field.Length",
            "structured-media-recipe:",
            "status.State is MediaRenderJobState.Succeeded",
            "ApprovalState: status.ApprovalState",
            "RetentionState: status.RetentionState",
            "StorageClass: status.StorageClass",
            "AssetUrl: status.AssetUrl",
            "AssetUrl: receipt.AssetUrl",
            "OrderedDistinct",
            "OrderBy(static jobId => jobId, StringComparer.OrdinalIgnoreCase)",
            "OrderBy(static receiptId => receiptId, StringComparer.OrdinalIgnoreCase)",
            "ReceiptIds: OrderedDistinct(rows.Select(static receipt => receipt.ReceiptId))",
            "JobIds: OrderedDistinct(rows.Select(static receipt => receipt.JobId))",
            "PublicationRefs: OrderedDistinct(rows.Select(static receipt => receipt.PublicationRef))",
            "CaptionRefs: OrderedDistinct(rows.SelectMany(static receipt => receipt.CaptionRefs))",
            "PreviewRefs: OrderedDistinct(rows.SelectMany(static receipt => receipt.PreviewRefs))",
            "ReceiptIds: OrderedDistinct(group.Select(static item => item.receipt.ReceiptId))",
            "JobIds: OrderedDistinct(group.Select(static item => item.receipt.JobId))",
            "PublicationRefs: OrderedDistinct(group.Select(static item => item.receipt.PublicationRef))",
            "var jobRenderedAtUtc = status.CompletedAtUtc ?? status.CreatedAtUtc",
            "renderedAtUtc = renderedAtUtc is { } currentRenderedAtUtc",
            "MaxTimestamp(currentRenderedAtUtc, jobRenderedAtUtc)",
            "RenderedAtUtc: renderedAtUtc",
        ):
            self.assertIn(token, runtime)

    def test_recipe_smoke_proves_output_ref_dedupe_collision_safety(self):
        smoke = read("tests/StructuredMediaRecipeSmoke/Program.cs")

        for token in (
            "recipe-execution-collision-proof",
            "public-proof://release/video-web",
            "caption://release/en-US.web.vtt",
            "preview://release/web-card",
            "Different video output refs must not collapse onto one recipe job",
            "recipe-execution-delimiter-collision-proof",
            "Delimiter-heavy recipe output refs must not collapse onto one recipe job",
            "recipe-execution-receipt-collision-proof",
            "public-proof://release/receipt-delimiter/a",
            "caption|variant",
            "Delimiter-heavy caption refs must not collapse onto one receipt id",
            "duplicate-publication-ref",
            "Duplicate publication ref validation did not fail.",
            "recipe-execution-reordered-proof",
            "Replay-safe dedupe should keep bundle rendered timestamps stable.",
            "Replay-safe dedupe should not let later request timestamps rewrite bundle rendered timestamps.",
            "Recipe receipt ordering should stay stable even when callers submit siblings in a different order.",
            "Publication refs should stay stable when callers reorder recipe siblings.",
            "Caption refs should stay stable when callers reorder recipe siblings.",
            "Preview refs should stay stable when callers reorder recipe siblings.",
            "Publication-ready refs should stay stable when callers reorder recipe siblings.",
            "Publication ref receipt rows should stay stable when callers reorder recipe siblings.",
            "Role receipt groups should stay stable when callers reorder recipe siblings.",
            "Caption ref receipt rows should stay stable when callers reorder recipe siblings.",
            "Preview ref receipt rows should stay stable when callers reorder recipe siblings.",
            "Executed recipe receipts must preserve concrete asset urls.",
            "Publication-ready refs must preserve ref, receipt, job, asset id, and asset url.",
            "Publication-ready refs must preserve signed asset urls.",
            "Publication ref rows must preserve asset urls.",
            "Caption artifact rows must preserve publication refs, job ids, asset urls, and retention truth.",
            "Shared caption ref should expose first-class media job ids.",
            "Shared caption ref should expose first-class publication refs.",
            "Preview ref rows must preserve completed job, asset urls, and retention truth.",
            "Shared preview ref should expose first-class media job ids.",
            "Shared preview ref should expose first-class publication refs.",
            "Publication ref rows must preserve caption and preview refs.",
            "Audio publication ref rows must preserve caption refs without inventing preview refs.",
            "Caption artifact rows must preserve per-artifact caption refs.",
            "Caption artifact rows must preserve per-artifact preview refs.",
            "Preview ref rows must preserve per-artifact caption refs.",
            "Preview ref rows must preserve per-artifact preview refs.",
        ):
            self.assertIn(token, smoke)

    def test_receipt_hashing_uses_length_prefixed_ref_segments(self):
        runtime = read("src/Chummer.Media.Factory.Runtime/Assets/StructuredMediaRecipeExecutionService.cs")

        for token in (
            'BuildRefHashSegment("caption", artifact.CaptionRefs)',
            'BuildRefHashSegment("preview", artifact.PreviewRefs)',
            'new[] { $"{prefix}:{refs.Count}" }',
            'refs.Select(static value => $"{value.Length}:{value}")',
        ):
            self.assertIn(token, runtime)

    def test_recipe_execution_stays_render_only(self):
        runtime = read("src/Chummer.Media.Factory.Runtime/Assets/StructuredMediaRecipeExecutionService.cs")

        forbidden_tokens = (
            "Chummer.Campaign.Contracts",
            "Chummer.Engine.Contracts",
            "provider-routing",
            "approval workflow",
            "delivery policy",
        )

        for token in forbidden_tokens:
            self.assertNotIn(token, runtime)

    def test_media_render_job_restore_reanchors_ttl_expiry(self):
        runtime = read("src/Chummer.Media.Factory.Runtime/Assets/MediaRenderJobService.cs")

        for token in (
            "public void RestoreBackup(MediaRenderJobBackupPackage backup)",
            "RestoredAtUtc = job.State == MediaRenderJobState.Succeeded ? DateTimeOffset.UtcNow : null",
            "row.RestoredAtUtc ?? row.CompletedAtUtc",
            "row.RestoredAtUtc = null;",
        ):
            self.assertIn(token, runtime)


if __name__ == "__main__":
    unittest.main()
