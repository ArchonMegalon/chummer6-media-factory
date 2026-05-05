# Next90 M119 Starter Artifact Proof Floor

Package: `next90-m119-media-factory-starter-artifacts`
Frontier: `1413666751`
Status: `complete`
Completion action: `verify_closed_package_only`
Proof floor commit: `TO_BE_FILLED_M119_COMMIT`
Owned surfaces: `starter_primer_artifacts`, `first_session_briefing_artifacts`

This package is implemented repo-locally in `chummer6-media-factory` as a render-only lane for approved starter source packs. The runtime accepts only starter-pack scoped sibling payloads and emits localized starter primer, first-session briefing, and support-safe onboarding receipts without becoming campaign, rules, or approval authority.

Current closure posture:

- `StarterArtifactBundleService.cs` renders localized starter primer, first-session briefing, and support-safe onboarding siblings through media-factory job execution only.
- `MediaFactoryContracts.cs` defines `StarterArtifactBundleRenderRequest`, `StarterArtifactBundleReceipt`, `StarterArtifactReceipt`, `StarterArtifactReadyRef`, `StarterArtifactLocaleReceiptGroup`, `StarterArtifactBundleLocaleReceiptGroup`, `StarterArtifactArtifactRefReceipt`, `StarterArtifactCaptionRefReceipt`, `StarterArtifactPreviewRefReceipt`, and `StarterArtifactSupportNoteReceipt`.
- generated repo-local release proof now pins the exact M119 package identity: title `Render starter primer and first-session companion artifacts`, task `Produce localized starter primers, first-session briefings, and support-safe onboarding artifacts from approved source packs.`, work-task id `119.4`, wave `W14`, repo `chummer6-media-factory`, allowed paths `src/tests/docs/scripts`, owned surfaces, and frontier id `1413666751`.
- starter artifact rendering stays render-verified by requiring an `ApprovedStarterSourcePackId`, `SourcePackRevisionId`, `StarterLaneId`, and per-artifact locale plus sibling-only payloads before any media job can enqueue.
- parseable JSON starter artifact payloads fail closed when required scope fields are missing or mismatched, so JSON strings, arrays, or wrong-sibling objects cannot bypass exact scope matching through text fallback.
- non-JSON starter artifact payloads require exact keyed values or delimiter-safe scope tokens, so near-match source pack ids, starter lane ids, and locales cannot pass by raw substring collision.
- starter artifact bundles require requested-locale and fallback locale triads for starter primer, first-session briefing, and support-safe onboarding siblings, while fallback locales stay bounded to at most two locales.
- starter primer and first-session video/audio siblings require caption refs, video and preview-card siblings require preview refs, and support-safe onboarding siblings require bounded support-note refs.
- starter artifact rendering rejects duplicate artifact refs inside one starter-lane render request.
- dedupe stays bundle-scoped across approved starter source pack id, source pack revision id, starter lane id, bundle kind, role, locale, category, output format, artifact ref, and caller dedupe key.
- receipt hashes include caption, preview, and support-note refs so starter receipts stay tied to emitted companion evidence.
- receipt hashes use length-prefixed `locale`, `output-format`, `caption`, `preview`, and `support-note` segments so delimiter-heavy variants cannot collapse onto one media job or receipt id.
- normalized sibling ordering keeps receipt ids, ready refs, locale receipt groups, bundle-locale receipt groups, and aggregate ref receipts stable when callers reorder the same starter siblings.
- case-insensitive caption, preview, and support-note dedupe selects one canonical ref spelling before receipt hashing and aggregate ref emission, so mixed-case duplicate refs stay stable when callers reorder them.
- `RenderedAtUtc` resolves from completed media jobs so later deduped retries cannot rewrite the bundle render time with a newer request timestamp.
- starter artifact package authority requires exactly one canonical queue row per mirror and exactly one registry task block.
- generated and published proof artifacts now require exactly one M119 successor package entry before package drift checks run, so duplicate materialized rows cannot hide behind a successful first-match lookup.
- generated proof now requires unique M119 proof citations and unique starter artifact guard rows before drift comparison, so closed-package evidence cannot silently duplicate itself while still matching canonical mirrors.
- published proof tests now fail closed when either generated proof file carries duplicate M119 package rows or any M119 proof citation escapes the package-owned `src`, `tests`, `docs`, or `scripts` roots.
- package-scoped queue and registry mirrors must now match the canonical M119 package and task blocks exactly, so repo-local proof fails closed when scoped fields such as `status`, `task`, or `work_task_id` drift away from the canonical mirrors.
- proof citations now stay anchored to repo-local `src`, `tests`, `docs`, and `scripts` paths only, so closed-package evidence cannot drift into sibling surfaces while reusing unrelated receipts.
- published `MEDIA_LOCAL_RELEASE_PROOF.generated.json` and `ARTIFACT_PUBLICATION_CERTIFICATION.generated.json` entries must match a freshly materialized M119 package entry exactly, so repo-local published proof cannot silently drift away from the package-scoped verifier.
- canonical successor queue rows are now complete with `landed_commit: TO_BE_FILLED_M119_COMMIT`, so this package can only be considered closed from the landed proof floor.
- the package-local verify entrypoint now materializes fresh repo-local release proof, compares generated and published M119 package entries directly, and rejects worker-unsafe blocked run-helper citations in proof sources.
- `scripts/ai/verify_m119_starter_artifacts.sh` stays directly executable so workers can invoke the package verifier without wrapping it in an ad hoc shell fallback.

Required proof commands:

- `python3 -m unittest tests/test_m119_successor_package_authority.py`
- `python3 -m unittest tests/test_m119_starter_artifact_proof.py`
- `python3 -m unittest tests/test_starter_artifact_rendering.py`
- `dotnet run --project tests/StarterArtifactBundleSmoke/Chummer.Media.Factory.StarterArtifactBundleSmoke.csproj --configuration Release --nologo --verbosity quiet`
- `./scripts/ai/verify_m119_starter_artifacts.sh`

Current closure posture:

- package-scoped proof is green for the current M119 worktree: `python3 -m unittest tests.test_m119_successor_package_authority tests.test_m119_starter_artifact_proof tests.test_starter_artifact_rendering`, `dotnet run --project tests/StarterArtifactBundleSmoke/Chummer.Media.Factory.StarterArtifactBundleSmoke.csproj --configuration Release --nologo --verbosity quiet`, and `./scripts/ai/verify_m119_starter_artifacts.sh` all exit `0`.
- canonical successor queue rows now pin the assigned M119 frontier id `1413666751`, canonical queue and registry mirrors pin the same M119 task identity, and queue plus registry can cite `landed_commit: TO_BE_FILLED_M119_COMMIT` honestly.
- future shards should reuse this proof floor and the package-scoped verify results instead of re-discovering whether starter artifact rendering is implemented.

Repo-local surface:

- `src/Chummer.Media.Factory.Runtime/Assets/StarterArtifactBundleService.cs`
- `src/Chummer.Media.Contracts/Compatibility/RunServices/MediaFactoryContracts.cs`
- `tests/StarterArtifactBundleSmoke/Program.cs`
- `tests/test_starter_artifact_rendering.py`
- `tests/test_m119_starter_artifact_proof.py`
- `tests/test_m119_successor_package_authority.py`
- `docs/NEXT90_M119_STARTER_ARTIFACT_PROOF_FLOOR.md`
- `scripts/ai/verify.sh`
- `scripts/ai/verify_m119_starter_artifacts.sh`
