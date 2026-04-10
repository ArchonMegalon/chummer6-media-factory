# GitHub Codex Review
Status: READ (2026-04-10)

PR: https://github.com/ArchonMegalon/chummer6-media-factory/pull/3

Findings:
- [high] scripts/render_guide_asset.py [correctness] openai-edits-missing-reference-crash
OpenAI-edits path passes `reference_image=reference_image or Path("")` (render path), then `_render_with_openai_edits` only checks `.exists()` before `read_bytes()`. `Path("")` resolves to `.` so the check passes and `read_bytes()` crashes with `IsADirectoryError` instead of a controlled media-factory error.; Repro: `CHUMMER_MEDIA_FACTORY_IMAGE_BACKEND=openai_edits CHUMMER_MEDIA_FACTORY_OPENAI_API_KEY=dummy python3 scripts/render_guide_asset.py --prompt x --output /tmp/mf-openai-test.png --width 1024 --height 1024` -> uncaught `IsADirectoryError: [Errno 21] Is a directory: '.'`.; Verification script does not exercise the OpenAI backend path, so this regression is not caught.
Expected fix: Require a real file path for `--reference-image` before entering the OpenAI-edits call path, return a namespaced runtime error when missing/invalid, and add verify coverage for that branch.
- [high] Chummer.Media.Factory.Runtime.Verify/Program.cs [tests] runtime-verify-lifecycle-expiry-coverage-regression
Branch removed the post-sweep assertion that render jobs transition to and remain inspectable in `Expired` state (previously validated after retention sweep).; Current test only checks cache asset expiry and no longer validates render-job expiry lifecycle behavior.
Expected fix: Restore equivalent assertions proving render-job expiry state after sweep (inspectable + `MediaRenderJobState.Expired`) so lifecycle regression coverage is not reduced.
- [high] scripts/ai/materialize_media_release_proof.py [contracts] publication-certification-contract-drift-in-media-factory
New script introduces `chummer6-media-factory.artifact_publication_certification` and publication-lane assertions (`target.sheet-viewer`, `target.print-pdf-export`, `target.session-recap`, etc.).; Repo boundary guidance and ownership matrix keep publication/catalog/install/update truth outside media-factory; media-factory is render jobs/manifests/previews/asset lifecycle.
Expected fix: Remove or relocate publication-certification contract semantics to the owning publication/control surface, or explicitly re-scope to render-only media-factory receipts.
