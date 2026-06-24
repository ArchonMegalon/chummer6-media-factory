from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[1]


def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8")


class GmPrepPacketRenderingTests(unittest.TestCase):
    def test_gm_prep_packet_contracts_and_runtime_emit_first_class_receipts(self):
        contracts = read("src/Chummer.Media.Contracts/Compatibility/RunServices/MediaFactoryContracts.cs")
        runtime = read("src/Chummer.Media.Factory.Runtime/Assets/GmPrepPacketBundleService.cs")

        for token in (
            "GmPrepPacketRenderRequest",
            "GmPrepPacketBundleReceipt",
            "GmPrepPacketEntryRenderRequest",
            "GmPrepPacketArtifactRenderRequest",
            "GmPrepPacketArtifactReceipt",
            "GmPrepPacketEntryReceipt",
            "GmPrepPacketSubjectReceiptGroup",
            "GmPrepPacketSubjectKind.Opposition",
            "GmPrepPacketSubjectKind.Scene",
            "GmPrepPacketSubjectKind.PrepLibraryEntry",
            "GmPrepPacketArtifactRole.Packet",
            "GmPrepPacketArtifactRole.Preview",
            "GmPrepPacketArtifactRole.Briefing",
            "PacketReceiptIds",
            "PreviewReceiptIds",
            "BriefingReceiptIds",
            "OppositionPacketReceiptIds",
            "ScenePacketReceiptIds",
            "PrepLibraryPacketReceiptIds",
            "PacketRefs",
            "SubjectReceiptGroups",
            "GovernedSourcePackId",
            "SourcePackRevisionId",
        ):
            self.assertTrue(token in contracts or token in runtime, token)

    def test_gm_prep_runtime_maps_all_required_subject_artifacts_to_media_jobs(self):
        runtime = read("src/Chummer.Media.Factory.Runtime/Assets/GmPrepPacketBundleService.cs")

        for token in (
            "MediaRenderJobType.GmPrepOppositionPacket",
            "MediaRenderJobType.GmPrepOppositionPreview",
            "MediaRenderJobType.GmPrepOppositionBriefing",
            "MediaRenderJobType.GmPrepScenePacket",
            "MediaRenderJobType.GmPrepScenePreview",
            "MediaRenderJobType.GmPrepSceneBriefing",
            "MediaRenderJobType.GmPrepLibraryPacket",
            "MediaRenderJobType.GmPrepLibraryPreview",
            "MediaRenderJobType.GmPrepLibraryBriefing",
            "BuildSubjectReceiptGroups",
            "BuildSubjectReceiptGroupId",
            "BuildScopedDeduplicationKey",
            'BuildHashSegment("subject-kind", entry.SubjectKind.ToString())',
            'BuildHashSegment("artifact-role", role.ToString())',
            'BuildHashSegment("output-format", artifact.OutputFormat)',
            "RequireOppositionEntry",
            "RequireUniqueSourceEntries",
            "RequireUniquePacketRefs",
            "RequirePayloadScope",
            "PayloadMatchesGovernedSourcePackId",
            "PayloadMatchesSourcePackRevisionId",
            "PayloadMatchesPacketRef",
            "PayloadMatchesSourceEntryId",
            "ParseJsonScopePayload",
            "JsonPayloadMissingScopeFields",
            "TryParseScopeFromTextPayload",
            "ContainsDelimitedScopeValue",
            "IsScopeDelimiter",
            "IsScopeTokenCharacter",
            "Regex.Match",
            "JsonDocument.Parse",
            "var renderingId = request.RenderingId.Trim();",
            "var governedSourcePackId = request.GovernedSourcePackId.Trim();",
            "var sourcePackRevisionId = request.SourcePackRevisionId.Trim();",
            "var source = request.Source.Trim();",
            "SubjectReceiptGroups: subjectReceiptGroups",
            "AssetUrl: status.AssetUrl",
            "ApprovalState: status.ApprovalState",
            "RetentionState: status.RetentionState",
            "StorageClass: status.StorageClass",
            "RenderedAtUtc: renderedAtUtc",
            "field.Length",
            "gm_prep_packet_receipt_",
            "gm-prep-packet:",
        ):
            self.assertIn(token, runtime)

    def test_gm_prep_smoke_proves_scope_and_collision_safety(self):
        smoke = read("tests/GmPrepPacketSmoke/Program.cs")

        for token in (
            "GM prep packet rendering should receipt packet, preview, and optional briefing siblings.",
            "Each GM prep subject kind should emit a receipt group.",
            "Replay-safe dedupe should keep GM prep packet jobs stable.",
            "Replay-safe dedupe should keep GM prep rendered timestamps stable.",
            "Delimiter-heavy GM prep packet variants must not collapse onto one media job.",
            "Delimiter-heavy GM prep packet receipt variants must not collapse onto one receipt id.",
            "Governed source pack scope should keep GM prep entry receipt ids distinct across reused rendering ids.",
            "Governed source pack scope should keep GM prep subject receipt groups distinct across reused rendering ids.",
            "gm-prep-missing-opposition",
            "Missing opposition entry validation did not fail.",
            "gm-prep-duplicate-source-entry",
            "Duplicate GM prep source entry validation did not fail.",
            "gm-prep-duplicate-packet-ref",
            "Duplicate GM prep packet ref validation did not fail.",
            "gm-prep-json-scope-spoof",
            "GM prep packet JSON scope spoof validation did not fail.",
            "gm-prep-text-scope-near-miss",
            "GM prep packet text payload near-miss scope validation did not fail.",
            "gm-prep-json-missing-scope-fields",
            "GM prep packet JSON payloads without required scope fields should fail closed instead of falling back to substring matching.",
            "gm-prep-json-array-scope-spoof",
            "GM prep packet JSON array payloads without required scope fields should fail closed instead of falling back to substring matching.",
            "gm-prep-json-string-scope-spoof",
            "GM prep packet JSON string payloads without required scope fields should fail closed instead of falling back to substring matching.",
            "gm-prep-json-packet-ref-spoof",
            "GM prep packet JSON packet-ref spoof validation did not fail.",
            "gm-prep-text-source-entry-near-miss",
            "GM prep packet text payload near-miss source-entry validation did not fail.",
            "gm-prep-json-scope-padding",
            "JSON GM prep payloads should trim surrounding whitespace on governed scope values.",
            "gm-prep-text-scope-padding",
            "Keyed text GM prep payloads should trim surrounding whitespace on governed scope values.",
            "gm-prep-whitespace-normalized",
            "GM prep rendering ids should normalize surrounding whitespace before receipts emit.",
            "GM prep governed source pack ids should normalize surrounding whitespace before scope enforcement.",
            "GM prep source pack revision ids should normalize surrounding whitespace before scope enforcement.",
            "GM prep source values should normalize surrounding whitespace before receipts emit.",
            "GM prep packet job ids should stay stable when only top-level request whitespace changes.",
            "GM prep packet receipt ids should stay stable when only top-level request whitespace changes.",
            "GM prep subject receipt groups should stay stable when only top-level request whitespace changes.",
            "Non-JSON GM prep payloads should still render when they carry governed source scope text.",
            "gm-prep-null-entry",
            "Null GM prep entry validation did not fail.",
            "gm-prep-missing-preview-artifact",
            "Missing GM prep preview artifact validation did not fail.",
        ):
            self.assertIn(token, smoke)

    def test_gm_prep_signoff_records_first_class_bundle_lane(self):
        signoff = read("docs/MEDIA_CAPABILITY_SIGNOFF.md")

        for token in (
            "GM prep packet lanes for governed opposition, scene, and prep-library entries rendered into packet, preview, and optional briefing siblings",
            "GmPrepPacketRenderRequest",
            "GmPrepPacketBundleReceipt",
            "GmPrepPacketEntryReceipt",
            "GmPrepPacketSubjectReceiptGroup",
            "GmPrepOppositionPacket",
            "GmPrepOppositionPreview",
            "GmPrepOppositionBriefing",
            "GmPrepScenePacket",
            "GmPrepScenePreview",
            "GmPrepSceneBriefing",
            "GmPrepLibraryPacket",
            "GmPrepLibraryPreview",
            "GmPrepLibraryBriefing",
            "GM prep packet rendering stays render-verified by requiring a `GovernedSourcePackId`, `SourcePackRevisionId`, `PacketRef`, and `SourceEntryId` plus sibling-only payloads before any media job can enqueue",
            "Parseable JSON GM prep payloads fail closed when required scope fields are missing or the root payload is not an object, so JSON strings, arrays, or note-only objects cannot bypass exact scope matching through text fallback",
            "Non-JSON GM prep payloads require exact keyed values or delimiter-safe scope tokens, so near-match source pack ids, packet refs, and source entry ids cannot pass by raw substring collision",
            "JSON and keyed text GM prep scope values trim surrounding whitespace before exact scope matching so padded governed payloads stay valid without reopening substring spoof paths",
            "GM prep packet rendering fails closed when the request contains null entries or a governed entry drops its required packet or preview artifact before normalization continues",
            "GM prep packet rendering requires at least one opposition entry and preserves first-class packet, preview, and optional briefing receipt ids per governed entry",
            "GM prep packet rendering rejects duplicate source entries and packet refs inside one governed render request",
            "GM prep request-level rendering id, governed source pack id, source pack revision id, and source values normalize surrounding whitespace before scope enforcement so valid padded requests keep stable job ids and receipt ids",
            "GM prep packet subject receipt groups preserve grouped entry, packet, preview, briefing, and job ids so downstream shelves do not need to reconstruct governed packet evidence from raw artifact receipts",
            "GM prep packet dedupe includes governed source pack id, source pack revision id, rendering id, subject kind, source entry id, packet ref, artifact role, category, output format, and caller dedupe",
        ):
            self.assertIn(token, signoff)

    def test_gm_prep_runtime_stays_render_only(self):
        runtime = read("src/Chummer.Media.Factory.Runtime/Assets/GmPrepPacketBundleService.cs")

        forbidden_tokens = (
            "Chummer.Engine.Contracts",
            "Chummer.Campaign.Contracts",
            "planner",
            "mutate campaign truth",
            "approval workflow",
        )

        for token in forbidden_tokens:
            self.assertNotIn(token, runtime)


if __name__ == "__main__":
    unittest.main()
