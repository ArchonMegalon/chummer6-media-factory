# Media Adapter Matrix

Purpose: keep `MF-011` explicit.

This matrix inventories the live media adapter families and makes provider choice media-factory-private, switchable, and kill-switchable.

## Current control surface

- backend selector env: `CHUMMER_MEDIA_FACTORY_IMAGE_BACKEND`
- execution kill switch env: `CHUMMER_MEDIA_FACTORY_ENABLE_IMAGE_EXECUTION`
- current live backend token: `onemin`
- owner bridge: `scripts/render_guide_asset.py`

## Adapter families

| Family | Current owner surface | Current backend posture | Switchability | Kill switch | Notes |
|---|---|---|---|---|---|
| document render | media-factory contracts/runtime | planned owner-repo execution, no direct provider runner committed here yet | not live yet | n/a | Contract and asset/runtime boundary exists; provider execution depth still pending under `MF-011`/`MF-012`. |
| preview image | `scripts/render_guide_asset.py` | live through `onemin` via EA tool call | `CHUMMER_MEDIA_FACTORY_IMAGE_BACKEND` | `CHUMMER_MEDIA_FACTORY_ENABLE_IMAGE_EXECUTION` | Current guide/preview bridge is media-factory-owned and emits media-factory receipts. |
| route visual / route video | media-factory contracts + hosted consumers | planned owner-repo execution, no direct provider runner committed here yet | not live yet | n/a | Runtime and contract surfaces exist; provider executor remains future work. |
| portrait / persona video | media-factory contracts + hosted consumers | planned owner-repo execution, no direct provider runner committed here yet | not live yet | n/a | Hosted product flows consume owner-repo contracts/runtime assembly, but provider execution depth is still queued. |
| archive / retention storage | media-factory runtime | live inside asset lifecycle/runtime store | runtime-owned | restore/runbook path | Storage class, retention, and restore posture are now covered by `MEDIA_FACTORY_RESTORE_RUNBOOK.md`. |

## Rules

- Upstream repos may request media intent, but they do not choose provider adapters or provider credentials.
- Backend selection must stay inside media-factory-owned surfaces.
- Media execution must fail closed when `CHUMMER_MEDIA_FACTORY_ENABLE_IMAGE_EXECUTION=0`.
- Unsupported backend tokens must fail fast instead of silently falling back.

## Current gap

Only the preview/image bridge is live as a provider-backed adapter today. The remaining families already have contract/runtime ownership and downstream consumers, but still need owner-repo provider executors before `MF-011` can be closed.
