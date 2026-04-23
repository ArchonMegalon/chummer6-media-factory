# Next90 M110 Runsite Orientation Proof Floor

This repo-local proof floor closes `next90-m110-media-factory-runsite-bundles` inside `chummer6-media-factory`.

## Package

- frontier id: `5126560638`
- milestone id: `110`
- package id: `next90-m110-media-factory-runsite-bundles`
- owned surfaces: `runsite_orientation_bundle`, `route_preview:artifact_receipts`
- allowed paths: `src`, `tests`, `docs`, `scripts`

## Proof anchors

- `src/Chummer.Media.Factory.Runtime/Assets/RunsiteOrientationBundleService.cs` renders host clips, route previews, audio companions, and optional tour siblings through media-factory job execution only.
- `src/Chummer.Media.Contracts/Compatibility/RunServices/MediaFactoryContracts.cs` defines `RunsiteOrientationBundleRequest`, `RunsiteOrientationBundleReceipt`, `RunsiteOrientationArtifactReceipt`, and `RunsiteRoutePreviewArtifactReceipt`.
- `tests/RunsiteOrientationBundleSmoke/Program.cs` proves host clips and route previews are mandatory, route preview receipts stay linked to route segment ids and media job ids, and replay-safe dedupe keeps artifact jobs stable.
- `tests/test_runsite_orientation_bundle_contracts.py` fail-closes contract drift so runsite orientation stays first-class and render-only.
- `tests/test_m110_successor_closure_authority.py` fail-closes queue, registry, generated-proof, and do-not-reopen drift so future shards verify the closed package instead of repeating it.
- `scripts/ai/materialize_media_release_proof.py` emits the M110 closure package into repo-local release proof receipts.
- `scripts/ai/verify.sh` runs the smoke project and contract tests as part of the standard media-factory verify lane.

## Guard conditions

- orientation bundles must reject requests that omit every host clip
- orientation bundles must reject requests that omit every route preview
- `PreviewTruthPosture` must stay `pre-session-orientation-only-not-tactical-truth`
- route preview artifact receipts must preserve `RouteSegmentId`, `ReceiptId`, `JobId`, `JobState`, `AssetId`, and `CacheTtl`
- dedupe must stay bundle-scoped across approved runsite pack, route summary, bundle id, artifact role, route segment, artifact category, output format, and caller dedupe key
- current successor pass replaces delimiter-joined orientation job dedupe and receipt hashing with length-prefixed hash segments so delimiter-heavy route preview variants cannot collapse onto one media job or receipt id
- proof must not cite task-local telemetry, active-run handoff notes, or blocked helper commands as closure evidence
