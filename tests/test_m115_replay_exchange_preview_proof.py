from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[1]


def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8")


class M115ReplayExchangePreviewProofTests(unittest.TestCase):
    def test_proof_floor_records_m115_scope_and_guards(self):
        proof = read("docs/NEXT90_M115_REPLAY_EXCHANGE_PREVIEW_PROOF_FLOOR.md")

        for token in (
            "next90-m115-media-factory-exchange-previews",
            "frontier id: `1547375325`",
            "milestone id: `115`",
            "proof floor commit: `unlanded`",
            "recap_preview_artifacts",
            "replay_exchange_preview_artifacts",
            "ReplayExchangePreviewRenderingService.cs",
            "MediaFactoryContracts.cs",
            "tests/ReplayExchangePreviewSmoke/Program.cs",
            "tests/test_replay_exchange_preview_rendering.py",
            "tests/test_m115_successor_package_authority.py",
            "scripts/ai/materialize_media_release_proof.py",
            "scripts/ai/verify_m115_replay_exchange_previews.sh",
            "scripts/ai/verify.sh",
            "replay, recap, and exchange bundles must each stay first-class",
            "preview-card and inspectable sibling artifacts must both preserve preview refs",
            "bundle refs and artifact refs must stay unique per render request",
            "source and requested timestamp metadata must stay outside bundle-scoped dedupe and receipt identity",
            "length-prefixed receipt hashing for caption and preview refs",
            "proof must not cite task-local telemetry, active-run handoff notes, or blocked helper commands as closure evidence",
        ):
            self.assertIn(token, proof, token)


if __name__ == "__main__":
    unittest.main()
