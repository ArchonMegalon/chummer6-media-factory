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
            "duplicate-publication-ref",
            "Duplicate publication ref validation did not fail.",
        ):
            self.assertIn(token, smoke)

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


if __name__ == "__main__":
    unittest.main()
