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
approved, persisted, non-purged public asset lifecycle, with curation and provenance
agreeing on the exact asset id, content SHA-256, and canonical media-manifest SHA-256.
Provider-private execution evidence stays internal.

Fresh-checkout builds bootstrap the sole external runtime contract from the exact
Registry owner commit in `eng/package-plane.lock.json`; ambient sibling repositories
and mutable package feeds are not release inputs. The official .NET SDK archive is
digest-pinned, and its complete extracted inventory is authenticated before the first
`dotnet` execution.

## Local Origin Dossier worker

The Origin Dossier worker is the Chummer-owned execution boundary for premium
audiobook and selected-scene requests. The Hub writes provider-neutral requests;
this worker resolves protected provider configuration, renders once, and emits a
provider-redacted receipt. A matching successful receipt is reused without another
provider call.

Before the first local image build, create the governed package feed with
`scripts/ai/bootstrap_media_package_feed.py` and the exact SDK/archive from
`eng/package-plane.lock.json`. Then prepare a least-privilege provider env file:

```bash
python3 scripts/providers/prepare_origin_dossier_provider_env.py \
  --source /docker/EA/.env \
  --output /docker/fleet/secrets/chummer-media-factory/providers.env
```

The preparation step copies only the approved MagicFit and Unmixr settings and
discovers the current English voice ids for the provider-neutral product choices.
It does not synthesize audio or render video.

Build locally—no GitHub Actions are required or used:

```bash
docker build \
  --file Dockerfile.origin-dossier-worker \
  --tag chummer-origin-media-worker:local \
  .
```

`chummer.run-services/docker-compose.public-edge.yml` mounts the Hub state
read-only into the worker and shares only `/origin-media` for inbox requests,
outputs, receipts, and the worker heartbeat.
