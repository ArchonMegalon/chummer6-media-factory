# GitHub Codex Review

PR: https://github.com/ArchonMegalon/chummer6-media-factory/pull/3

Findings:
- [high] scripts/render_guide_asset.py [correctness] MF-REVIEW-OPENAI-REF-001
`render_asset()` still calls `_render_with_openai_edits(..., reference_image=reference_image or Path(""))` (around line 1046), so missing input becomes `.`.; `_render_with_openai_edits()` only checks `exists()` (around line 554) and then calls `reference_image.read_bytes()` (around line 565), which raises `IsADirectoryError` for `.` instead of a deterministic `media_factory:*` validation error.; Repro in this review: `CHUMMER_MEDIA_FACTORY_IMAGE_BACKEND=openai_edits OPENAI_API_KEY=dummy python3 scripts/render_guide_asset.py ...` failed with `IsADirectoryError: [Errno 21] Is a directory: '.'`.
Expected fix: Require `--reference-image` for `openai_edits`, validate `reference_image.is_file()` before reads, and fail with stable `media_factory:*` validation errors for missing/invalid paths.
- [high] scripts/render_guide_asset.py [state] MF-REVIEW-HEALTH-STATE-002
`_load_health_registry()` parses JSON and immediately dereferences `loaded.get("providers")` (around line 93) without confirming `loaded` is a dict.; If `guide_provider_health.json` is valid non-object JSON (e.g., `[]`), this raises `AttributeError: 'list' object has no attribute 'get'`.; Repro in this review: with `CHUMMER_MEDIA_FACTORY_STATE_DIR` pointed to a temp directory containing `guide_provider_health.json` = `[]`, calling `_load_health_registry()` raised `AttributeError`.
Expected fix: Normalize non-dict parsed JSON to `{}` before accessing `.get()` and preserve default provider structure.
- [high] scripts/ai/verify.sh [tests] MF-REVIEW-TEST-COVERAGE-003
`verify.sh` checks dry-run, disabled backend, and bogus backend behavior (lines ~48-55) but has no assertion path for `openai_edits` missing/invalid reference images.; `verify.sh` has no resilience case for malformed/non-object health registry JSON via `CHUMMER_MEDIA_FACTORY_STATE_DIR`.; Both blocking regressions above pass the current verification lane undetected.
Expected fix: Add verify cases that assert deterministic failure for missing/invalid `openai_edits` reference images and no crash when health-registry JSON is malformed/non-object.
