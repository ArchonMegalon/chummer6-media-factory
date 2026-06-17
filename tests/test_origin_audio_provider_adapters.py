from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[1]


def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8")


class OriginAudioProviderAdapterTests(unittest.TestCase):
    def test_soundmadeseen_provider_files_exist_and_encode_candidate_only_boundary(self):
        adapter = read("src/Chummer.Media.Factory.Runtime/Providers/Soundmadeseen/SoundmadeseenProviderAdapter.cs")
        options = read("src/Chummer.Media.Factory.Runtime/Providers/Soundmadeseen/SoundmadeseenProviderOptions.cs")
        contracts = read("src/Chummer.Media.Factory.Runtime/Providers/Soundmadeseen/SoundmadeseenRenderRequest.cs")
        boundary = read("docs/SOUNDMADeseen_PROVIDER_BOUNDARY.md")

        for token in (
            "public sealed class SoundmadeseenProviderAdapter",
            "public interface ISoundmadeseenProviderAdapter",
            "VerifyAsync",
            "RenderAsync",
            "DownloadAsync",
            "CandidateAssetOnly: true",
            "PublishAuthority: false",
            "may_publish_to_chummer_run: false",
            "may_send_email: false",
            "may_set_editorial_truth: false",
            "SoundmadeseenProviderOptions",
            "FailClosedDefaults",
            "Narration Studio",
            "SoundmadeseenProviderVerificationReceipt",
            "SoundmadeseenDownloadedAssetReceipt",
            "SoundmadeseenRenderReceipt",
            "CommercialUseVerified",
            "DownloadVerified",
            "SourceReceiptAssociationStatus",
            "AudioPresent",
        ):
            self.assertTrue(token in adapter or token in options or token in contracts, token)

        for token in (
            "origin dossier audiobook drafts",
            "dossier brief narration",
            "candidate-only narration receipts",
            "direct publish",
            "editorial truth",
            "product behavior proof",
        ):
            self.assertIn(token, boundary)

    def test_unmixr_provider_files_exist_and_encode_candidate_only_boundary(self):
        adapter = read("src/Chummer.Media.Factory.Runtime/Providers/UnmixrAI/UnmixrProviderAdapter.cs")
        options = read("src/Chummer.Media.Factory.Runtime/Providers/UnmixrAI/UnmixrProviderOptions.cs")
        contracts = read("src/Chummer.Media.Factory.Runtime/Providers/UnmixrAI/UnmixrRenderRequest.cs")
        boundary = read("docs/UNMIXR_PROVIDER_BOUNDARY.md")

        for token in (
            "public sealed class UnmixrProviderAdapter",
            "public interface IUnmixrProviderAdapter",
            "VerifyAsync",
            "RenderAsync",
            "DownloadAsync",
            "CandidateAssetOnly: true",
            "PublishAuthority: false",
            "may_publish_to_chummer_run: false",
            "may_send_email: false",
            "may_set_editorial_truth: false",
            "UnmixrProviderOptions",
            "FailClosedDefaults",
            "UnmixrProviderVerificationReceipt",
            "UnmixrDownloadedAssetReceipt",
            "UnmixrRenderReceipt",
            "CommercialUseVerified",
            "DownloadVerified",
            "SourceReceiptAssociationStatus",
            "AudioPresent",
        ):
            self.assertTrue(token in adapter or token in options or token in contracts, token)

        for token in (
            "alternate voice narration drafts",
            "candidate-only narration receipts",
            "direct publish",
            "editorial truth",
            "product behavior proof",
        ):
            self.assertIn(token, boundary)


if __name__ == "__main__":
    unittest.main()
