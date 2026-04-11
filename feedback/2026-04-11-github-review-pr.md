# GitHub Codex Review

PR: https://github.com/ArchonMegalon/chummer6-media-factory/pull/3

Findings:
- [high] scripts/render_guide_asset.py [correctness] MF-REVIEW-OPENAI-REF-001
`render_asset()` still calls `_render_with_openai_edits(..., reference_image=reference_image or Path(""))` (line ~1046), so missing input resolves to `.`.; `_render_with_openai_edits()` only checks `exists()` then calls `read_bytes()` (lines ~554-565), which crashes on directories.; Repro: `CHUMMER_MEDIA_FACTORY_IMAGE_BACKEND=openai_edits OPENAI_API_KEY=dummy python3 scripts/render_guide_asset.py --prompt x --output /tmp/.../out.png --width 512 --height 512` produces `IsADirectoryError: [Errno 21] Is a directory: '.'`.
Expected fix: Require a real file (`is_file()`) for `openai_edits` reference input and return a deterministic `media_factory:*` validation error when missing/invalid.
- [high] scripts/render_guide_asset.py [state] MF-REVIEW-HEALTH-STATE-002
`_load_health_registry()` assumes parsed JSON is a dict and immediately calls `loaded.get("providers")` (line ~93).; If `guide_provider_health.json` contains valid non-object JSON (e.g. `[]`), this raises `AttributeError`.; Repro: with `CHUMMER_MEDIA_FACTORY_STATE_DIR` pointing to a temp dir where `guide_provider_health.json` is `[]`, calling `_load_health_registry()` fails with `AttributeError: 'list' object has no attribute 'get'`.
Expected fix: Coerce any non-dict parsed health JSON to `{}` before dereferencing and continue with default provider structure.
- [high] scripts/ai/verify.sh [tests] MF-REVIEW-TEST-COVERAGE-003
`scripts/ai/verify.sh` has no checks for `openai_edits`, `--reference-image`, or malformed health-registry state (`rg` returns no matches).; The two blocking regressions above currently pass the verify lane undetected.
Expected fix: Add verify cases that assert deterministic failure for missing/invalid `openai_edits` reference images and resilience when health registry JSON is malformed/non-object.
- [high] .codex-design/repo/IMPLEMENTATION_SCOPE.md [review] milestone-coverage-truth-drift-impl-scope
`WORKLIST.md` claims milestone-coverage modeling is complete in `.codex-design/repo/IMPLEMENTATION_SCOPE.md` with explicit `M0..M8` gates and ETA/completion basis.; Current `.codex-design/repo/IMPLEMENTATION_SCOPE.md` only contains a short milestone spine list (`M0..M8`) without explicit per-milestone mapping, coverage sources, completion gates, ETA/completion basis, or status.
Expected fix: Restore explicit milestone coverage modeling in `IMPLEMENTATION_SCOPE.md` (per-milestone mapping, coverage sources, completion gates, ETA/completion basis, status) or update queue/worklist claims to match actual scope truth.
