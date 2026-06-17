from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[1]


def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8")


class OriginDossierNarrationRenderingTests(unittest.TestCase):
    def test_origin_dossier_contracts_and_runtime_emit_audio_receipts(self):
        contracts = read("src/Chummer.Media.Contracts/Compatibility/RunServices/MediaFactoryContracts.cs")
        runtime = read("src/Chummer.Media.Factory.Runtime/Assets/OriginDossierNarrationRenderingService.cs")

        for token in (
            "OriginDossierNarrationRenderRequest",
            "OriginDossierNarrationRenderReceipt",
            "OriginDossierNarrationArtifactReceipt",
            "OriginDossierNarrationArtifactRole.CanonicalAudio",
            "OriginDossierNarrationArtifactRole.AlternateAudio",
            "OriginDossierCanonicalAudiobookAudio",
            "OriginDossierAlternateAudiobookAudio",
            "PrimaryAudioReceiptIds",
            "AlternateAudioReceiptIds",
            "CompanionReadyRefs",
            "OriginDossierNarrationReadyRef",
            "OriginDossierNarrationCompanionRefReceipt",
            "OriginDossierNarrationCaptionRefReceipt",
            "OriginDossierNarrationPreviewRefReceipt",
            "OriginDossierNarrationRoleReceiptGroup",
            "Provider",
            "ApprovedOriginPacketId",
            "OriginRevisionId",
        ):
            self.assertTrue(token in contracts or token in runtime, token)

    def test_origin_dossier_runtime_stays_scoped_and_render_only(self):
        runtime = read("src/Chummer.Media.Factory.Runtime/Assets/OriginDossierNarrationRenderingService.cs")

        for token in (
            "MediaRenderJobType.OriginDossierCanonicalAudiobookAudio",
            "MediaRenderJobType.OriginDossierAlternateAudiobookAudio",
            "PayloadMatchesApprovedPacketId",
            "PayloadMatchesRevisionId",
            "ParseJsonScopePayload",
            "JsonPayloadMissingScopeFields",
            "TryParseScopeFromTextPayload",
            "ContainsDelimitedScopeValue",
            "BuildCompanionReadyRefs",
            "BuildCaptionRefReceipts",
            "BuildPreviewRefReceipts",
            "BuildRoleReceiptGroups",
            "origin_dossier_narration_receipt_",
            "origin-dossier-narration:",
            "BuildRefHashSegment(\"caption\", artifact.CaptionRefs)",
            "BuildRefHashSegment(\"preview\", artifact.PreviewRefs)",
            "companion refs must be unique",
            "Origin dossier narration artifacts[{index}] is required.",
        ):
            self.assertIn(token, runtime)

        for token in (
            "Chummer.Engine.Contracts",
            "Chummer.Campaign.Contracts",
            "provider routing policy",
            "rules authority",
            "canon-generation",
        ):
            self.assertNotIn(token, runtime)

    def test_origin_dossier_smoke_proves_collision_and_scope_guards(self):
        smoke = read("tests/OriginDossierNarrationSmoke/Program.cs")

        for token in (
            "origin-dossier-narration-render-collision-proof",
            "origin-dossier://packet-001/audio/alt-deluxe",
            "Different origin dossier output refs must not collapse onto one narration render job",
            "origin-dossier-narration-duplicate-companion-ref",
            "Duplicate origin dossier narration companion ref validation did not fail.",
            "origin-dossier-narration-missing-approved-packet-scope",
            "Origin dossier narration payload packet scope validation did not fail.",
            "origin-dossier-narration-missing-revision-scope",
            "Origin dossier narration payload revision scope validation did not fail.",
            "origin-dossier-narration-json-missing-scope-fields",
            "Origin dossier narration JSON payloads without required scope fields should fail closed instead of falling back to substring matching.",
            "Replay-safe dedupe should keep origin dossier narration jobs stable.",
            "Origin dossier narration ready refs must preserve ref, receipt, job, asset id, and asset url.",
            "Origin dossier narration caption receipt rows must preserve grouped providers.",
            "Origin dossier narration preview receipt rows must preserve grouped providers.",
        ):
            self.assertIn(token, smoke)


if __name__ == "__main__":
    unittest.main()
