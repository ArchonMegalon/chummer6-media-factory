from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[1]


def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8")


class ReplayExchangePreviewRenderingTests(unittest.TestCase):
    def test_m115_contracts_and_runtime_emit_preview_and_inspectable_receipts(self):
        contracts = read("src/Chummer.Media.Contracts/Compatibility/RunServices/MediaFactoryContracts.cs")
        runtime = read("src/Chummer.Media.Factory.Runtime/Assets/ReplayExchangePreviewRenderingService.cs")

        for token in (
            "ReplayExchangePreviewRenderRequest",
            "ReplayExchangePreviewRenderReceipt",
            "ReplayExchangePreviewArtifactReceipt",
            "ReplayExchangePreviewBundleReceipt",
            "ReplayExchangePreviewKindReceiptGroup",
            "ReplayExchangePreviewReadyRef",
            "ReplayExchangePreviewArtifactRefReceipt",
            "ReplayExchangePreviewCaptionRefReceipt",
            "ReplayExchangePreviewPreviewRefReceipt",
            "ReplayExchangePreviewBundleKind.Recap",
            "ReplayExchangePreviewBundleKind.Replay",
            "ReplayExchangePreviewBundleKind.Exchange",
            "ReplayExchangePreviewArtifactRole.PreviewCard",
            "ReplayExchangePreviewArtifactRole.InspectableSibling",
            "PreviewCardReceiptIds",
            "InspectableSiblingReceiptIds",
            "BundleRefs",
            "LineageRefs",
            "CompatibilityReceiptIds",
            "ProvenanceReceiptIds",
            "BoundedLossReceiptIds",
        ):
            self.assertTrue(token in contracts or token in runtime, token)

    def test_m115_runtime_maps_bundle_kinds_and_grouped_receipts(self):
        runtime = read("src/Chummer.Media.Factory.Runtime/Assets/ReplayExchangePreviewRenderingService.cs")

        for token in (
            "MediaRenderJobType.RecapPreviewCard",
            "MediaRenderJobType.RecapInspectableSibling",
            "MediaRenderJobType.ReplayPreviewCard",
            "MediaRenderJobType.ReplayInspectableSibling",
            "MediaRenderJobType.ExchangePreviewCard",
            "MediaRenderJobType.ExchangeInspectableSibling",
            "BuildBundleReceipts",
            "BuildKindReceiptGroups",
            "BuildReadyRefs",
            "BuildArtifactRefReceipts",
            "BuildCaptionRefReceipts",
            "BuildPreviewRefReceipts",
            "replay_exchange_preview_receipt_",
            "replay-exchange-preview:",
            "BuildRefHashSegment(\"caption\", artifact.CaptionRefs)",
            "BuildRefHashSegment(\"preview\", artifact.PreviewRefs)",
            "CanonicalizeGroupedRef",
            ".GroupBy(static value => value, StringComparer.OrdinalIgnoreCase)",
            "RequireBundleKind(bundles, ReplayExchangePreviewBundleKind.Recap, request);",
            "RequireBundleKind(bundles, ReplayExchangePreviewBundleKind.Replay, request);",
            "RequireBundleKind(bundles, ReplayExchangePreviewBundleKind.Exchange, request);",
            "RequireUniqueBundleRefs",
            "RequireUniqueArtifactRefs",
            "throw new ArgumentNullException(nameof(ReplayExchangePreviewRenderRequest.Bundles));",
            "Replay/exchange preview bundles[{index}] is required.",
            "Replay/exchange preview artifacts require at least one preview ref.",
            "RenderedAtUtc: renderedAtUtc",
            "AssetUrl: status.AssetUrl",
            "AssetUrl: receipt.AssetUrl",
        ):
            self.assertIn(token, runtime)

    def test_m115_smoke_proves_replay_and_collision_safety(self):
        smoke = read("tests/ReplayExchangePreviewSmoke/Program.cs")

        for token in (
            "replay-exchange-preview-render-collision-proof",
            "Different replay/exchange preview output refs must not collapse onto one preview render job",
            "replay-exchange-preview-render-receipt-collision-proof",
            "artifact://recap/receipt-delimiter/a",
            "Delimiter-heavy replay/exchange preview caption refs must not collapse onto one receipt id.",
            "replay-exchange-preview-duplicate-bundle-ref",
            "Duplicate replay/exchange bundle ref validation did not fail.",
            "replay-exchange-preview-duplicate-artifact-ref",
            "Duplicate replay/exchange preview artifact ref validation did not fail.",
            "replay-exchange-preview-missing-kind",
            "Replay/exchange preview missing-kind validation did not fail.",
            "replay-exchange-preview-null-bundle-entry",
            "Null replay/exchange preview bundle entry validation did not fail.",
            "replay-exchange-preview-null-bundle-list",
            "Null replay/exchange preview bundle list validation did not fail.",
            "replay-exchange-preview-missing-preview-ref",
            "Replay/exchange preview ref validation did not fail.",
            "Replay-safe dedupe should keep replay/exchange preview jobs stable.",
            "Replay/exchange preview job ids should stay stable when only source and requested timestamps drift.",
            "Replay/exchange preview receipt ids should stay stable when only source and requested timestamps drift.",
            "Replay/exchange preview ready refs should stay stable when only source and requested timestamps drift.",
            "Preview-card receipt ids should stay stable when callers reorder replay/exchange preview bundles.",
            "Inspectable-sibling receipt ids should stay stable when callers reorder replay/exchange preview bundles.",
            "Kind receipt groups should stay stable when callers reorder replay/exchange preview bundles.",
            "Replay/exchange preview ready refs must preserve ref, receipt, job, asset id, and asset url.",
            "Replay/exchange preview artifact ref receipts must preserve ref, receipt, job, asset id, and asset url.",
            "Shared replay/exchange caption refs must preserve aggregate receipt ids.",
            "Shared replay/exchange preview refs must preserve aggregate job ids.",
        ):
            self.assertIn(token, smoke)

    def test_m115_runtime_stays_render_only(self):
        runtime = read("src/Chummer.Media.Factory.Runtime/Assets/ReplayExchangePreviewRenderingService.cs")

        forbidden_tokens = (
            "Chummer.Engine.Contracts",
            "Chummer.Campaign.Contracts",
            "approval workflow",
            "delivery policy",
        )

        for token in forbidden_tokens:
            self.assertNotIn(token, runtime)


if __name__ == "__main__":
    unittest.main()
