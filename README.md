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

Current status: scaffold-stage bootstrap. `Chummer.Media.Contracts` is the canonical render-only contract plane for this repo, with package metadata and namespace policy checks in verification.

Current maturity note:

- the boundary is now documented honestly
- the package plane is real
- the service is still early until live render execution cutover and lifecycle proof stop living mostly in upstream repos and evidence docs

Operator bridge:

- `scripts/render_guide_asset.py` is the current operator-run bridge that lets upstream Chummer guide refreshes hand image execution to Media Factory instead of talking to provider adapters directly.
- The bridge is intentionally narrow for now: it owns receipt emission and the render seam, while using EA's executable `1min` image tool under the hood until more adapters move fully into this repo.

The package does not define narrative briefs, canon decisions, routing policy, delivery policy, or campaign/session orchestration contracts. Those remain upstream in `chummer6-hub`.
