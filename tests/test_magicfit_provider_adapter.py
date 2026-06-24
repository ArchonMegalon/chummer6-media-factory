from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[1]


def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8")


class MagicFitProviderAdapterTests(unittest.TestCase):
    def test_magicfit_provider_files_exist_and_encode_candidate_only_boundary(self):
        adapter = read("src/Chummer.Media.Factory.Runtime/Providers/MagicFit/MagicFitProviderAdapter.cs")
        options = read("src/Chummer.Media.Factory.Runtime/Providers/MagicFit/MagicFitProviderOptions.cs")
        contracts = read("src/Chummer.Media.Factory.Runtime/Providers/MagicFit/MagicFitRenderReceipt.cs")
        boundary = read("docs/MAGICFIT_PROVIDER_BOUNDARY.md")

        for token in (
            "public sealed class MagicFitProviderAdapter",
            "public interface IMagicFitProviderAdapter",
            "VerifyAsync",
            "RenderAsync",
            "DownloadAsync",
            "CandidateAssetOnly: true",
            "PublishAuthority: false",
            "may_publish_to_chummer_run: false",
            "may_send_email: false",
            "may_set_editorial_truth: false",
            "may_create_candidate_assets only after provider verification",
            "MagicFitProviderOptions",
            "FailClosedDefaults",
            "License Tier 5",
            "MagicFitProviderVerificationReceipt",
            "MagicFitDownloadedAssetReceipt",
            "MagicFitRenderReceipt",
            "CommercialUseVerified",
            "WatermarkFreeVerified",
            "SourceReceiptAssociationStatus",
            "Mp4Present",
        ):
            self.assertTrue(token in adapter or token in options or token in contracts, token)

        for token in (
            "Black Ledger Newsroom B-roll",
            "Faction promo scenes",
            "Short photoreal anchor tests",
            "Social video derivatives",
            "Text-to-video/image-to-video provider bake-off",
            "direct publish",
            "editorial truth",
            "product behavior proof",
            "private campaign data",
            "sourcebook text",
            "unreviewed public videos",
        ):
            self.assertIn(token, boundary)

    def test_magicfit_provider_adapter_stays_render_only(self):
        adapter = read("src/Chummer.Media.Factory.Runtime/Providers/MagicFit/MagicFitProviderAdapter.cs")
        forbidden = (
            "Smtp",
            "Emailit",
            "publish to chummer.run",
            "set editorial truth",
            "product proof authority",
        )

        for token in forbidden:
            self.assertNotIn(token, adapter)


if __name__ == "__main__":
    unittest.main()
