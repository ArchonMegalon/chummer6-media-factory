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
            "PublicationRefs",
            "CaptionRefs",
            "PreviewRefs",
            "StructuredMediaRecipePublicationRefReceipt",
            "StructuredMediaRecipeCaptionRefReceipt",
            "StructuredMediaRecipePreviewRefReceipt",
            "PublicationRefReceipts",
            "CaptionRefReceipts",
            "PreviewRefReceipts",
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
            "BuildCaptionRefReceipts",
            "BuildPreviewRefReceipts",
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


if __name__ == "__main__":
    unittest.main()
