# Next90 M107 Media Recipe Proof Floor

Package: `next90-m107-media-factory-recipe-execution`
Frontier: `1746209281`
Milestone: `107`
Status: `complete`
Completion action: `verify_closed_package_only`

This package is closed by repo-local media-factory evidence. Future shards must verify the structured recipe service, contracts, smoke proof, generated release proof, and canonical queue/registry rows instead of reopening implementation.

Proof floor:

- `47df6ab` executes structured video, audio, preview-card, and packet-bundle recipes as media-factory-owned render jobs.
- `a2a3702` tightens generated release proof so M107 closure pins package identity, frontier, owned surfaces, artifact roles, and first-class publication, caption, and preview receipt rows.
- `15fb6ef` pins that proof floor into the materializer and unit guard.
- `3dc59e0` guards M107 successor closure authority across the canonical queue row, registry task row, proof-floor note, and generated release proof.
- `e93f8f4` hardens generated proof with explicit structured publication-ready ref guards for per-artifact receipt, job, output-format, caption, preview, grouped caption, grouped preview, and packet-bundle preview receipt detail.
- `398f756` hardens structured recipe receipts with direct asset URLs on structured artifact, publication-ready, publication-ref, and grouped ref artifact rows so downstream shelves can publish without a second asset lookup hop.
- current successor pass adds aggregate `JobIds` and `PublicationRefs` onto grouped caption-ref and preview-ref receipt rows so shared evidence stays first-class without forcing consumers to reconstruct it from nested artifact receipts.
- current successor pass also preserves per-artifact `CaptionRefs` and `PreviewRefs` on publication-ref rows plus grouped caption-ref and preview-ref artifact rows so downstream publication shelves do not need to join back to the raw artifact list for publication-ready detail.
- `398f756` also pins `AssetUrl` into the generated M107 receipt-row inventory so the closure metadata reflects direct publication-ready asset refs, not only the runtime DTO fields.
- `e93f8f4` adds `RoleReceiptGroups` so each video, audio, preview-card, and packet-bundle sibling has a first-class grouped receipt row with receipt ids, job ids, publication refs, caption refs, preview refs, and artifact detail.
- `e93f8f4` adds aggregate `JobIds` on `StructuredMediaRecipeBundleReceipt` so publication surfaces can prove every emitted sibling has a media-factory job without reconstructing coverage from role groups.
- `398f756` makes structured recipe job dedupe include artifact category, output format, and publication ref, rejects duplicate publication refs inside one bundle, and makes receipt hashes include caption and preview refs so two publication-ready outputs cannot share one render job only because the caller reused a dedupe key.
- `398f756` replaces delimiter-joined recipe job dedupe with a length-prefixed hash and proves delimiter-heavy category, output-format, publication-ref, and caller-dedupe values cannot collapse two publication-ready video outputs onto one media job.
- `398f756` also replaces delimiter-joined receipt ref hashing with length-prefixed caption and preview ref segments so delimiter-heavy refs cannot collapse distinct publication-ready outputs onto one receipt id.
- `398f756` sorts role, caption, and preview receipt group ids and refs explicitly so replayed bundles keep stable publication evidence ordering even when callers reorder siblings.
- `398f756` ties `RenderedAtUtc` to the emitted media-job completion timestamps so a later deduped retry cannot rewrite the bundle render time just by sending a newer `RequestedAtUtc`.
- `6adf9a8` tightens completed-package closure authority so the Fleet queue mirror, canonical design queue mirror, registry task row, proof-floor note, and generated release proof all verify the same M107 media recipe package instead of reopening it.
- `9614cca` pins the queue-mirror closure guard so Fleet and design-owned queue rows must carry the same completed-package proof floor.
- generated release proof now pins `398f756` as the current proof floor so publication receipts, direct asset urls, stable replay ordering, length-prefixed ref hashing, and queue-mirror closure authority resolve to the same closed-package guard.
- generated release proof also cites `tests/test_m107_successor_closure_authority.py` and `scripts/ai/materialize_media_release_proof.py` directly so future shards can verify the closure-authority guard from repo-local proof instead of inferring it only from queue or registry rows.

Required proof commands:

- `python3 -m unittest tests/test_structured_media_recipe_execution.py tests/test_materialize_media_release_proof.py`
- `python3 -m unittest tests/test_m107_successor_closure_authority.py`
- `dotnet run --project tests/StructuredMediaRecipeSmoke/Chummer.Media.Factory.StructuredMediaRecipeSmoke.csproj --configuration Release --nologo --verbosity quiet`

Closed-package surface:

- `src/Chummer.Media.Factory.Runtime/Assets/StructuredMediaRecipeExecutionService.cs`
- `src/Chummer.Media.Contracts/Compatibility/RunServices/MediaFactoryContracts.cs`
- `tests/StructuredMediaRecipeSmoke/Program.cs`
- `tests/test_structured_media_recipe_execution.py`
- `tests/test_m107_successor_closure_authority.py`
- `scripts/ai/materialize_media_release_proof.py`
- `scripts/ai/verify.sh`
