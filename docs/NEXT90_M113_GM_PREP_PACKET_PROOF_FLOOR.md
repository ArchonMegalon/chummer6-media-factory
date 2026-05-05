# Next90 M113 GM Prep Packet Proof Floor

This repo-local proof floor closes `next90-m113-media-factory-gm-prep-packets` inside `chummer6-media-factory`.

## Package

- frontier id: `3813748639`
- milestone id: `113`
- package id: `next90-m113-media-factory-gm-prep-packets`
- proof floor commit: `TBD_COMMIT`
- owned surfaces: `gm_prep_packets`, `opposition_packet_artifacts`
- allowed paths: `src`, `tests`, `docs`, `scripts`

## Proof anchors

- `src/Chummer.Media.Factory.Runtime/Assets/GmPrepPacketBundleService.cs` renders governed opposition, scene, and prep-library entries into packet, preview, and optional briefing artifacts through media-factory job execution only.
- `src/Chummer.Media.Contracts/Compatibility/RunServices/MediaFactoryContracts.cs` defines `GmPrepPacketRenderRequest`, `GmPrepPacketBundleReceipt`, `GmPrepPacketEntryReceipt`, `GmPrepPacketSubjectReceiptGroup`, and `GmPrepPacketArtifactReceipt`.
- `tests/GmPrepPacketSmoke/Program.cs` proves opposition entries stay mandatory, packet refs stay unique, governed source-pack plus sibling packet/source-entry scope stays enforced, optional briefings stay bounded per entry, and delimiter-heavy variants cannot collapse dedupe or receipt ids.
- `tests/test_gm_prep_packet_rendering.py` fail-closes contract drift so GM prep packet rendering stays first-class and render-verified.
- `tests/test_m113_gm_prep_packet_proof.py` fail-closes the generated proof floor so M113 stays pinned as a completed package with the current closure commit.
- `tests/test_m113_successor_package_authority.py` fail-closes queue, registry, generated-proof, and do-not-reopen drift so future shards verify the closed package instead of repeating it.
- `scripts/ai/materialize_media_release_proof.py` emits the M113 closure package into repo-local release proof receipts.
- `.codex-studio/published/MEDIA_LOCAL_RELEASE_PROOF.generated.json` and `.codex-studio/published/ARTIFACT_PUBLICATION_CERTIFICATION.generated.json` must stay synchronized with the current M113 materializer output so published proof cannot drift behind the repo-local guard set.
- `scripts/ai/verify_m113_gm_prep_packets.sh` gives the package one repo-local verifier entrypoint for the authority tests and smoke proof.
- `scripts/ai/verify_m113_gm_prep_packets.sh` stays directly executable so workers can invoke the package verifier without wrapping it in an ad hoc shell fallback.
- `scripts/ai/verify.sh` calls the dedicated M113 verifier as part of the standard media-factory verify lane.
- canonical queue and registry evidence for M113 must cite the published generated proof outputs and the standard `scripts/ai/verify.sh` lane, so future shards can verify the closed package instead of rediscovering the same repo-local proof anchors.

## Guard conditions

- GM prep packet rendering stays render-verified by requiring a `GovernedSourcePackId`, `SourcePackRevisionId`, `PacketRef`, and `SourceEntryId` plus sibling-only payloads before any media job can enqueue
- parseable JSON GM prep payloads fail closed when required scope fields are missing or mismatched, so JSON strings, arrays, or wrong-sibling objects cannot bypass exact scope matching through text fallback
- non-JSON GM prep payloads require exact keyed values or delimiter-safe scope tokens, so near-match source pack ids, packet refs, and source entry ids cannot pass by raw substring collision
- GM prep packet rendering requires at least one opposition entry and keeps scene and prep-library entries optional within the same governed render request
- GM prep packet entries require packet and preview artifacts while briefing artifacts stay optional per governed entry
- GM prep packet rendering rejects duplicate source entries and duplicate packet refs inside one governed render request
- dedupe must stay bundle-scoped across governed source pack id, source pack revision id, rendering id, subject kind, source entry id, packet ref, artifact role, category, output format, and caller dedupe key
- current successor pass uses length-prefixed receipt hashing for subject kind, artifact role, and output format so delimiter-heavy variants cannot collapse onto one media job or receipt id
- entry receipt ids and subject receipt group ids must stay scoped to governed source pack id, source pack revision id, and rendering id so reused packet refs cannot alias grouped evidence across governed packs
- subject receipt groups must preserve grouped entry ids, packet refs, packet receipt ids, preview receipt ids, optional briefing receipt ids, aggregate job ids, and grouped artifact rows
- GM prep packet artifact receipts must preserve asset urls, approval state, retention state, and storage class alongside packet, preview, and optional briefing outputs
- GM prep packet package authority requires exactly one canonical queue row per mirror and exactly one registry task block
- proof must not cite task-local telemetry, active-run handoff notes, or blocked helper commands as closure evidence

## Verification

- `python3 -m unittest tests.test_m113_successor_package_authority tests.test_m113_gm_prep_packet_proof tests.test_gm_prep_packet_rendering` exits 0
- `dotnet run --project tests/GmPrepPacketSmoke/Chummer.Media.Factory.GmPrepPacketSmoke.csproj --configuration Release --nologo --verbosity quiet` exits 0
- `bash scripts/ai/verify_m113_gm_prep_packets.sh` exits 0
- `bash scripts/ai/verify.sh` exits 0
