from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[1]


def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8")


class InstallAwareConciergeRenderingTests(unittest.TestCase):
    def test_install_aware_concierge_contracts_and_runtime_emit_first_class_receipts(self):
        contracts = read("src/Chummer.Media.Contracts/Compatibility/RunServices/MediaFactoryContracts.cs")
        runtime = read("src/Chummer.Media.Factory.Runtime/Assets/InstallAwareConciergeBundleService.cs")

        for token in (
            "InstallAwareConciergeRenderRequest",
            "InstallAwareConciergeBundleReceipt",
            "InstallAwareConciergeArtifactReceipt",
            "InstallAwareConciergeCompanionReadyRef",
            "InstallAwareConciergeCompanionRefReceipt",
            "InstallAwareConciergeCaptionRefReceipt",
            "InstallAwareConciergePreviewRefReceipt",
            "InstallAwareConciergeSiblingNoteReceipt",
            "InstallAwareConciergeBundleReceiptGroup",
            "InstallAwareConciergeRoleReceiptGroup",
            "InstallAwareConciergeBundleKind.ReleaseExplainer",
            "InstallAwareConciergeBundleKind.SupportClosure",
            "InstallAwareConciergeBundleKind.PublicConcierge",
            "InstallAwareConciergeArtifactRole.Video",
            "InstallAwareConciergeArtifactRole.Audio",
            "InstallAwareConciergeArtifactRole.PreviewCard",
            "ReleaseExplainerReceiptIds",
            "SupportClosureReceiptIds",
            "PublicConciergeReceiptIds",
            "SiblingNoteRefs",
            "SiblingNoteReceipts",
            "ArtifactIdentityId",
            "InstalledBuildReceiptId",
            "InstallAwarePacketId",
        ):
            self.assertTrue(token in contracts or token in runtime, token)

    def test_install_aware_runtime_maps_all_required_siblings_to_media_jobs(self):
        runtime = read("src/Chummer.Media.Factory.Runtime/Assets/InstallAwareConciergeBundleService.cs")

        for token in (
            "MediaRenderJobType.InstallAwareReleaseExplainerVideo",
            "MediaRenderJobType.InstallAwareSupportClosureAudio",
            "MediaRenderJobType.InstallAwarePublicConciergePreviewCard",
            "BuildCompanionRefReceipts",
            "BuildCompanionReadyRefs",
            "BuildCaptionRefReceipts",
            "BuildPreviewRefReceipts",
            "BuildSiblingNoteReceipts",
            "BuildBundleReceiptGroups",
            "BuildRoleReceiptGroups",
            "install_aware_concierge_receipt_",
            "install-aware-concierge:",
            "BuildRefHashSegment(\"caption\", artifact.CaptionRefs)",
            "BuildRefHashSegment(\"preview\", artifact.PreviewRefs)",
            "BuildRefHashSegment(\"sibling-note\", artifact.SiblingNoteRefs)",
            "CanonicalizeGroupedRef",
            "RequireUniqueCompanionRefs",
            "RequirePayloadScope",
            "PayloadMatchesInstallAwarePacketId",
            "PayloadMatchesInstalledBuildReceiptId",
            "PayloadMatchesArtifactIdentityId",
            "TryParseScopeFromJsonPayload",
            "parsedJsonPayload",
            "TryParseScopeFromTextPayload",
            "ContainsDelimitedScopeValue",
            "JsonDocument.Parse",
            "MaxSiblingNotesPerArtifact",
            "status.State is MediaRenderJobState.Succeeded",
            "AssetUrl: status.AssetUrl",
            "AssetUrl: receipt.AssetUrl",
            "RenderedAtUtc: renderedAtUtc",
        ):
            self.assertIn(token, runtime)

    def test_install_aware_smoke_proves_scope_and_collision_safety(self):
        smoke = read("tests/InstallAwareConciergeSmoke/Program.cs")

        for token in (
            "Release explainer receipt ids should stay stable when callers reorder install-aware concierge siblings.",
            "Companion ready refs should stay stable when callers reorder install-aware concierge siblings.",
            "Bundle receipt groups should stay stable when callers reorder install-aware concierge siblings.",
            "Role receipt groups should stay stable when callers reorder install-aware concierge siblings.",
            "Caption ref receipt rows should stay stable when callers reorder install-aware concierge siblings.",
            "Preview ref receipt rows should stay stable when callers reorder install-aware concierge siblings.",
            "Sibling note receipt rows should stay stable when callers reorder install-aware concierge siblings.",
            "install-aware-mixed-case-ref-normalization",
            "Mixed-case caption, preview, and sibling-note duplicates should keep release receipt ids stable when callers reorder the same refs.",
            "Mixed-case caption ref duplicates should keep aggregate caption refs stable when callers reorder the same refs.",
            "Mixed-case preview ref duplicates should keep aggregate preview refs stable when callers reorder the same refs.",
            "Mixed-case sibling-note duplicates should keep aggregate sibling-note refs stable when callers reorder the same refs.",
            "Mixed-case caption ref receipt rows should keep canonical ref casing stable when callers reorder the same refs.",
            "Mixed-case preview ref receipt rows should keep canonical ref casing stable when callers reorder the same refs.",
            "Mixed-case sibling-note receipt rows should keep canonical ref casing stable when callers reorder the same refs.",
            "install-aware-concierge-render-collision-proof",
            "install-aware://release/video-web",
            "Different install-aware release output refs must not collapse onto one concierge render job.",
            "install-aware-concierge-render-receipt-collision-proof",
            "install-aware://release/receipt-delimiter/a",
            "Delimiter-heavy install-aware concierge refs must not collapse onto one receipt id.",
            "install-aware-duplicate-companion-ref",
            "Duplicate install-aware concierge companion ref validation did not fail.",
            "install-aware-missing-packet-scope",
            "Install-aware concierge payload packet scope validation did not fail.",
            "install-aware-json-scope-spoof",
            "Install-aware concierge JSON scope spoof validation did not fail.",
            "install-aware-json-missing-scope-fields",
            "Install-aware concierge JSON payload missing required scope fields did not fail.",
            "install-aware-json-string-scope-spoof",
            "Install-aware concierge JSON string payload scope spoof did not fail.",
            "install-aware-delimited-scope-spoof",
            "Install-aware concierge delimited text scope spoof validation did not fail.",
            "install-aware-unbounded-sibling-notes",
            "Install-aware concierge sibling note bounds validation did not fail.",
            "Non-JSON install-aware concierge payloads should still render when they carry the install-aware scope text.",
            "install-aware-padded-payload-scope",
            "Install-aware concierge JSON payload scope values should normalize surrounding whitespace.",
            "install-aware-whitespace-normalization",
            "Install-aware concierge packet ids should normalize surrounding whitespace before scope validation.",
            "Install-aware concierge installed build receipt ids should normalize surrounding whitespace before scope validation.",
            "Install-aware concierge artifact identity ids should normalize surrounding whitespace before scope validation.",
            "Replay-safe dedupe should keep install-aware concierge jobs stable.",
            "Replay-safe dedupe should keep install-aware concierge rendered timestamps stable.",
            "Install-aware concierge source and requested timestamps should stay outside replay-safe dedupe.",
            "Install-aware concierge source and requested timestamps should stay outside receipt identity.",
            "Install-aware concierge source and requested timestamps should stay outside companion ready identity.",
            "Release explainer bundle groups must preserve aggregate receipt ids.",
            "Support closure bundle groups must preserve preview refs.",
            "Public concierge bundle groups must preserve sibling notes.",
            "Release caption refs must preserve aggregate job ids.",
            "Support caption refs must preserve bundle kinds, roles, and grouped asset urls.",
            "Support preview refs must preserve bundle-kind grouping.",
            "Public preview refs must preserve bundle kinds, roles, and grouped asset urls.",
            "Shared support sibling notes must preserve aggregate receipt ids.",
            "Shared support sibling notes must preserve bundle kinds, roles, and grouped asset urls.",
        ):
            self.assertIn(token, smoke)

    def test_install_aware_runtime_stays_render_only(self):
        runtime = read("src/Chummer.Media.Factory.Runtime/Assets/InstallAwareConciergeBundleService.cs")

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
