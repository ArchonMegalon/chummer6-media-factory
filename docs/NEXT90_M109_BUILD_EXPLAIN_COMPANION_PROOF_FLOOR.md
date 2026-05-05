# Next90 M109 Build Explain Companion Proof Floor

Package: `next90-m109-media-factory-build-explain-bundles`
Frontier: `4037265286`
Status: `complete`
Completion action: `verify_closed_package_only`
Proof floor commit: `7d5a0167`
Owned surfaces: `build_explain_companion_rendering`, `explain_artifact_receipts`

This package is implemented repo-locally in `chummer6-media-factory` as a render-verified lane for approved Build Lab explain packets. The runtime accepts only approved packet ids plus sibling artifact payloads and emits media-factory receipts for video, audio, preview-card, and packet companions without mutating engine truth.

Current closure posture:

- `BuildExplainCompanionRenderingService.cs` renders approved explain packet siblings through media-factory job execution only.
- `MediaFactoryContracts.cs` defines `BuildExplainCompanionRenderRequest`, `BuildExplainCompanionRenderReceipt`, `BuildExplainCompanionReadyRef`, `BuildExplainCompanionRoleReceiptGroup`, sibling artifact roles, first-class ready refs, grouped role receipts, and grouped caption/preview receipt rows.
- `Chummer.Media.Contracts/README.md` records the `BuildExplainCompanion*` family as a render-verified contract surface with approved explain packet sibling refs plus first-class role, caption, and preview receipt rows.
- generated repo-local release proof pins the exact M109 package identity: assigned title, task, work-task id `109.3`, wave `W9`, repo `chummer6-media-factory`, allowed paths `src/tests/docs/scripts`, owned surfaces, and frontier id `4037265286`.
- request-level `RenderingId`, approved explain packet id, explain packet revision id, and source normalize surrounding whitespace before scope enforcement, dedupe, and receipt emission so valid padded retries keep stable job ids and receipt ids.
- null artifact lists and null sibling entries now fail closed with explicit request-scoped validation instead of leaking incidental runtime null-reference failures during packet normalization.
- sibling payloads must stay scoped to the approved explain packet id and explain packet revision id before media jobs enqueue, and JSON payloads must match those scope fields exactly instead of passing on substring mentions alone.
- JSON and keyed text scope values now trim surrounding whitespace before exact scope matching so padded approved explain payloads stay valid without reopening substring spoof paths.
- valid JSON payloads now fail closed when required scope fields are missing, so object, array, or string note payloads cannot bypass approved explain packet validation through substring fallback.
- non-JSON scope fallback now requires exact keyed values or delimited scope tokens, so near-match packet or revision ids cannot slip through by embedding the approved ids as substrings inside longer text payload values.
- case-insensitive duplicate caption and preview refs now canonicalize to one stable spelling before grouped receipt rows emit, so mixed-case ref variants cannot rewrite aggregate ref casing when callers reorder the same approved explain packet siblings.
- caption and preview refs now trim surrounding whitespace before grouped receipt rows and receipt hashes emit, so padded sibling refs cannot fork stable companion receipt ids or ready refs.
- build explain companion rendering requires one video, one audio, one preview-card, and one packet companion before the bundle can render.
- build explain video and audio siblings require caption refs, while build explain video, preview-card, and packet companions require preview refs.
- companion refs trim surrounding whitespace and stay unique case-insensitively per approved explain packet so downstream shelves cannot confuse sibling outputs with padded or mixed-case retries.
- bundle-scoped dedupe keys include approved explain packet id, explain packet revision id, rendering id, sibling role, category, output format, companion ref, and caller dedupe key.
- receipt hashes include caption and preview refs with length-prefixed segments so delimiter-heavy refs cannot collapse distinct companion outputs onto one receipt id.
- normalized sibling ordering keeps receipt ids, companion refs, ready refs, and grouped role/caption/preview receipt rows stable even when callers reorder the same approved explain packet artifacts.
- source or requested timestamp drift cannot rewrite stable job ids, receipt ids, ready refs, or grouped role receipts for the same approved explain packet siblings.
- grouped receipt rows preserve aggregate `JobIds`, grouped companion refs, first-class caption refs, preview refs, lifecycle truth, and direct asset urls so downstream shelves do not need to reconstruct explain evidence from raw artifact rows.
- `RenderedAtUtc` resolves to emitted media-job completion timestamps so later deduped retries cannot rewrite the bundle render time with a newer request timestamp.
- build explain package authority requires exactly one canonical queue row per mirror and exactly one registry task block.
- build explain package authority now requires exactly one canonical queue row per mirror, exactly one repo-local `.codex-design` queue mirror row, and exactly one registry task block per canonical and repo-local mirror so closure proof fails closed on duplicate or drifting successor-wave entries.
- generated and published proof artifacts now require exactly one M109 successor package entry before package drift checks run, so duplicate materialized rows cannot hide behind a successful first-match lookup.
- repo-local proof materialization now fails closed when either generated or published proof artifact repeats any successor package id, so package-specific drift checks cannot be masked by duplicate package rows elsewhere in the same proof file.
- package-scoped queue and registry mirrors must now match the canonical M109 package/task blocks exactly, so repo-local proof fails closed when scoped fields such as `status` drift away from the fleet and design mirrors.
- generated M109 package entries now match the canonical queue scope directly and the canonical registry task owner/title scope directly, so proof materialization cannot stay green if hard-coded release-proof fields drift away from successor-wave truth.
- proof citations now stay anchored to repo-local `src`, `tests`, `docs`, and `scripts` paths only, so closed-package evidence cannot drift into sibling surfaces while reusing unrelated receipts.
- published `MEDIA_LOCAL_RELEASE_PROOF.generated.json` and `ARTIFACT_PUBLICATION_CERTIFICATION.generated.json` entries must match a freshly materialized M109 package entry exactly, so repo-local published proof cannot silently drift away from the package-scoped verifier.
- canonical successor queue rows are now complete with `landed_commit: 7d5a0167`, so future slices can close this package only from the landed proof floor.
- the package-local verify entrypoint now materializes fresh repo-local release proof, checks capability signoff tokens, and rejects worker-unsafe blocked run-helper citations in proof sources, so future shards verify the exact landed proof stack instead of trusting stale generated files or re-discovering the surface.

Required proof commands:

- `python3 -m unittest tests/test_m109_successor_package_authority.py`
- `python3 -m unittest tests/test_m109_build_explain_proof.py`
- `python3 -m unittest tests/test_build_explain_companion_rendering.py`
- `dotnet run --project tests/BuildExplainCompanionSmoke/Chummer.Media.Factory.BuildExplainCompanionSmoke.csproj --configuration Release --nologo --verbosity quiet`
- `./scripts/ai/verify_m109_build_explain_companion.sh`

Current closure posture:

- package-scoped proof is green for the current M109 worktree: `python3 -m unittest tests.test_m109_successor_package_authority tests.test_m109_build_explain_proof tests.test_build_explain_companion_rendering`, `dotnet run --project tests/BuildExplainCompanionSmoke/Chummer.Media.Factory.BuildExplainCompanionSmoke.csproj --configuration Release --nologo --verbosity quiet`, and `./scripts/ai/verify_m109_build_explain_companion.sh` all exit `0`.
- canonical successor queue rows now pin the assigned M109 frontier id `4037265286`, repo-local `.codex-design` queue and registry mirrors pin that same M109 frontier and task identity, and queue plus registry can cite `landed_commit: 7d5a0167` honestly.
- future shards should reuse this proof floor and the package-scoped verify results instead of re-discovering whether build explain companion rendering is implemented.

Repo-local surface:

- `src/Chummer.Media.Factory.Runtime/Assets/BuildExplainCompanionRenderingService.cs`
- `src/Chummer.Media.Contracts/Compatibility/RunServices/MediaFactoryContracts.cs`
- `tests/BuildExplainCompanionSmoke/Program.cs`
- `tests/test_m109_successor_package_authority.py`
- `tests/test_build_explain_companion_rendering.py`
- `tests/test_m109_build_explain_proof.py`
- `docs/NEXT90_M109_BUILD_EXPLAIN_COMPANION_PROOF_FLOOR.md`
- `scripts/ai/verify.sh`
- `scripts/ai/verify_m109_build_explain_companion.sh`
