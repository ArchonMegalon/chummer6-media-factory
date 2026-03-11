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

The package does not define narrative briefs, canon decisions, routing policy, delivery policy, or campaign/session orchestration contracts. Those remain upstream in `chummer6-hub`.
