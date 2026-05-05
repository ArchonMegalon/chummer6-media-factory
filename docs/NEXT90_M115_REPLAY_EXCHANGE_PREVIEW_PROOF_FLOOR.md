# Next90 M115 Replay, Recap, And Exchange Preview Proof Floor

This repo-local proof floor tracks `next90-m115-media-factory-exchange-previews` inside `chummer6-media-factory`.

## Package

- frontier id: `1547375325`
- milestone id: `115`
- package id: `next90-m115-media-factory-exchange-previews`
- proof floor commit: `unlanded`
- owned surfaces: `recap_preview_artifacts`, `replay_exchange_preview_artifacts`
- allowed paths: `src`, `tests`, `docs`, `scripts`

## Proof anchors

- `src/Chummer.Media.Factory.Runtime/Assets/ReplayExchangePreviewRenderingService.cs` renders recap, replay, and exchange preview-card plus inspectable sibling artifacts through media-factory job execution only.
- `src/Chummer.Media.Contracts/Compatibility/RunServices/MediaFactoryContracts.cs` defines `ReplayExchangePreviewRenderRequest`, `ReplayExchangePreviewRenderReceipt`, `ReplayExchangePreviewArtifactReceipt`, `ReplayExchangePreviewBundleReceipt`, `ReplayExchangePreviewKindReceiptGroup`, `ReplayExchangePreviewReadyRef`, `ReplayExchangePreviewArtifactRefReceipt`, `ReplayExchangePreviewCaptionRefReceipt`, and `ReplayExchangePreviewPreviewRefReceipt`.
- `tests/ReplayExchangePreviewSmoke/Program.cs` proves replay, recap, and exchange bundles must each stay first-class, emits preview-card and inspectable sibling receipts for every bundle, keeps kind-group, caption-ref, and preview-ref receipts first-class, prevents duplicate bundle refs and artifact refs, prevents delimiter-heavy refs from collapsing receipt ids, and proves replay-safe dedupe keeps job ids, receipt ids, and ready refs stable when `Source`, `RequestedAtUtc`, bundle ordering, or ref casing changes.
- `tests/test_replay_exchange_preview_rendering.py` fail-closes contract drift so recap/replay/exchange preview rendering stays render-verified.
- `tests/test_m115_replay_exchange_preview_proof.py` fail-closes proof-floor drift so repo-local package evidence does not fall behind the current implementation.
- `tests/test_m115_successor_package_authority.py` fail-closes queue, registry, design-mirror, and generated-proof drift while the slice remains in progress.
- `scripts/ai/materialize_media_release_proof.py` emits the M115 package into repo-local release proof receipts with the exact frontier id, work-task id, wave, repo, allowed-path scope, and owned surfaces assigned to this slice.
- `.codex-studio/published/MEDIA_LOCAL_RELEASE_PROOF.generated.json` and `.codex-studio/published/ARTIFACT_PUBLICATION_CERTIFICATION.generated.json` must stay synchronized with the current M115 materializer output so published proof cannot drift behind the repo-local guard set.
- `scripts/ai/verify_m115_replay_exchange_previews.sh` gives the package one repo-local verifier entrypoint for the authority tests and smoke proof.
- `scripts/ai/verify.sh` calls the dedicated M115 verifier as part of the standard media-factory verify lane.

## Guard conditions

- replay, recap, and exchange bundles must each stay first-class so portable artifact shelves can inspect every preview lane directly
- every bundle must preserve `BundleRef`, `LineageRef`, `CompatibilityReceiptId`, `ProvenanceReceiptId`, and `BoundedLossReceiptId` in both artifact receipts and grouped receipts
- preview-card and inspectable sibling artifacts must both preserve preview refs so downstream shelves can render bounded previews without re-querying artifact internals
- bundle refs and artifact refs must stay unique per render request so recap, replay, and exchange outputs cannot cross-link accidentally
- dedupe must stay bundle-scoped across bundle kind, bundle ref, lineage ref, compatibility receipt id, provenance receipt id, bounded-loss receipt id, role, category, output format, artifact ref, and caller dedupe key
- source and requested timestamp metadata must stay outside bundle-scoped dedupe and receipt identity so replayed preview renders cannot fork stable jobs, receipt ids, or ready refs
- normalized bundle ordering plus normalized caption and preview ref ordering must keep preview-card receipt ids, inspectable sibling receipt ids, ready refs, and grouped kind/caption/preview receipt rows stable when callers reorder the same bundles
- case-insensitive caption and preview dedupe must select one canonical ref spelling before receipt hashing and aggregate ref emission so mixed-case duplicate refs stay stable when callers reorder them
- current successor pass uses length-prefixed receipt hashing for caption and preview refs so delimiter-heavy variants cannot collapse onto one receipt id
- proof must not cite task-local telemetry, active-run handoff notes, or blocked helper commands as closure evidence

## Verification

- `python3 -m unittest tests.test_m115_successor_package_authority tests.test_m115_replay_exchange_preview_proof tests.test_replay_exchange_preview_rendering` exits 0
- `dotnet run --project tests/ReplayExchangePreviewSmoke/Chummer.Media.Factory.ReplayExchangePreviewSmoke.csproj --configuration Release --nologo --verbosity quiet` exits 0
- `bash scripts/ai/verify_m115_replay_exchange_previews.sh` exits 0
