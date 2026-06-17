from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[1]


def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8")


class OriginDossierVideoRenderingTests(unittest.TestCase):
    def test_video_request_runtime_and_provider_are_present(self):
        runtime = read("src/Chummer.Media.Factory.Runtime/Assets/OriginDossierVideoRequestFileService.cs")
        provider = read("src/Chummer.Media.Factory.Runtime/Providers/VidBoard/VidBoardProviderAdapter.cs")
        contracts = read("src/Chummer.Media.Factory.Runtime/Providers/VidBoard/VidBoardRenderRequest.cs")

        for token in (
            "IOriginDossierVideoRequestFileService",
            "OriginDossierVideoRequestFileResult",
            "origin_dossier_video",
            "vidBoard",
            "RenderCandidateVideoAsync",
            "ffmpeg",
            "RenderedVideoPath",
            "VidBoardRenderRequest",
            "VidBoardRenderReceipt",
            "VidBoardDownloadedAssetReceipt",
            "DownloadAsync",
            "CandidateAssetOnly",
        ):
            self.assertTrue(token in runtime or token in provider or token in contracts, token)

    def test_video_smoke_covers_receipt_and_output_file(self):
        smoke = read("tests/OriginDossierVideoSmoke/Program.cs")
        for token in (
            "Origin dossier video request-file rendering should write a receipt beside the request.",
            "Origin dossier video request-file rendering should emit a playable candidate video.",
            "Origin dossier video candidate render should not be empty.",
            "Origin dossier video render should stay candidate-only.",
            "Origin dossier video download receipt should confirm the video lane.",
            "renderedVideoPath",
            "downloadReceipt",
        ):
            self.assertIn(token, smoke)


if __name__ == "__main__":
    unittest.main()
