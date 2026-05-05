# Next90 M113 GM Prep Packet Proof Floor

Package: `next90-m113-media-factory-gm-prep-packets`
Frontier: `3813748639`
Status: `complete`
Completion action: `verify_closed_package_only`
Proof floor commit: `7d5a0167`
Owned surfaces: `gm_prep_packets`, `opposition_packet_artifacts`

This package is implemented repo-locally in `chummer6-media-factory` as a render-only lane for governed opposition, scene, and prep-library entries. The runtime accepts only governed source-pack scoped sibling payloads and emits packet, preview, and optional briefing receipts without becoming campaign, lore, or approval authority.

Current closure posture:

- `GmPrepPacketBundleService.cs` renders governed opposition, scene, and prep-library entries through media-factory job execution only.
- `MediaFactoryContracts.cs` defines `GmPrepPacketRenderRequest`, `GmPrepPacketBundleReceipt`, `GmPrepPacketEntryReceipt`, `GmPrepPacketSubjectReceiptGroup`, sibling artifact roles, and first-class packet, preview, briefing, and grouped subject receipt rows.
- generated repo-local release proof now pins the exact M113 package identity: title `Render opposition and GM prep packets from governed source packs`, task `Produce packet, preview, and optional briefing artifacts for opposition, scenes, and prep-library entries.`, work-task id `113.4`, wave `W11`, repo `chummer6-media-factory`, allowed paths `src/tests/docs/scripts`, owned surfaces, and frontier id `3813748639`.
- GM prep packet rendering stays render-verified by requiring a `GovernedSourcePackId`, `SourcePackRevisionId`, `PacketRef`, and `SourceEntryId` plus sibling-only payloads before any media job can enqueue.
- parseable JSON GM prep payloads fail closed when required scope fields are missing or mismatched, so JSON strings, arrays, or wrong-sibling objects cannot bypass exact scope matching through text fallback.
- non-JSON GM prep payloads require exact keyed values or delimiter-safe scope tokens, so near-match source pack ids, packet refs, and source entry ids cannot pass by raw substring collision.
- JSON and keyed text GM prep scope values trim surrounding whitespace before exact scope matching so padded governed payloads stay valid without reopening substring spoof paths.
- GM prep packet rendering fails closed when the request contains null entries or a governed entry drops its required packet or preview artifact before normalization continues.
- GM prep packet rendering requires at least one opposition entry and keeps scene and prep-library entries optional within the same governed render request.
- GM prep packet entries require packet and preview artifacts while briefing artifacts stay optional per governed entry.
- GM prep packet rendering rejects duplicate source entries and duplicate packet refs inside one governed render request.
- dedupe stays bundle-scoped across governed source pack id, source pack revision id, rendering id, subject kind, source entry id, packet ref, artifact role, category, output format, and caller dedupe key.
- receipt hashes use length-prefixed `subject-kind`, `artifact-role`, and `output-format` segments so delimiter-heavy variants cannot collapse onto one media job or receipt id.
- entry receipt ids and subject receipt group ids stay scoped to governed source pack id, source pack revision id, and rendering id so reused packet refs cannot alias grouped evidence across governed packs.
- subject receipt groups preserve grouped entry ids, packet refs, packet receipt ids, preview receipt ids, optional briefing receipt ids, aggregate `JobIds`, and grouped artifact rows.
- GM prep packet artifact receipts preserve asset urls, approval state, retention state, and storage class alongside packet, preview, and optional briefing outputs.
- `RenderedAtUtc` resolves from completed media jobs so later deduped retries cannot rewrite the bundle render time with a newer request timestamp.
- GM prep packet package authority requires exactly one canonical queue row per mirror and exactly one registry task block.
- generated and published proof artifacts now require exactly one M113 successor package entry before package drift checks run, so duplicate materialized rows cannot hide behind a successful first-match lookup.
- generated proof now requires unique M113 proof citations and unique GM prep guard rows before drift comparison, so closed-package evidence cannot silently duplicate itself while still matching canonical mirrors.
- generated proof now also requires unique M113 artifact roles and unique receipt rows before drift comparison, so closed-package evidence cannot silently duplicate rendered sibling claims or receipt surfaces while still matching canonical mirrors.
- generated proof now also requires the exact pinned M113 proof citations, artifact roles, and receipt rows before drift comparison, so closed-package evidence cannot quietly add sibling surfaces while still matching canonical mirrors.
- generated and published proof artifacts now also pin the exact M113 GM prep guard rows directly on the successor package entry, so repo-local closure proof cannot silently rewrite the closed-package scope rules while still matching on package identity alone.
- published proof tests now fail closed when either generated proof file carries duplicate M113 package rows or any M113 proof citation escapes the package-owned `src`, `tests`, `docs`, or `scripts` roots.
- every pinned M113 proof citation must now resolve to a repo-local file before generated or published closure proof can stay green, so closed-package evidence cannot cite deleted surfaces while still matching on strings alone.
- package-scoped queue and registry mirrors must now match the canonical M113 package and task blocks exactly, so repo-local proof fails closed when scoped fields such as `status`, `task`, or `work_task_id` drift away from the canonical mirrors.
- generated and published proof artifacts now pin `landed_commit: 7d5a0167` directly on the M113 successor package entry, so repo-local closure proof cannot drift behind the canonical queue receipt while still reporting `status: complete`.
- the package-local verify entrypoint now resolves the pinned `landed_commit` and `proof_floor_commit` anchors through local `git rev-parse --verify ...^{commit}` checks, so shared queue, registry, and generated-proof strings cannot keep the package green after commit anchor drift.
- the package-local verify entrypoint now also pins the full M113 package identity directly on the materialized successor package row, including `title`, `task`, `work_task_id`, `frontier_id`, `wave`, `repo`, `status`, `landed_commit`, `completion_action`, `proof_floor_commit`, `allowed_paths`, and `owned_surfaces`, so broad file-level string matches cannot mask row-local drift.
- proof citations now stay anchored to repo-local `src`, `tests`, `docs`, and `scripts` paths only, so closed-package evidence cannot drift into sibling surfaces while reusing unrelated receipts.
- published `MEDIA_LOCAL_RELEASE_PROOF.generated.json` and `ARTIFACT_PUBLICATION_CERTIFICATION.generated.json` entries must match a freshly materialized M113 package entry exactly, so repo-local published proof cannot silently drift away from the package-scoped verifier.
- canonical successor queue rows are now complete with `landed_commit: 7d5a0167`, so this package can only be considered closed from the landed proof floor.
- `verify_m113_gm_prep_packets.sh` now fail-closes when `scripts/ai/verify.sh` stops calling `bash scripts/ai/verify_m113_gm_prep_packets.sh`, so the shared media-factory verify lane cannot silently drop the closed M113 package.
- the package-local verify entrypoint now materializes fresh repo-local release proof, compares generated and published M113 package entries directly, and rejects worker-unsafe blocked run-helper citations across the full cited M113 proof set, including the contract lane, shared verify lane, smoke, and proof-test sources.
- `scripts/ai/verify_m113_gm_prep_packets.sh` stays directly executable so workers can invoke the package verifier without wrapping it in an ad hoc shell fallback.

Required proof commands:

- `python3 -m unittest tests/test_m113_successor_package_authority.py`
- `python3 -m unittest tests/test_m113_gm_prep_packet_proof.py`
- `python3 -m unittest tests/test_gm_prep_packet_rendering.py`
- `dotnet run --project tests/GmPrepPacketSmoke/Chummer.Media.Factory.GmPrepPacketSmoke.csproj --configuration Release --nologo --verbosity quiet`
- `./scripts/ai/verify_m113_gm_prep_packets.sh`

Current closure posture:

- package-scoped proof is green for the current M113 worktree: `python3 -m unittest tests.test_m113_successor_package_authority tests.test_m113_gm_prep_packet_proof tests.test_gm_prep_packet_rendering`, `dotnet run --project tests/GmPrepPacketSmoke/Chummer.Media.Factory.GmPrepPacketSmoke.csproj --configuration Release --nologo --verbosity quiet`, and `./scripts/ai/verify_m113_gm_prep_packets.sh` all exit `0`.
- canonical successor queue rows now pin the assigned M113 frontier id `3813748639`, canonical queue and registry mirrors pin the same M113 task identity, and queue plus registry can cite `landed_commit: 7d5a0167` honestly.
- future shards should reuse this proof floor and the package-scoped verify results instead of re-discovering whether GM prep packet rendering is implemented.

Repo-local surface:

- `src/Chummer.Media.Factory.Runtime/Assets/GmPrepPacketBundleService.cs`
- `src/Chummer.Media.Contracts/Compatibility/RunServices/MediaFactoryContracts.cs`
- `tests/GmPrepPacketSmoke/Program.cs`
- `tests/test_gm_prep_packet_rendering.py`
- `tests/test_m113_gm_prep_packet_proof.py`
- `tests/test_m113_successor_package_authority.py`
- `docs/NEXT90_M113_GM_PREP_PACKET_PROOF_FLOOR.md`
- `scripts/ai/verify.sh`
- `scripts/ai/verify_m113_gm_prep_packets.sh`
