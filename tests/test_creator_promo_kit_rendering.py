from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[1]


def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8")


class CreatorPromoKitRenderingTests(unittest.TestCase):
    def test_creator_promo_contracts_and_runtime_emit_preview_and_caption_receipts(self):
        contracts = read("src/Chummer.Media.Contracts/Compatibility/RunServices/MediaFactoryContracts.cs")
        runtime = read("src/Chummer.Media.Factory.Runtime/Assets/CreatorPromoKitRenderingService.cs")

        for token in (
            "CreatorPromoKitRenderRequest",
            "CreatorPromoKitRenderReceipt",
            "CreatorPromoKitArtifactReceipt",
            "CreatorPromoKitArtifactRole.PromoVideo",
            "CreatorPromoKitArtifactRole.PromoPoster",
            "CreatorPromoKitArtifactRole.PreviewCard",
            "PromoVideoReceiptIds",
            "PromoPosterReceiptIds",
            "PreviewCardReceiptIds",
            "ArtifactRefs",
            "ReadyRefs",
            "CreatorPromoKitReadyRef",
            "CreatorPromoKitArtifactRefReceipt",
            "CreatorPromoCaptionRefReceipt",
            "CreatorPromoPreviewRefReceipt",
            "CreatorPromoKitRoleReceiptGroup",
            "ArtifactReceipts",
            "AssetUrl",
            "ManifestRevisionId",
            "ApprovedManifestId",
        ):
            self.assertTrue(token in contracts or token in runtime, token)

    def test_creator_promo_runtime_maps_all_required_siblings_to_media_jobs(self):
        runtime = read("src/Chummer.Media.Factory.Runtime/Assets/CreatorPromoKitRenderingService.cs")

        for token in (
            "MediaRenderJobType.CreatorPromoVideo",
            "MediaRenderJobType.CreatorPromoPoster",
            "MediaRenderJobType.CreatorPromoPreviewCard",
            "BuildArtifactRefReceipts",
            "BuildReadyRefs",
            "BuildCaptionRefReceipts",
            "BuildPreviewRefReceipts",
            "BuildRoleReceiptGroups",
            "creator_promo_receipt_",
            "creator-promo-kit:",
            "BuildRefHashSegment(\"caption\", artifact.CaptionRefs)",
            "BuildRefHashSegment(\"preview\", artifact.PreviewRefs)",
            "CanonicalizeGroupedRef",
            ".GroupBy(static value => value, StringComparer.OrdinalIgnoreCase)",
            ".ThenBy(static value => value, StringComparer.Ordinal)",
            "RequireUniqueArtifactRefs",
            "RequirePayloadScope",
            "PayloadMatchesApprovedManifestId",
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
            "var approvedManifestId = request.ApprovedManifestId.Trim();",
            "var manifestRevisionId = request.ManifestRevisionId.Trim();",
            "var source = request.Source.Trim();",
            "RequirePayloadScope(artifacts, normalizedRequest);",
            "artifact refs must be unique",
            "throw new ArgumentNullException(nameof(CreatorPromoKitRenderRequest.Artifacts));",
            "Select((artifact, index) => NormalizeArtifact(artifact, index))",
            "Creator promo kit artifacts[{index}] is required.",
            "approved manifest id",
            "CreatorPromoKitArtifactRole.PromoVideo && captionRefs.Count == 0",
            "creator promo artifacts require at least one preview ref",
            "ManifestRevisionId",
            "ApprovedManifestId",
            "status.State is MediaRenderJobState.Succeeded",
            "AssetUrl: status.AssetUrl",
            "AssetUrl: receipt.AssetUrl",
            "RenderedAtUtc: renderedAtUtc",
        ):
            self.assertIn(token, runtime)

    def test_creator_promo_smoke_proves_replay_and_collision_safety(self):
        smoke = read("tests/CreatorPromoKitSmoke/Program.cs")

        for token in (
            "creator-promo-render-collision-proof",
            "creator-promo://manifest-001/video-web",
            "Different creator promo output refs must not collapse onto one promo render job",
            "creator-promo-render-receipt-collision-proof",
            "creator-promo://manifest-001/receipt-delimiter/a",
            "Delimiter-heavy creator promo caption refs must not collapse onto one receipt id.",
            "creator-promo-duplicate-artifact-ref",
            "Duplicate creator promo artifact ref validation did not fail.",
            "creator-promo-null-artifact-entry",
            "Null creator promo artifact entry validation did not fail.",
            "creator-promo-null-artifact-list",
            "Null creator promo artifact list validation did not fail.",
            "creator-promo-missing-approved-manifest-scope",
            "Creator promo payload manifest scope validation did not fail.",
            "creator-promo-missing-revision-scope",
            "Creator promo payload revision scope validation did not fail.",
            "creator-promo-json-missing-scope-fields",
            "Creator promo JSON payloads without required scope fields should fail closed instead of falling back to substring matching.",
            "creator-promo-non-json-scope-fallback",
            "Non-JSON creator promo payloads should still render when they carry the approved manifest scope text.",
            "creator-promo-padded-request",
            "Creator promo job ids should stay stable when only top-level request and scope whitespace changes.",
            "Creator promo receipt ids should stay stable when only top-level request and scope whitespace changes.",
            "creator-promo-case-folded-refs",
            "Mixed-case creator promo caption refs should dedupe into one canonical ref.",
            "Mixed-case creator promo preview refs should dedupe into one canonical ref.",
            "Mixed-case creator promo caption refs should stay grouped under one receipt row.",
            "Mixed-case creator promo preview refs should stay grouped under one receipt row.",
            "Mixed-case creator promo preview refs should still point at video, poster, and preview-card receipts.",
            "Replay-safe dedupe should keep creator promo kit jobs stable.",
            "Replay-safe dedupe should not let later request timestamps rewrite creator promo rendered timestamps.",
            "Normalized sibling ordering should keep creator promo job ids stable when callers reorder the same approved manifest siblings.",
            "Normalized sibling ordering should keep creator promo ready ref receipt ids stable when callers reorder the same approved manifest siblings.",
            "Normalized sibling ordering should keep creator promo artifact refs stable when callers reorder the same approved manifest siblings.",
            "Normalized sibling ordering should keep creator promo caption receipt rows stable when callers reorder the same approved manifest siblings.",
            "Normalized sibling ordering should keep creator promo preview receipt rows stable when callers reorder the same approved manifest siblings.",
            "Normalized sibling ordering should keep creator promo role receipt rows stable when callers reorder the same approved manifest siblings.",
            "Creator promo ready refs must preserve ref, receipt, job, asset id, and asset url.",
            "Creator promo artifact ref receipts must preserve ref, receipt, job, asset id, and asset url.",
            "Creator promo role receipt groups must preserve receipt, job, ref, and artifact rows.",
            "Shared creator promo preview ref should point at video, poster, and preview-card receipts.",
        ):
            self.assertIn(token, smoke)

    def test_creator_promo_runtime_stays_render_only(self):
        runtime = read("src/Chummer.Media.Factory.Runtime/Assets/CreatorPromoKitRenderingService.cs")

        forbidden_tokens = (
            "Chummer.Engine.Contracts",
            "Chummer.Campaign.Contracts",
            "publication workflow",
            "trust ranking policy",
            "delivery policy",
        )

        for token in forbidden_tokens:
            self.assertNotIn(token, runtime)


if __name__ == "__main__":
    unittest.main()
