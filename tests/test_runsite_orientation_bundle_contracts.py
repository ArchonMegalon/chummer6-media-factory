from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[1]


def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8")


class RunsiteOrientationBundleContractsTests(unittest.TestCase):
    def test_runsite_orientation_contracts_are_first_class_render_receipts(self):
        contracts = read("src/Chummer.Media.Contracts/Compatibility/RunServices/MediaFactoryContracts.cs")
        runtime = read("src/Chummer.Media.Factory.Runtime/Assets/RunsiteOrientationBundleService.cs")

        for token in (
            "RunsiteOrientationBundleRequest",
            "RunsiteOrientationBundleReceipt",
            "RunsiteOrientationArtifactReceipt",
            "RunsiteRoutePreviewArtifactReceipt",
            "RunsiteOrientationBundleService",
            "HostClipReceiptIds",
            "RoutePreviewReceiptIds",
            "RoutePreviewArtifactReceipts",
            "AudioCompanionReceiptIds",
            "TourSiblingReceiptIds",
            "RunsiteHostClip",
            "RunsiteRoutePreview",
            "RunsiteAudioCompanion",
            "RunsiteTourSibling",
        ):
            self.assertTrue(token in contracts or token in runtime, token)

        self.assertIn("pre-session-orientation-only-not-tactical-truth", runtime)
        self.assertIn("MediaRenderJobEnqueueRequest", runtime)
        self.assertIn("RunsiteOrientationArtifactRole.HostClip", runtime)
        self.assertIn("RunsiteOrientationArtifactRole.RoutePreview", runtime)
        self.assertIn("RunsiteRoutePreviewArtifactReceipt", runtime)
        self.assertIn("RouteSegmentId: receipt.RouteSegmentId", runtime)
        self.assertIn("JobId: receipt.JobId", runtime)
        self.assertIn('artifact.Category', runtime)
        self.assertIn('artifact.OutputFormat', runtime)
        self.assertIn('field.Length', runtime)
        self.assertIn('BuildHashSegment("output-format", artifact.OutputFormat)', runtime)

    def test_runsite_orientation_slice_stays_render_only(self):
        runtime = read("src/Chummer.Media.Factory.Runtime/Assets/RunsiteOrientationBundleService.cs")

        forbidden_tokens = (
            "Chummer.Campaign.Contracts",
            "Chummer.Engine.Contracts",
            "session relay",
            "provider-routing",
        )

        for token in forbidden_tokens:
            self.assertNotIn(token, runtime)


if __name__ == "__main__":
    unittest.main()
