from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[1]


def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8")


class CampaignBriefingBundleContractsTests(unittest.TestCase):
    def test_campaign_briefing_contracts_are_first_class_render_receipts(self):
        contracts = read("src/Chummer.Media.Contracts/Compatibility/RunServices/MediaFactoryContracts.cs")
        runtime = read("src/Chummer.Media.Factory.Runtime/Assets/CampaignBriefingBundleService.cs")

        for token in (
            "CampaignBriefingBundleRequest",
            "CampaignBriefingBundleReceipt",
            "CampaignBriefingBundleEntryRequest",
            "CampaignBriefingBundleArtifactRequest",
            "CampaignBriefingArtifactReceipt",
            "CampaignBriefingLocaleReceipt",
            "CampaignBriefingLocaleBundleReceipt",
            "CampaignBriefingFallbackSiblingReceipt",
            "ReceiptId",
            "LocaleBundleReceipts",
            "ColdOpenCaptionReceiptId",
            "MissionBriefingCaptionReceiptId",
            "ColdOpenPreviewReceiptId",
            "MissionBriefingPreviewReceiptId",
            "RequestedLocaleBundleReceiptId",
            "FallbackLocales",
            "FallbackLocaleBundleReceiptIds",
            "ColdOpenReceiptIds",
            "MissionBriefingReceiptIds",
            "CaptionReceiptIds",
            "PreviewReceiptIds",
            "FallbackSiblingReceipts",
            "CampaignColdOpen",
            "CampaignMissionBriefing",
            "CampaignCaption",
            "CampaignPreview",
        ):
            self.assertTrue(token in contracts or token in runtime, token)

        for token in (
            "CampaignBriefingBundleService",
            "MediaRenderJobEnqueueRequest",
            "CampaignBriefingBundleSlot.ColdOpen",
            "CampaignBriefingBundleSlot.MissionBriefing",
            "CampaignBriefingBundleArtifactKind.Caption",
            "CampaignBriefingBundleArtifactKind.Preview",
            "BuildScopedDeduplicationKey",
            'BuildHashSegment("locale", entry.Locale)',
            'BuildHashSegment("output-format", artifact.OutputFormat)',
            "field.Length",
            "MaxFallbackLocales",
            "fallback locales",
            "RequireLocalePosture",
            "requested locale as the primary sibling",
            "to be marked as a fallback sibling",
            "RequireLocaleBundles",
            "RequireUniqueLocaleSlotEntries",
            "BuildLocaleBundleReceipts",
            "BuildLocaleBundleReceiptId",
            "BuildFallbackSiblingReceiptId",
            "OrderEntries",
            "locale-matched cold-open and mission briefing siblings",
            "ColdOpenCaptionReceiptId: coldOpen.CaptionReceiptId",
            "MissionBriefingCaptionReceiptId: missionBriefing.CaptionReceiptId",
            "ColdOpenPreviewReceiptId: coldOpen.PreviewReceiptId",
            "MissionBriefingPreviewReceiptId: missionBriefing.PreviewReceiptId",
            "RequestedLocaleBundleReceiptId: requestedLocaleBundleReceipt.ReceiptId",
            "FallbackLocales: fallbackLocales",
            "FallbackLocaleBundleReceiptIds: fallbackLocaleBundleReceiptIds",
            "AssetUrl: status.AssetUrl",
            "ApprovalState: status.ApprovalState",
            "RetentionState: status.RetentionState",
            "StorageClass: status.StorageClass",
            "ValidateReceiptStatus",
        ):
            self.assertIn(token, runtime)

    def test_campaign_briefing_bundle_stays_render_only(self):
        runtime = read("src/Chummer.Media.Factory.Runtime/Assets/CampaignBriefingBundleService.cs")

        forbidden_tokens = (
            "Chummer.Campaign.Contracts",
            "Chummer.Engine.Contracts",
            "session relay",
            "provider-routing",
            "approval workflow",
        )

        for token in forbidden_tokens:
            self.assertNotIn(token, runtime)

    def test_campaign_briefing_signoff_records_first_class_bundle_lane(self):
        signoff = read("docs/MEDIA_CAPABILITY_SIGNOFF.md")

        for token in (
            "campaign briefing bundle lanes for locale-matched cold-open, mission briefing, caption, and preview siblings",
            "CampaignBriefingBundleRequest",
            "CampaignBriefingBundleReceipt",
            "CampaignBriefingLocaleBundleReceipt",
            "CampaignBriefingFallbackSiblingReceipt",
            "RequestedLocaleBundleReceiptId",
            "FallbackLocales",
            "FallbackLocaleBundleReceiptIds",
            "ColdOpenCaptionReceiptId",
            "MissionBriefingCaptionReceiptId",
            "ColdOpenPreviewReceiptId",
            "MissionBriefingPreviewReceiptId",
            "CampaignColdOpen",
            "CampaignMissionBriefing",
            "CampaignCaption",
            "CampaignPreview",
            "campaign briefing bundles require requested-locale cold-open and mission briefing entries",
            "campaign briefing locale-bundle and fallback-sibling receipt rows keep stable receipt ids",
            "campaign briefing locale-bundle and fallback-sibling receipt rows also preserve slot-aware caption and preview sibling ids",
            "campaign briefing bundle receipts also carry first-class `RequestedLocaleBundleReceiptId`, `FallbackLocales`, and `FallbackLocaleBundleReceiptIds` fields",
            "campaign briefing job dedupe includes requested locale, slot, entry locale, fallback posture, category, output format, and caller dedupe",
            "campaign briefing normalized locale-bundle ordering keeps locale receipts, locale bundle receipts, fallback sibling receipts, and summary job ids stable when callers reorder the same bundle entries",
        ):
            self.assertIn(token, signoff)


if __name__ == "__main__":
    unittest.main()
