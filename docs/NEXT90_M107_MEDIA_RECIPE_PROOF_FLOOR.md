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
- `e93f8f4` adds `RoleReceiptGroups` so each video, audio, preview-card, and packet-bundle sibling has a first-class grouped receipt row with receipt ids, job ids, publication refs, caption refs, preview refs, and artifact detail.
- `e93f8f4` adds aggregate `JobIds` on `StructuredMediaRecipeBundleReceipt` so publication surfaces can prove every emitted sibling has a media-factory job without reconstructing coverage from role groups.
- current worker hardening makes structured recipe job dedupe include artifact category, output format, and publication ref, rejects duplicate publication refs inside one bundle, and makes receipt hashes include caption and preview refs so two publication-ready outputs cannot share one render job only because the caller reused a dedupe key.
- current successor pass replaces delimiter-joined recipe job dedupe with a length-prefixed hash and proves delimiter-heavy category, output-format, publication-ref, and caller-dedupe values cannot collapse two publication-ready video outputs onto one media job.
- `6adf9a8` tightens completed-package closure authority so the Fleet queue mirror, canonical design queue mirror, registry task row, proof-floor note, and generated release proof all verify the same M107 media recipe package instead of reopening it.
- `9614cca` pins the queue-mirror closure guard so Fleet and design-owned queue rows must carry the same completed-package proof floor.
- generated release proof now pins `9614cca` as the current proof floor so publication receipts, lifecycle truth, output-ref dedupe safety, and queue-mirror closure authority resolve to the same closed-package guard.

Required proof commands:

- `python3 -m unittest tests/test_structured_media_recipe_execution.py tests/test_materialize_media_release_proof.py`
- `python3 -m unittest tests/test_m107_successor_closure_authority.py`
- `dotnet run --project tests/StructuredMediaRecipeSmoke/Chummer.Media.Factory.StructuredMediaRecipeSmoke.csproj --configuration Release --nologo --verbosity quiet`

Closed-package surface:

- `src/Chummer.Media.Factory.Runtime/Assets/StructuredMediaRecipeExecutionService.cs`
- `src/Chummer.Media.Contracts/Compatibility/RunServices/MediaFactoryContracts.cs`
- `tests/StructuredMediaRecipeSmoke/Program.cs`
- `tests/test_structured_media_recipe_execution.py`
- `scripts/ai/materialize_media_release_proof.py`
- `scripts/ai/verify.sh`
