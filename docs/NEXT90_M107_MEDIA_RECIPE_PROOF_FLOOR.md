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
- this pass hardens generated proof with explicit structured publication-ready ref guards for per-artifact receipt, job, output-format, caption, preview, grouped caption, grouped preview, and packet-bundle preview receipt detail.
- this pass also adds `RoleReceiptGroups` so each video, audio, preview-card, and packet-bundle sibling has a first-class grouped receipt row with receipt ids, job ids, publication refs, caption refs, preview refs, and artifact detail.
- this pass adds aggregate `JobIds` on `StructuredMediaRecipeBundleReceipt` so publication surfaces can prove every emitted sibling has a media-factory job without reconstructing coverage from role groups.

Required proof commands:

- `python3 -m unittest tests/test_structured_media_recipe_execution.py tests/test_materialize_media_release_proof.py`
- `dotnet run --project tests/StructuredMediaRecipeSmoke/Chummer.Media.Factory.StructuredMediaRecipeSmoke.csproj --configuration Release --nologo --verbosity quiet`

Closed-package surface:

- `src/Chummer.Media.Factory.Runtime/Assets/StructuredMediaRecipeExecutionService.cs`
- `src/Chummer.Media.Contracts/Compatibility/RunServices/MediaFactoryContracts.cs`
- `tests/StructuredMediaRecipeSmoke/Program.cs`
- `tests/test_structured_media_recipe_execution.py`
- `scripts/ai/materialize_media_release_proof.py`
- `scripts/ai/verify.sh`
