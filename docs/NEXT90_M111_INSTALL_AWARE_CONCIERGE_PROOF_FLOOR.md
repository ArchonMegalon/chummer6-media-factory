# Next90 M111 Install-Aware Concierge Proof Floor

This repo-local proof floor tracks `next90-m111-media-factory-concierge-bundles` inside `chummer6-media-factory`.

## Package

- frontier id: `4132724850`
- milestone id: `111`
- package id: `next90-m111-media-factory-concierge-bundles`
- proof floor commit: `unlanded`
- owned surfaces: `release_explainer_artifacts`, `support_closure_artifacts`, `public_concierge_companions`
- allowed paths: `src`, `tests`, `docs`, `scripts`

## Proof anchors

- `src/Chummer.Media.Factory.Runtime/Assets/InstallAwareConciergeBundleService.cs` renders release explainer, support closure, and public concierge companions through media-factory job execution only.
- `src/Chummer.Media.Contracts/Compatibility/RunServices/MediaFactoryContracts.cs` defines `InstallAwareConciergeRenderRequest`, `InstallAwareConciergeBundleReceipt`, `InstallAwareConciergeArtifactReceipt`, top-level `CaptionRefs`, `PreviewRefs`, and `SiblingNoteRefs`, `InstallAwareConciergeCompanionReadyRef`, `InstallAwareConciergeBundleReceiptGroup`, `InstallAwareConciergeRoleReceiptGroup`, `InstallAwareConciergeCaptionRefReceipt`, `InstallAwareConciergePreviewRefReceipt`, and `InstallAwareConciergeSiblingNoteReceipt`.
- `tests/InstallAwareConciergeSmoke/Program.cs` proves each concierge bundle kind requires video, audio, and preview-card siblings, publishes first-class per-bundle aggregate receipt rows, keeps caption, preview, and sibling-note grouped receipts first-class with bundle kinds, roles, and grouped asset urls, keeps payload scope install-aware, keeps bounded sibling notes enforced, prevents delimiter-heavy refs from collapsing dedupe or receipt ids, and proves replayed packets keep stable job ids, receipt ids, and companion-ready rows even when `Source` or `RequestedAtUtc` changes.
- `tests/test_install_aware_concierge_rendering.py` fail-closes contract drift so install-aware concierge rendering stays first-class and render-only.
- `tests/test_m111_successor_package_authority.py` fail-closes queue, registry, generated-proof, and package-scope drift while the slice remains in progress.
- `scripts/ai/materialize_media_release_proof.py` emits the M111 package into repo-local release proof receipts with the exact title, task, work-task id, wave, repo, allowed-path scope, and owned surfaces assigned to this slice.
- `.codex-studio/published/MEDIA_LOCAL_RELEASE_PROOF.generated.json` and `.codex-studio/published/ARTIFACT_PUBLICATION_CERTIFICATION.generated.json` must stay synchronized with the current M111 materializer output so published proof cannot drift behind the repo-local guard set.
- `scripts/ai/verify_m111_install_aware_concierge.sh` gives the package one repo-local verifier entrypoint for the authority tests and smoke proof.
- `scripts/ai/verify.sh` calls the dedicated M111 verifier as part of the standard media-factory verify lane.

## Guard conditions

- install-aware concierge payloads must stay scoped to `InstallAwarePacketId`, `InstalledBuildReceiptId`, and `ArtifactIdentityId` before any media job can enqueue, and non-JSON payloads must satisfy keyed or delimiter-safe scope matching instead of raw substring mentions
- request-level rendering, packet, installed-build, artifact-identity, and source ids normalize surrounding whitespace before scope enforcement so valid install-aware packets do not fail on padded caller input
- parseable JSON payloads fail closed when required scope fields are missing or the root payload is not an object, so JSON strings, arrays, or note-only objects cannot bypass exact install-aware scope matching through text fallback
- JSON and keyed text scope values trim surrounding whitespace before exact scope matching so padded install-aware payloads stay valid without reopening substring spoof paths
- release explainer, support closure, and public concierge bundle kinds must each emit `Video`, `Audio`, and `PreviewCard` companions before the bundle can render
- video and audio siblings must preserve caption refs, while video and preview-card siblings must preserve preview refs
- every concierge artifact must preserve at least one sibling note ref and keep `SiblingNoteRefs` bounded to at most two refs per artifact
- companion refs must stay unique per install-aware packet so release, support, and public concierge outputs cannot cross-link accidentally
- dedupe must stay bundle-scoped across install-aware packet id, installed build receipt id, artifact identity id, rendering id, bundle kind, artifact role, category, output format, companion ref, and caller dedupe key
- source and requested timestamp metadata must stay outside bundle-scoped dedupe, receipt identity, and companion-ready identity so replayed install-aware packets cannot fork stable jobs or receipt refs
- normalized artifact ordering plus normalized caption, preview, and sibling-note ref ordering must keep receipt ids, companion refs, ready refs, and grouped bundle/role/caption/preview/sibling-note receipt rows stable when callers reorder the same install-aware packet artifacts
- case-insensitive caption, preview, and sibling-note dedupe must select one canonical ref spelling before receipt hashing and aggregate ref emission so mixed-case duplicate refs stay stable when callers reorder them
- current successor pass uses length-prefixed receipt hashing for caption, preview, and sibling-note refs so delimiter-heavy variants cannot collapse onto one media job or receipt id
- top-level caption, preview, and sibling-note aggregate refs must stay first-class receipt rows so downstream shelves can surface bundle-wide concierge evidence without reconstructing it from grouped rows
- caption, preview, and sibling-note grouped receipt rows must preserve bundle kinds, roles, and grouped asset urls so downstream shelves can publish release, support, and public concierge evidence without reconstructing it from raw artifact receipts
- bundle receipt groups must preserve aggregate receipt ids, job ids, companion refs, caption refs, preview refs, sibling note refs, roles, and grouped artifact rows for each release, support, and public concierge sibling bundle
- rendered timestamps must resolve from completed media jobs so later deduped retries cannot rewrite bundle render time with a newer request timestamp
- install-aware concierge package authority requires exactly one canonical queue row per mirror and exactly one registry task block while the package remains unlanded
- proof must not cite task-local telemetry, active-run handoff notes, or blocked helper commands as closure evidence

## Verification

- `python3 -m unittest tests.test_m111_successor_package_authority tests.test_m111_install_aware_concierge_proof tests.test_install_aware_concierge_rendering` exits 0
- `dotnet run --project tests/InstallAwareConciergeSmoke/Chummer.Media.Factory.InstallAwareConciergeSmoke.csproj --configuration Release --nologo --verbosity quiet` exits 0
- `bash scripts/ai/verify_m111_install_aware_concierge.sh` exits 0
