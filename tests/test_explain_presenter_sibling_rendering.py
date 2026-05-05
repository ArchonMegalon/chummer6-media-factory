from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[1]


def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8")


class ExplainPresenterSiblingRenderingTests(unittest.TestCase):
    def test_explain_presenter_contracts_and_runtime_emit_grounded_receipts(self):
        contracts = read("src/Chummer.Media.Contracts/Compatibility/RunServices/MediaFactoryContracts.cs")
        runtime = read("src/Chummer.Media.Factory.Runtime/Assets/ExplainPresenterSiblingRenderingService.cs")

        for token in (
            "ExplainPresenterSiblingRenderRequest",
            "ExplainPresenterSiblingRenderReceipt",
            "ExplainPresenterSiblingArtifactReceipt",
            "ExplainPresenterSiblingArtifactRole.Audio",
            "ExplainPresenterSiblingArtifactRole.PresenterVideo",
            "AudioReceiptIds",
            "PresenterReceiptIds",
            "FirstPartyTextFallback",
            "TextFallbackReceipt",
            "ExplainPresenterTextFallbackReceipt",
            "GroundingScopeRef",
            "CompanionReadyRefs",
            "ExplainPresenterSiblingReadyRef",
            "ExplainPresenterCompanionRefReceipt",
            "ExplainPresenterCaptionRefReceipt",
            "ExplainPresenterPreviewRefReceipt",
            "ExplainPresenterSiblingRoleReceiptGroup",
        ):
            self.assertTrue(token in contracts or token in runtime, token)

    def test_explain_presenter_runtime_maps_optional_siblings_and_scope_guards(self):
        runtime = read("src/Chummer.Media.Factory.Runtime/Assets/ExplainPresenterSiblingRenderingService.cs")

        for token in (
            "MediaRenderJobType.ExplainPresenterSiblingAudio",
            "MediaRenderJobType.ExplainPresenterSiblingPresenterVideo",
            "BuildCompanionRefReceipts",
            "BuildCompanionReadyRefs",
            "BuildCaptionRefReceipts",
            "BuildPreviewRefReceipts",
            "BuildRoleReceiptGroups",
            "BuildTextFallbackReceipt",
            "explain_presenter_receipt_",
            "explain-presenter-sibling:",
            "BuildRefHashSegment(\"caption\", artifact.CaptionRefs)",
            "BuildRefHashSegment(\"preview\", artifact.PreviewRefs)",
            "request.FirstPartyTextFallback.Length",
            "PayloadMatchesApprovedPacketId",
            "PayloadMatchesRevisionId",
            "PayloadMatchesGroundingScopeRef",
            "ParseJsonScopePayload",
            "JsonPayloadMissingScopeFields",
            "TryParseScopeFromTextPayload",
            "ContainsDelimitedScopeValue",
            "TryGetJsonStringProperty",
            "TrimScopeValue",
            "approvedExplanationPacketId",
            "groundingScopeRef",
            "packet_id",
            "value_ref",
            "require at least one caption ref",
            "require at least one preview ref",
            "companion refs must be unique",
            "throw new ArgumentNullException(nameof(ExplainPresenterSiblingRenderRequest.Artifacts));",
            "Explain presenter sibling artifacts[{index}] is required.",
            "RenderedAtUtc: renderedAtUtc",
        ):
            self.assertIn(token, runtime)

    def test_explain_presenter_smoke_proves_fallback_and_collision_safety(self):
        smoke = read("tests/ExplainPresenterSiblingSmoke/Program.cs")

        for token in (
            "explain-presenter-render-collision-proof",
            "explain-presenter://packet-001/presenter-deluxe",
            "Different explain presenter output refs must not collapse onto one sibling render job",
            "explain-presenter-render-receipt-collision-proof",
            "explain-presenter://packet-001/receipt-delimiter/a",
            "Delimiter-heavy explain presenter caption refs must not collapse onto one receipt id.",
            "explain-presenter-duplicate-companion-ref",
            "Duplicate explain presenter companion ref validation did not fail.",
            "explain-presenter-null-artifact-entry",
            "Null explain presenter artifact entry validation did not fail.",
            "explain-presenter-null-artifact-list",
            "Null explain presenter artifact list validation did not fail.",
            "explain-presenter-missing-approved-packet-scope",
            "Explain presenter payload packet scope validation did not fail.",
            "explain-presenter-text-revision-near-miss",
            "Explain presenter text payload revision near-miss validation did not fail.",
            "explain-presenter-text-grounding-near-miss",
            "Explain presenter text payload grounding near-miss validation did not fail.",
            "explain-presenter-missing-revision-scope",
            "Explain presenter payload revision scope validation did not fail.",
            "explain-presenter-missing-grounding-scope",
            "Explain presenter payload grounding scope validation did not fail.",
            "explain-presenter-json-missing-scope-fields",
            "Explain presenter JSON payloads without required scope fields should fail closed instead of falling back to substring matching.",
            "explain-presenter-json-array-scope-spoof",
            "explain-presenter-json-string-scope-spoof",
            "explain-presenter-non-json-scope-fallback",
            "Non-JSON explain presenter payloads should still render when they carry the approved packet scope text.",
            "explain-presenter-padded-json-scope",
            "JSON explain presenter payloads should trim surrounding whitespace on approved packet scope values.",
            "explain-presenter-padded-text-scope",
            "Keyed text explain presenter payloads should trim surrounding whitespace on approved packet scope values.",
            "Replay-safe dedupe should keep explain presenter sibling jobs stable.",
            "Explain presenter sibling job ids should stay stable when only source and requested timestamps drift.",
            "Explain presenter sibling job ids should stay stable when only top-level request whitespace changes.",
            "Explain presenter sibling job ids should stay stable when approved explanation packet artifacts reorder.",
            "Explain presenter sibling receipt ids should stay stable when approved explanation packet artifacts reorder.",
            "Explain presenter ready refs should stay stable when approved explanation packet artifacts reorder.",
            "explain-presenter-mixed-case-ref-normalization",
            "Mixed-case explain presenter caption and preview duplicates should keep sibling receipt ids stable when callers reorder the same refs.",
            "Mixed-case explain presenter caption duplicates should keep aggregate caption refs stable when callers reorder the same refs.",
            "Mixed-case explain presenter preview duplicates should keep aggregate preview refs stable when callers reorder the same refs.",
            "Mixed-case explain presenter caption receipt rows should keep canonical ref casing stable when callers reorder the same refs.",
            "Mixed-case explain presenter preview receipt rows should keep canonical ref casing stable when callers reorder the same refs.",
            "Explain presenter role receipt groups must preserve aggregate job ids, grouped companion refs, and grouped artifact rows.",
            "Explain presenter role receipt groups must preserve grouped artifact asset urls and lifecycle truth.",
            "Explain presenter caption receipt rows must preserve grouped artifact asset urls and lifecycle truth.",
            "Explain presenter preview receipt rows must preserve grouped artifact asset urls and lifecycle truth.",
            "Explain presenter text fallback receipts should stay stable when callers replay the same request.",
            "Explain presenter text fallback receipts should stay stable when only source and requested timestamps drift.",
            "Explain presenter text fallback receipts should stay stable when only top-level request whitespace changes.",
            "Explain presenter text fallback receipts should stay stable when approved explanation packet artifacts reorder.",
            "Explain presenter text fallback receipts should change when first-party fallback text changes.",
            "Explain presenter receipt must preserve first-party text fallback.",
            "Explain presenter text fallback must publish a receipt id.",
        ):
            self.assertIn(token, smoke)

    def test_explain_presenter_runtime_stays_render_only(self):
        runtime = read("src/Chummer.Media.Factory.Runtime/Assets/ExplainPresenterSiblingRenderingService.cs")

        for token in (
            "Chummer.Engine.Contracts",
            "Chummer.Campaign.Contracts",
            "calculation authority",
            "provider routing policy",
            "delivery policy",
        ):
            self.assertNotIn(token, runtime)


if __name__ == "__main__":
    unittest.main()
