from __future__ import annotations

import json
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
LOCK = ROOT / "eng/historical-proof-anchors.lock.json"
SCRIPT = ROOT / "scripts/ai/verify_historical_proof_anchors.py"


class HistoricalProofAnchorTests(unittest.TestCase):
    def test_anchor_lock_uses_full_unique_commit_identities(self) -> None:
        lock = json.loads(LOCK.read_text(encoding="utf-8"))
        self.assertEqual(
            "chummer.media.historical-proof-anchors-lock/v1", lock["contract"]
        )
        self.assertEqual(
            "https://github.com/ArchonMegalon/chummer6-media-factory.git",
            lock["repositoryUrl"],
        )
        names = [row["name"] for row in lock["commits"]]
        shas = [row["gitObjectSha1"] for row in lock["commits"]]
        self.assertEqual(sorted(names), names)
        self.assertEqual(len(names), len(set(names)))
        self.assertEqual(len(shas), len(set(shas)))
        self.assertTrue(all(len(commit) == 40 for commit in shas))

    def test_resolution_recomputes_archived_git_object_identity_without_network(self) -> None:
        script = SCRIPT.read_text(encoding="utf-8")
        self.assertIn('f"commit {len(payload)}\\0"', script)
        self.assertIn("hashlib.sha1", script)
        self.assertNotIn("git fetch", script)
        self.assertNotIn("subprocess", script)


if __name__ == "__main__":
    unittest.main()
