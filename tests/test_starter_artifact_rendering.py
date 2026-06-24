from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[1]


def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8")


class StarterArtifactRenderingTests(unittest.TestCase):
    def test_starter_artifact_contracts_and_runtime_emit_first_class_receipts(self):
        contracts = read("src/Chummer.Media.Contracts/Compatibility/RunServices/MediaFactoryContracts.cs")
        runtime = read("src/Chummer.Media.Factory.Runtime/Assets/StarterArtifactBundleService.cs")

        for token in (
            "StarterArtifactBundleRenderRequest",
            "StarterArtifactBundleReceipt",
            "StarterArtifactReceipt",
            "StarterArtifactReadyRef",
            "StarterArtifactGroupedReceipt",
            "StarterArtifactArtifactRefReceipt",
            "StarterArtifactCaptionRefReceipt",
            "StarterArtifactPreviewRefReceipt",
            "StarterArtifactSupportNoteReceipt",
            "StarterArtifactLocaleReceiptGroup",
            "StarterArtifactBundleLocaleReceiptGroup",
            "StarterPrimerReceiptIds",
            "FirstSessionBriefingReceiptIds",
            "SupportSafeOnboardingReceiptIds",
            "RequestedLocaleReceiptIds",
            "FallbackLocaleReceiptIds",
            "LocaleReceiptGroups",
            "BundleLocaleReceiptGroups",
            "ArtifactRefReceipts",
            "CaptionRefReceipts",
            "PreviewRefReceipts",
            "SupportNoteReceipts",
            "StarterPrimerVideo",
            "FirstSessionBriefingAudio",
            "SupportSafeOnboardingPreviewCard",
        ):
            self.assertTrue(token in contracts or token in runtime, token)

    def test_starter_artifact_runtime_maps_all_required_siblings_to_media_jobs(self):
        runtime = read("src/Chummer.Media.Factory.Runtime/Assets/StarterArtifactBundleService.cs")

        for token in (
            "MediaRenderJobType.StarterPrimerVideo",
            "MediaRenderJobType.FirstSessionBriefingAudio",
            "MediaRenderJobType.SupportSafeOnboardingPreviewCard",
            "MaxFallbackLocales",
            "MaxSupportNotesPerArtifact",
            "RequirePayloadScope",
            "RequireLocaleBundles",
            "RequireUniqueArtifactRefs",
            "PayloadMatchesField",
            "TryParseScopeFromTextPayload",
            "ContainsDelimitedScopeValue",
            "JsonDocument.Parse",
            "BuildScopedDeduplicationKey",
            'BuildHashSegment("locale", artifact.Locale)',
            'BuildHashSegment("output-format", artifact.OutputFormat)',
            'BuildRefHashSegment("caption", artifact.CaptionRefs)',
            'BuildRefHashSegment("preview", artifact.PreviewRefs)',
            'BuildRefHashSegment("support-note", artifact.SupportNoteRefs)',
            "CanonicalizeGroupedRef",
            "BuildReadyRefs",
            "BuildLocaleReceiptGroups",
            "BuildBundleLocaleReceiptGroups",
            "BuildArtifactRefReceipts",
            "BuildCaptionRefReceipts",
            "BuildPreviewRefReceipts",
            "BuildSupportNoteReceipts",
            "ArtifactRef: artifact.ArtifactRef",
            "AssetUrl: status.AssetUrl",
            "ApprovalState: status.ApprovalState",
            "RetentionState: status.RetentionState",
            "StorageClass: status.StorageClass",
            "renderedAtUtc = renderedAtUtc is",
            "throw new TimeoutException($\"Media job {jobId} did not reach a terminal state in time.\")",
        ):
            self.assertIn(token, runtime)

    def test_starter_artifact_smoke_proves_scope_locale_and_collision_safety(self):
        smoke = read("tests/StarterArtifactBundleSmoke/Program.cs")

        for token in (
            "Starter artifact rendering should receipt every requested sibling.",
            "Starter artifact receipts should preserve fallback locales.",
            "Starter artifact source and requested timestamp metadata should stay outside receipt identity.",
            "Starter artifact source and requested timestamp metadata should stay outside ready-ref identity.",
            "Starter artifact bundle-locale groups should stay stable when callers reorder the same siblings.",
            "Different starter artifact output refs must not collapse onto one media job.",
            "Delimiter-heavy starter locale refs must not collapse onto one receipt id.",
            "Mixed-case caption, preview, and support-note duplicates should keep starter receipt ids stable when callers reorder the same refs.",
            "Mixed-case caption ref duplicates should keep aggregate starter caption refs stable when callers reorder the same refs.",
            "Mixed-case preview ref duplicates should keep aggregate starter preview refs stable when callers reorder the same refs.",
            "Mixed-case support-note duplicates should keep aggregate starter support-note refs stable when callers reorder the same refs.",
            "Mixed-case caption ref receipt rows should keep canonical starter ref casing stable when callers reorder the same refs.",
            "Mixed-case preview ref receipt rows should keep canonical starter ref casing stable when callers reorder the same refs.",
            "Mixed-case support-note receipt rows should keep canonical starter ref casing stable when callers reorder the same refs.",
            "Non-JSON starter artifact payloads should still render when they carry the starter scope text.",
            "Starter artifact delimited text scope spoof validation did not fail.",
            "Starter artifact payload source-pack scope validation did not fail.",
            "Starter artifact JSON payloads without required scope fields should fail closed instead of falling back to substring matching.",
            "Starter artifact fallback locale bound validation did not fail.",
            "Duplicate starter artifact ref validation did not fail.",
        ):
            self.assertIn(token, smoke)

    def test_starter_artifact_signoff_records_first_class_lane(self):
        signoff = read("docs/MEDIA_CAPABILITY_SIGNOFF.md")

        for token in (
            "starter onboarding artifact bundle lanes for localized starter primers, first-session briefings, and support-safe onboarding companions",
            "StarterArtifactBundleRenderRequest",
            "StarterArtifactBundleReceipt",
            "StarterArtifactReadyRef",
            "StarterArtifactLocaleReceiptGroup",
            "StarterArtifactBundleLocaleReceiptGroup",
            "StarterArtifactArtifactRefReceipt",
            "StarterArtifactCaptionRefReceipt",
            "StarterArtifactPreviewRefReceipt",
            "StarterArtifactSupportNoteReceipt",
            "StarterPrimerVideo",
            "FirstSessionBriefingAudio",
            "SupportSafeOnboardingPreviewCard",
            "starter onboarding artifact receipts stay render-verified by requiring an `ApprovedStarterSourcePackId`, `SourcePackRevisionId`, `StarterLaneId`, and per-artifact locale plus sibling-only payloads before any media job can enqueue",
            "starter onboarding bundles require requested-locale and fallback locale triads for starter primer, first-session briefing, and support-safe onboarding siblings, while fallback locales stay bounded to at most two locales",
            "starter onboarding video and audio siblings require caption refs, video and preview-card siblings require preview refs, and support-safe onboarding siblings require bounded support-note refs",
            "starter onboarding rendering rejects duplicate artifact refs inside one starter-lane request and uses length-prefixed dedupe and receipt hashing across locale, caption, preview, and support-note inputs so delimiter-heavy variants cannot collapse distinct outputs onto one job or receipt id",
            "starter onboarding locale and bundle-locale receipt groups preserve aggregate job ids, artifact refs, caption refs, preview refs, support notes, and grouped artifact rows so downstream starter surfaces do not need to reconstruct locale evidence from raw artifact receipts",
        ):
            self.assertIn(token, signoff)

    def test_starter_artifact_runtime_stays_render_only(self):
        runtime = read("src/Chummer.Media.Factory.Runtime/Assets/StarterArtifactBundleService.cs")

        forbidden_tokens = (
            "Chummer.Campaign.Contracts",
            "Chummer.Engine.Contracts",
            "session relay",
            "provider-routing",
            "approval workflow",
            "delivery policy",
        )

        for token in forbidden_tokens:
            self.assertNotIn(token, runtime)


if __name__ == "__main__":
    unittest.main()
