from __future__ import annotations

import json
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
FREEZE = ROOT / "eng/governed-spatial.freeze.json"
FORBIDDEN_IMPLEMENTATION_MARKERS = (
    "Matterport",
    "3DVista",
    "Pano2VR",
    "krpano",
    "PropertyQuarry",
    "GovernedSpatialRender",
)


class GovernedSpatialFreezeTests(unittest.TestCase):
    def test_freeze_contract_remains_fail_closed(self) -> None:
        payload = json.loads(FREEZE.read_text(encoding="utf-8"))
        self.assertEqual(
            {
                "contract",
                "status",
                "providerExecutionAuthorized",
                "quotaUseAuthorized",
                "releaseScopeWidened",
                "requiredBeforeUnfreeze",
            },
            set(payload),
        )
        self.assertEqual("chummer.media.governed-spatial-freeze/v1", payload["contract"])
        self.assertEqual("blocked", payload["status"])
        self.assertIs(False, payload["providerExecutionAuthorized"])
        self.assertIs(False, payload["quotaUseAuthorized"])
        self.assertIs(False, payload["releaseScopeWidened"])
        self.assertEqual(5, len(payload["requiredBeforeUnfreeze"]))

    def test_no_spatial_provider_implementation_entered_source(self) -> None:
        sources = "\n".join(
            path.read_text(encoding="utf-8")
            for path in sorted((ROOT / "src").rglob("*.cs"))
        )
        for marker in FORBIDDEN_IMPLEMENTATION_MARKERS:
            self.assertNotIn(marker, sources)


if __name__ == "__main__":
    unittest.main()
