from __future__ import annotations

import hashlib
import json
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
LOCK = ROOT / "eng/design-mirror.lock.json"
MIRROR_ROOT = ROOT / ".codex-design/product"
VERIFIER = ROOT / "scripts/ai/verify_design_mirror.py"


class DesignMirrorLockTests(unittest.TestCase):
    def test_mirror_lock_pins_external_owner_commit_and_exact_bytes(self) -> None:
        lock = json.loads(LOCK.read_text(encoding="utf-8"))
        self.assertEqual("chummer.media.design-mirror-lock/v1", lock["contract"])
        self.assertEqual("ArchonMegalon/chummer6-design", lock["repository"])
        self.assertEqual(40, len(lock["commit"]))
        paths = [row["path"] for row in lock["files"]]
        self.assertEqual(sorted(paths), paths)
        self.assertEqual(len(paths), len(set(paths)))
        for row in lock["files"]:
            mirror = MIRROR_ROOT / row["path"]
            self.assertFalse(mirror.is_symlink())
            self.assertEqual(
                row["sha256"], hashlib.sha256(mirror.read_bytes()).hexdigest()
            )

    def test_verifier_has_no_ambient_absolute_design_checkout(self) -> None:
        verifier = VERIFIER.read_text(encoding="utf-8")
        self.assertNotIn('/docker/chummercomplete/chummer-design', verifier)
        self.assertNotIn("--repair", verifier)


if __name__ == "__main__":
    unittest.main()
