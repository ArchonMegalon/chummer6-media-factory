# Chummer Media Factory

This repo is a presentation and artifact lane. It can publish screenshots, contact sheets, mockups, thumbnails, packaging visuals, and governed media bundles for the broader Chummer fleet.

It is not a proof authority for product behavior.

Use this repo for:
- release and feature artwork
- public proof thumbnails and contact sheets
- creator/publication visual packaging
- governed screenshot bundles that point back to first-party proof receipts

Do not use this repo alone to claim:
- public route availability
- Chummer5A desktop parity
- SR4, SR5, or SR6 ruleset depth
- OAuth or account-linking readiness
- support-case or install-flow closure

Those claims require matching receipts from the owning repos, typically:
- `chummer.run-services` for public routes, account flows, and support proofs
- `chummer-presentation` for Chummer5A visual and workflow parity
- `chummer-core-engine` for ruleset depth and capability boundaries

Every published media bundle must use `PublicMediaAssetProjection`, which binds the
exact Registry `CURRENT.json`, full v2 snapshot, manifest, decision, and provenance
bytes by content-addressed refs and SHA-256 digests. It also requires a curated,
approved, persisted, non-purged public asset lifecycle. Provider-private execution
evidence stays internal.

Fresh-checkout builds bootstrap the sole external runtime contract from the exact
Registry owner commit in `eng/package-plane.lock.json`; ambient sibling repositories
and mutable package feeds are not release inputs.
