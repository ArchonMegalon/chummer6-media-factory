from pathlib import Path
import json
import subprocess
import sys
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[1]
PACKAGE_ID = "next90-m108-media-factory-campaign-briefing-renders"
PROOF_FLOOR_COMMIT = "ef3f006"


class M108CampaignBriefingProofTests(unittest.TestCase):
    def test_generated_release_proof_tracks_closed_m108_campaign_briefing_package(self):
        with tempfile.TemporaryDirectory() as tmp:
            subprocess.run(
                [
                    sys.executable,
                    "scripts/ai/materialize_media_release_proof.py",
                    "--out-dir",
                    tmp,
                    "--status",
                    "passed",
                ],
                cwd=ROOT,
                check=True,
                stdout=subprocess.DEVNULL,
            )
            release = json.loads((Path(tmp) / "MEDIA_LOCAL_RELEASE_PROOF.generated.json").read_text(encoding="utf-8"))

        package = next(
            candidate
            for candidate in release["successor_packages"]
            if candidate["package_id"] == PACKAGE_ID
        )

        self.assertEqual(4459920059, package["frontier_id"])
        self.assertEqual(108, package["milestone_id"])
        self.assertEqual("complete", package["status"])
        self.assertEqual("verify_closed_package_only", package["completion_action"])
        self.assertEqual(PROOF_FLOOR_COMMIT, package["proof_floor_commit"])
        self.assertEqual(
            ["campaign_briefing_bundle_rendering", "campaign_artifact_receipts"],
            package["owned_surfaces"],
        )
        self.assertIn(
            "campaign briefing bundles require a requested-locale ColdOpen entry before rendering",
            package["campaign_briefing_guards"],
        )
        self.assertIn(
            "campaign briefing bundles require a requested-locale MissionBriefing entry before rendering",
            package["campaign_briefing_guards"],
        )
        self.assertIn(
            "campaign briefing bundles render media, caption, and preview siblings per locale entry",
            package["campaign_briefing_guards"],
        )
        self.assertIn(
            "campaign briefing bundles keep the requested locale as the primary sibling and require every other locale to be a fallback sibling",
            package["campaign_briefing_guards"],
        )
        self.assertIn(
            "campaign briefing bundles require locale-matched cold-open and mission briefing siblings for every requested and fallback locale bundle",
            package["campaign_briefing_guards"],
        )
        self.assertIn(
            "campaign briefing receipts preserve asset urls, locale receipt ids, locale bundle receipt ids, and per-entry job ids",
            package["campaign_briefing_guards"],
        )
        self.assertIn(
            "campaign briefing bundle receipts also preserve first-class requested-locale and fallback bundle summary fields",
            package["campaign_briefing_guards"],
        )
        self.assertIn(
            "campaign briefing locale bundle and fallback sibling receipts preserve slot-aware caption and preview sibling ids",
            package["campaign_briefing_guards"],
        )
        self.assertIn(
            "campaign briefing dedupe keys include requested locale, slot, entry locale, fallback posture, category, output format, and caller dedupe",
            package["campaign_briefing_guards"],
        )
        self.assertIn(
            "campaign briefing artifact receipts preserve approval state, retention state, and storage class alongside asset urls",
            package["campaign_briefing_guards"],
        )
        self.assertIn(
            "campaign briefing receipt hashes use length-prefixed locale, artifact-kind, and output-format segments so delimiter-heavy locale variants cannot collapse onto one receipt id",
            package["campaign_briefing_guards"],
        )
        self.assertIn(
            "campaign briefing normalized locale-bundle ordering keeps locale receipts, locale bundle receipts, fallback sibling receipts, and summary job ids stable when callers reorder the same bundle entries",
            package["campaign_briefing_guards"],
        )
        self.assertIn(
            "campaign briefing package authority requires exactly one canonical queue row per mirror and exactly one registry task block",
            package["campaign_briefing_guards"],
        )
        self.assertIn(
            "src/Chummer.Media.Factory.Runtime/Assets/CampaignBriefingBundleService.cs",
            package["proof"],
        )
        self.assertIn(
            "tests/CampaignBriefingBundleSmoke/Program.cs",
            package["proof"],
        )
        self.assertIn(
            "docs/NEXT90_M108_CAMPAIGN_BRIEFING_PROOF_FLOOR.md",
            package["proof"],
        )

    def test_m108_proof_sources_stay_worker_safe(self):
        combined = "\n".join(
            (
                (ROOT / "docs/NEXT90_M108_CAMPAIGN_BRIEFING_PROOF_FLOOR.md").read_text(encoding="utf-8"),
                (ROOT / "scripts/ai/materialize_media_release_proof.py").read_text(encoding="utf-8"),
                (ROOT / "src/Chummer.Media.Factory.Runtime/Assets/CampaignBriefingBundleService.cs").read_text(
                    encoding="utf-8"
                ),
            )
        ).lower()

        forbidden = (
            "task_local_telemetry.generated.json",
            "active_run_handoff.generated.md",
            "operator telemetry",
            "supervisor status",
            "status query",
            "eta query",
            "active-run helper",
        )

        for token in forbidden:
            self.assertNotIn(token, combined, token)


if __name__ == "__main__":
    unittest.main()
