# GitHub Codex Review

PR: https://github.com/ArchonMegalon/chummer6-media-factory/pull/3

Findings:
- [high] src/Chummer.Media.Factory.Runtime/Assets/CreatorPublicationPlannerService.cs [contracts] contracts-campaign-truth-leak-creator-publication-planner
Runtime now depends on `Chummer.Campaign.Contracts` (`Chummer.Media.Factory.Runtime.csproj:5`) and adds `CreatorPublicationPlannerService` that consumes campaign/publication projections (`CreatorPublicationProjection`, `BuildLabHandoffProjection`) instead of render/job/lifecycle-only inputs (`CreatorPublicationPlannerService.cs:1,19,24`).; Service logic introduces publication workflow semantics (`queue_review`, `share_public_publication`, `refresh_publication_posture`) and campaign/publication evidence authoring (`CreatorPublicationPlannerService.cs:263-267`, plus campaign/publication narrative lines throughout).; Runtime verification also shifted to assert publication-planner behavior and campaign projection lanes (`Program.cs:100-191`), which is outside media-factory render-only mission/boundaries.
Expected fix: Remove campaign/publication planning from media-factory runtime/verify surfaces, drop the campaign-contract runtime dependency, and keep verification focused on render jobs/assets/lifecycle guarantees.
- [high] scripts/render_guide_asset.py [correctness] correctness-openai-edits-reference-image-path-fallback
`render_asset` passes `reference_image=reference_image or Path("")` into `_render_with_openai_edits` (`render_guide_asset.py:1041-1047`), so missing reference can become `.`.; _render_with_openai_edits` only checks `exists()` (`render_guide_asset.py:554`) and then unconditionally calls `reference_image.read_bytes()` (`render_guide_asset.py:565`), which raises `IsADirectoryError` for `Path(".")` instead of deterministic media-factory validation failure.
Expected fix: Require a non-empty file path and validate `is_file()` before reads; fail with stable media-factory error code for missing/invalid reference images.
- [high] scripts/ai/verify.sh [tests] tests-missing-openai-edits-reference-validation-coverage
Verification script exercises dry-run, disabled backend, and bogus backend paths (`verify.sh:54-60`) but has no check for `openai_edits` missing/invalid reference-image validation behavior.; Current regressions in `openai_edits` input validation can therefore pass `scripts/ai/verify.sh` undetected.
Expected fix: Add a verify test case for `openai_edits` with missing/invalid `--reference-image` and assert deterministic `media_factory:*` validation error output.
