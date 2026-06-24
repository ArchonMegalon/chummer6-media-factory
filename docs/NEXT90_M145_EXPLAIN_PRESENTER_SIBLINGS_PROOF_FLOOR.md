# Next90 M145 Explain Presenter Siblings Proof Floor

Package: `next90-m145-media-factory-explain-presenter-siblings`
Frontier: `2090633046`
Status: `complete`
Completion action: `verify_closed_package_only`
Proof floor commit: `7d5a0167`
Owned surfaces: `explain_presenter_siblings:media_factory`, `explain_audio_video:media_factory`

This package is implemented repo-locally in `chummer6-media-factory` as a render-only lane for approved explanation packets. The runtime accepts only approved packet ids, revision ids, grounding scope refs, first-party text fallback, and sibling artifact payloads; it emits media-factory receipts for optional audio and presenter-video siblings without becoming calculation authority.

Current closure posture:

- `ExplainPresenterSiblingRenderingService.cs` renders approved explanation-packet siblings through media-factory job execution only.
- `MediaFactoryContracts.cs` defines `ExplainPresenterSiblingRenderRequest`, `ExplainPresenterSiblingRenderReceipt`, `ExplainPresenterTextFallbackReceipt`, `ExplainPresenterSiblingRoleReceiptGroup`, sibling artifact roles, first-class ready refs, grouped role receipts, and grouped caption/preview receipt rows.
- `Chummer.Media.Contracts/README.md` records the `ExplainPresenter*` family as a render-verified contract surface with approved explanation-packet siblings, grounding-scope identity, and first-party text fallback receipts.
- request-level `RenderingId`, approved explanation packet id, explanation packet revision id, grounding scope ref, source, and first-party text fallback normalize surrounding whitespace before scope enforcement, dedupe, and receipt emission so valid padded retries keep stable job ids, receipt ids, and text fallback receipts.
- null artifact lists and null sibling entries fail closed with explicit request-scoped validation before approved explanation packet normalization continues.
- explain presenter payloads must stay scoped to the approved explanation packet id, explanation packet revision id, and grounding scope ref before media jobs enqueue, and JSON payloads must match those scope fields exactly instead of passing on substring mentions alone.
- parseable JSON payloads fail closed when required packet-identity or grounding-scope fields are missing or the root payload is not an object, so JSON strings, arrays, or note-only objects cannot bypass exact scope matching through text fallback.
- JSON and keyed text explain presenter scope values trim surrounding whitespace before exact scope matching so padded approved explanation payloads stay valid without reopening substring spoof paths.
- non-JSON scope fallback requires exact keyed values or delimited scope tokens so near-match packet, revision, or grounding-scope ids cannot slip through by substring collision.
- explain presenter audio and presenter siblings require caption refs while presenter-video siblings require preview refs.
- case-insensitive duplicate caption and preview refs canonicalize to one stable spelling before grouped receipt rows emit so mixed-case ref variants cannot rewrite aggregate receipt casing when callers reorder the same approved explanation packet siblings.
- companion refs are unique per approved explanation packet so downstream shelves cannot confuse optional audio and presenter outputs.
- bundle-scoped dedupe keys include approved explanation packet id, explanation packet revision id, grounding scope ref, rendering id, sibling role, category, output format, companion ref, and caller dedupe key.
- receipt hashes include caption refs, preview refs, and first-party text fallback so sibling receipts stay tied to their emitted explanation packet fallback posture.
- receipt hashes use length-prefixed caption, preview, and text-fallback segments so delimiter-heavy values cannot collapse distinct presenter outputs onto one receipt id.
- normalized sibling ordering keeps receipt ids, companion refs, ready refs, grouped role, caption, and preview receipt rows, and text fallback receipts stable when callers reorder the same approved explanation packet artifacts.
- source and requested timestamp metadata stay outside bundle-scoped dedupe and receipt identity so replayed approved explanation packet renders cannot fork stable job ids, receipt ids, ready refs, grouped role receipts, or text fallback receipts.
- companion ready refs preserve per-artifact ref, receipt id, job id, job state, output format, caption refs, preview refs, asset id, asset url, cache ttl, approval state, retention state, and storage class.
- first-party text fallback stays first-class in the render receipt and text fallback receipt so optional media surfaces never become the only explain surface.
- role, caption, and preview receipt groups preserve aggregate job ids, grouped companion refs, and grouped artifact rows so downstream shelves do not need to reconstruct explain presenter evidence from raw artifact receipts.
- rendered timestamps resolve from completed media jobs so later deduped retries cannot rewrite bundle render time with a newer request timestamp.
- explain presenter package authority requires exactly one canonical queue row per mirror and exactly one registry task block.
- canonical successor queue rows are now complete with `landed_commit: 7d5a0167`, so future slices can close this package only from the landed proof floor.
- explain presenter package authority requires exactly one canonical queue row per mirror, exactly one repo-local `.codex-design` queue mirror row, and exactly one registry task block per canonical and repo-local mirror.
- queue and registry mirrors must match the canonical M145 package and task blocks exactly so repo-local proof cannot drift on status or scoped fields.
- canonical successor queue rows now pin the assigned M145 frontier id `2090633046`, repo-local `.codex-design` queue and registry mirrors pin that same M145 frontier and task identity, and queue plus registry can cite that commit honestly once this package is landed.
- isolated M145 proof-floor commit exists only after the package is landed; until then, future shards should reuse this proof floor and the package-scoped verify results instead of re-discovering whether explain presenter sibling rendering is implemented.
- rejects worker-unsafe blocked run-helper citations in proof sources.
- `scripts/ai/verify_m145_explain_presenter_siblings.sh` gives the package one repo-local verifier entrypoint that materializes generated proof before scanning the proof floor and signoff text.
- published release proof and publication certification snapshots track the current M145 package entry exactly, and the package verifier fails closed if either published snapshot drifts from the freshly materialized M145 package row.
- package-scoped proof is green for the current M145 worktree: `python3 -m unittest tests.test_m145_successor_package_authority tests.test_m145_explain_presenter_proof tests.test_explain_presenter_sibling_rendering`, `dotnet run --project tests/ExplainPresenterSiblingSmoke/Chummer.Media.Factory.ExplainPresenterSiblingSmoke.csproj --configuration Release --nologo --verbosity quiet`, and `./scripts/ai/verify_m145_explain_presenter_siblings.sh` all exit `0`.

Verification:

- `python3 -m unittest tests.test_m145_successor_package_authority tests.test_m145_explain_presenter_proof tests.test_explain_presenter_sibling_rendering`
- `dotnet run --project tests/ExplainPresenterSiblingSmoke/Chummer.Media.Factory.ExplainPresenterSiblingSmoke.csproj --configuration Release --nologo --verbosity quiet`
- `./scripts/ai/verify_m145_explain_presenter_siblings.sh`

Proof sources:

- `src/Chummer.Media.Factory.Runtime/Assets/ExplainPresenterSiblingRenderingService.cs`
- `src/Chummer.Media.Contracts/Compatibility/RunServices/MediaFactoryContracts.cs`
- `src/Chummer.Media.Contracts/README.md`
- `tests/ExplainPresenterSiblingSmoke/Program.cs`
- `tests/test_explain_presenter_sibling_rendering.py`
- `tests/test_m145_successor_package_authority.py`
- `tests/test_m145_explain_presenter_proof.py`
- `scripts/ai/materialize_media_release_proof.py`
- `scripts/ai/verify_m145_explain_presenter_siblings.sh`
