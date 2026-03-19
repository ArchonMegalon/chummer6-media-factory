# chummer6-media-factory

Render-only media and asset lifecycle service for Chummer6.

This repo exists to own:

- asset and job lifecycle
- render pipelines
- storage adapters
- signed access URLs
- approval-state persistence for rendered assets

This repo must not own:

- rules math
- session relay
- lore retrieval
- provider routing outside render execution
- narrative generation policy

Current status: active runtime owner. `Chummer.Media.Contracts` is the canonical render-only contract plane for this repo, and `Chummer.Media.Factory.Runtime` now owns render-job plus asset-lifecycle execution outside `chummer6-hub`.

Current maturity note:

- the boundary is now documented honestly
- the package plane is real
- render-job and asset-lifecycle execution now live in this repo and are verified through local build plus cross-repo clean-room checks
- restore, retention, and replay-safe operator evidence are now exercised through `Chummer.Media.Factory.Runtime.Verify` and `docs/MEDIA_FACTORY_RESTORE_RUNBOOK.md`

Operator bridge:

- `scripts/render_guide_asset.py` is the current operator-run bridge that lets upstream Chummer guide refreshes hand image execution to Media Factory instead of talking to provider adapters directly.
- The bridge is intentionally narrow for now: it owns receipt emission and the render seam, while using EA's executable `1min` image tool under the hood until more adapters move fully into this repo.
- Provider choice for the live image bridge is controlled inside this repo with `CHUMMER_MEDIA_FACTORY_IMAGE_BACKEND`, and image execution can be failed closed with `CHUMMER_MEDIA_FACTORY_ENABLE_IMAGE_EXECUTION=0`.
- Unsupported backend tokens fail fast, and receipts now capture the actual selected backend plus the controlling env vars.
- The current family inventory and switch/kill-switch posture are tracked in `docs/MEDIA_ADAPTER_MATRIX.md`.

The package does not define narrative briefs, canon decisions, routing policy, delivery policy, or campaign/session orchestration contracts. Those remain upstream in `chummer6-hub`.
