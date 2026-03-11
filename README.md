# Chummer Media Factory

Render-only media and asset lifecycle service for Project Chummer.

This repo exists to own:

- asset/job lifecycle
- render pipelines
- storage adapters
- signed access URLs
- approval-state persistence for rendered assets

This repo must not own:

- rules math
- session relay
- Spider analysis
- lore retrieval
- provider routing
- narrative generation policy

Current status: scaffold-stage bootstrap. `Chummer.Media.Contracts` is now established as the canonical render-only contract plane for this repo, with package metadata and namespace policy checks in verification.

Bootstrap layout:

- `Chummer.Media.Factory.slnx` is the repo solution entrypoint
- `src/Chummer.Media.Contracts` is the canonical render-only package plane scaffold
- `scripts/ai/verify.sh` restores and builds the bootstrap in isolation
- `docs/MF-005-service-seams-and-handoffs.md` defines cross-service seam ownership and extraction checklist coverage
- `docs/EXTRACT-006-SEAM-02-run-services-handoff-conformance-matrix.md` defines required ingress payloads and forbidden run-services ownership paths for seam cutover

`Chummer.Media.Contracts` now owns only three contract families:

- rendering requests for deterministic document, portrait, and video execution
- render job queue state for claim, dedupe, retry, and completion tracking
- asset manifests, catalog entries, and lifecycle state for approval, persistence, TTL, and lineage

Ownership details seeded in the current scaffold:

- queue-owned render jobs define queue name, dedupe scope/key, retry timing, and supersession
- asset manifests own storage lineage while catalog entries own lookup metadata for approved render outputs

The package does not define narrative briefs, canon decisions, routing policy, delivery policy, or campaign/session orchestration contracts. Those remain upstream in `chummer.run-services`.
