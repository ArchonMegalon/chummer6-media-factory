from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[1]


def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8")


class BuildExplainCompanionRenderingTests(unittest.TestCase):
    def test_build_explain_contracts_and_runtime_emit_companion_receipts(self):
        contracts = read("src/Chummer.Media.Contracts/Compatibility/RunServices/MediaFactoryContracts.cs")
        runtime = read("src/Chummer.Media.Factory.Runtime/Assets/BuildExplainCompanionRenderingService.cs")

        for token in (
            "BuildExplainCompanionRenderRequest",
            "BuildExplainCompanionRenderReceipt",
            "BuildExplainCompanionArtifactReceipt",
            "BuildExplainCompanionArtifactRole.Video",
            "BuildExplainCompanionArtifactRole.Audio",
            "BuildExplainCompanionArtifactRole.PreviewCard",
            "BuildExplainCompanionArtifactRole.PacketCompanion",
            "VideoReceiptIds",
            "AudioReceiptIds",
            "PreviewCardReceiptIds",
            "PacketCompanionReceiptIds",
            "CompanionRefs",
            "CompanionReadyRefs",
            "BuildExplainCompanionReadyRef",
            "BuildExplainCompanionRefReceipt",
            "BuildExplainCaptionRefReceipt",
            "BuildExplainPreviewRefReceipt",
            "BuildExplainCompanionRoleReceiptGroup",
            "ArtifactReceipts",
            "AssetUrl",
            "ExplainPacketRevisionId",
            "ApprovedExplainPacketId",
        ):
            self.assertTrue(token in contracts or token in runtime, token)

    def test_build_explain_runtime_maps_all_required_siblings_to_media_jobs(self):
        runtime = read("src/Chummer.Media.Factory.Runtime/Assets/BuildExplainCompanionRenderingService.cs")

        for token in (
            "MediaRenderJobType.BuildExplainCompanionVideo",
            "MediaRenderJobType.BuildExplainCompanionAudio",
            "MediaRenderJobType.BuildExplainCompanionPreviewCard",
            "MediaRenderJobType.BuildExplainCompanionPacketCompanion",
            "BuildCompanionRefReceipts",
            "BuildCompanionReadyRefs",
            "BuildCaptionRefReceipts",
            "BuildPreviewRefReceipts",
            "BuildRoleReceiptGroups",
            "build_explain_receipt_",
            "build-explain-companion:",
            "BuildRefHashSegment(\"caption\", artifact.CaptionRefs)",
            "BuildRefHashSegment(\"preview\", artifact.PreviewRefs)",
            "CanonicalizeGroupedRef",
            ".GroupBy(static value => value, StringComparer.OrdinalIgnoreCase)",
            ".ThenBy(static value => value, StringComparer.Ordinal)",
            "RequireUniqueCompanionRefs",
            "RequirePayloadScope",
            "PayloadMatchesApprovedPacketId",
            "PayloadMatchesRevisionId",
            "ParseJsonScopePayload",
            "JsonPayloadMissingScopeFields",
            "TryParseScopeFromTextPayload",
            "ContainsDelimitedScopeValue",
            "IsScopeDelimiter",
            "IsScopeTokenCharacter",
            "TryGetJsonStringProperty",
            "TrimScopeValue",
            "Regex.Match",
            "JsonDocument.Parse",
            "var renderingId = request.RenderingId.Trim();",
            "var approvedExplainPacketId = request.ApprovedExplainPacketId.Trim();",
            "var explainPacketRevisionId = request.ExplainPacketRevisionId.Trim();",
            "var source = request.Source.Trim();",
            "RequirePayloadScope(artifacts, normalizedRequest);",
            "companion refs must be unique",
            "throw new ArgumentNullException(nameof(BuildExplainCompanionRenderRequest.Artifacts));",
            "Select((artifact, index) => NormalizeArtifact(artifact, index))",
            "Build explain companion artifacts[{index}] is required.",
            "approved explain packet id",
            "PacketCompanion && previewRefs.Count == 0",
            "ExplainPacketRevisionId",
            "ApprovedExplainPacketId",
            "status.State is MediaRenderJobState.Succeeded",
            "AssetUrl: status.AssetUrl",
            "AssetUrl: receipt.AssetUrl",
            "RenderedAtUtc: renderedAtUtc",
        ):
            self.assertIn(token, runtime)

    def test_build_explain_smoke_proves_replay_and_collision_safety(self):
        smoke = read("tests/BuildExplainCompanionSmoke/Program.cs")

        for token in (
            "build-explain-render-collision-proof",
            "build-explain://packet-001/video-web",
            "Different build explain output refs must not collapse onto one companion render job",
            "build-explain-render-receipt-collision-proof",
            "build-explain://packet-001/receipt-delimiter/a",
            "Delimiter-heavy build explain caption refs must not collapse onto one receipt id.",
            "build-explain-duplicate-companion-ref",
            "Duplicate build explain companion ref validation did not fail.",
            "build-explain-duplicate-companion-ref-normalized",
            "Case-insensitive or padded build explain companion ref validation did not fail.",
            "build-explain-null-artifact-entry",
            "Null build explain artifact entry validation did not fail.",
            "build-explain-null-artifact-list",
            "Null build explain artifact list validation did not fail.",
            "build-explain-missing-approved-packet-scope",
            "Build explain payload packet scope validation did not fail.",
            "build-explain-text-scope-near-miss",
            "Build explain text payload near-miss scope validation did not fail.",
            "build-explain-missing-revision-scope",
            "Build explain payload revision scope validation did not fail.",
            "build-explain-json-scope-spoof",
            "Build explain JSON scope spoof validation did not fail.",
            "build-explain-json-missing-scope-fields",
            "Build explain JSON payloads without required scope fields should fail closed instead of falling back to substring matching.",
            "build-explain-json-array-scope-spoof",
            "Build explain JSON array payloads without required scope fields should fail closed instead of falling back to substring matching.",
            "build-explain-json-string-scope-spoof",
            "Build explain JSON string payloads without required scope fields should fail closed instead of falling back to substring matching.",
            "build-explain-non-json-scope-fallback",
            "Non-JSON build explain payloads should still render when they carry the approved packet scope text.",
            "build-explain-padded-json-scope",
            "JSON build explain payloads should trim surrounding whitespace on approved packet scope values.",
            "build-explain-padded-text-scope",
            "Keyed text build explain payloads should trim surrounding whitespace on approved packet scope values.",
            "build-explain-mixed-case-ref-normalization",
            "Mixed-case caption refs should keep canonical ref casing stable when callers reorder the same refs.",
            "Mixed-case preview refs should keep canonical ref casing stable when callers reorder the same refs.",
            "Mixed-case caption ref receipt rows should keep canonical ref casing stable when callers reorder the same refs.",
            "Mixed-case preview ref receipt rows should keep canonical ref casing stable when callers reorder the same refs.",
            "Build explain caption refs should trim surrounding whitespace before grouped receipts emit.",
            "Build explain preview refs should trim surrounding whitespace before grouped receipts emit.",
            "Build explain caption ref receipt rows should trim surrounding whitespace before grouped receipts emit.",
            "Build explain preview ref receipt rows should trim surrounding whitespace before grouped receipts emit.",
            "Build explain receipt ids should stay stable when only caption and preview ref whitespace changes.",
            "Companion ready refs should stay stable when only caption and preview ref whitespace changes.",
            "Replay-safe dedupe should keep build explain companion jobs stable.",
            "Replay-safe dedupe should not let later request timestamps rewrite build explain rendered timestamps.",
            "Video receipt ids should stay stable when callers reorder build explain siblings.",
            "Companion ready refs should stay stable when callers reorder build explain siblings.",
            "Role receipt groups should stay stable when callers reorder build explain siblings.",
            "Caption ref receipt rows should stay stable when callers reorder build explain siblings.",
            "Preview ref receipt rows should stay stable when callers reorder build explain siblings.",
            "Build explain rendering ids should normalize surrounding whitespace before receipts emit.",
            "Build explain approved packet ids should normalize surrounding whitespace before scope enforcement.",
            "Build explain revision ids should normalize surrounding whitespace before scope enforcement.",
            "Build explain source values should normalize surrounding whitespace before receipts emit.",
            "Build explain companion job ids should stay stable when only top-level request whitespace changes.",
            "Build explain receipt ids should stay stable when only top-level request whitespace changes.",
            "Companion ready refs should stay stable when only top-level request whitespace changes.",
            "Build explain companion job ids should stay stable when only source and requested timestamps drift.",
            "Build explain receipt ids should stay stable when only source and requested timestamps drift.",
            "Companion ready refs should stay stable when only source and requested timestamps drift.",
            "Role receipt groups should stay stable when only source and requested timestamps drift.",
            "Build explain ready refs must preserve ref, receipt, job, asset id, and asset url.",
            "Build explain companion ref receipts must preserve ref, receipt, job, asset id, and asset url.",
            "Build explain role receipt groups must preserve receipt, job, ref, and artifact rows.",
            "Build explain caption ref receipts must preserve aggregate job ids.",
            "Build explain caption ref receipts must preserve grouped companion refs.",
            "Build explain caption ref receipts must preserve grouped artifact rows.",
            "Build explain preview ref receipts must preserve aggregate job ids.",
            "Build explain preview ref receipts must preserve grouped companion refs.",
            "Build explain preview ref receipts must preserve grouped artifact rows.",
            "Build explain video role groups must preserve exact aggregate receipt, job, and artifact linkage.",
            "Shared build explain caption ref should point at video and audio receipts.",
            "Shared build explain preview ref should point at video, preview-card, and packet receipts.",
        ):
            self.assertIn(token, smoke)

    def test_build_explain_runtime_stays_render_only(self):
        runtime = read("src/Chummer.Media.Factory.Runtime/Assets/BuildExplainCompanionRenderingService.cs")

        forbidden_tokens = (
            "Chummer.Engine.Contracts",
            "Chummer.Campaign.Contracts",
            "mutate engine truth",
            "approval workflow",
            "delivery policy",
        )

        for token in forbidden_tokens:
            self.assertNotIn(token, runtime)


if __name__ == "__main__":
    unittest.main()
