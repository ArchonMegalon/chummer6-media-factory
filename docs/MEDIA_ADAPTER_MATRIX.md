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
| document render | media-factory contracts/runtime | live through owner-repo job and asset lifecycle runtime; backend choice stays media-factory-private | runtime-owned | runtime-owned | Document preview/pdf/thumbnail capability is expressed through owner contracts and verified capability signoff. |
| preview image | `scripts/render_guide_asset.py` | live through `onemin` via EA tool call | `CHUMMER_MEDIA_FACTORY_IMAGE_BACKEND` | `CHUMMER_MEDIA_FACTORY_ENABLE_IMAGE_EXECUTION` | Current guide/preview bridge is media-factory-owned and emits media-factory receipts. |
| route visual / route video | media-factory contracts + hosted consumers | live as owner-repo route/video artifact contract family with runtime-governed asset lifecycle | runtime-owned | runtime-owned | Provider expansion is future depth; upstream still sees only owner contracts, lifecycle, and receipts. |
| portrait / persona video | media-factory contracts + hosted consumers | live as owner-repo portrait/video capability family with runtime-governed asset lifecycle | runtime-owned | runtime-owned | Portrait/video families stay media-factory-private even when provider backends expand. |
| archive / retention storage | media-factory runtime | live inside asset lifecycle/runtime store | runtime-owned | restore/runbook path | Storage class, retention, and restore posture are now covered by `MEDIA_FACTORY_RESTORE_RUNBOOK.md`. |

## Rules

- Upstream repos may request media intent, but they do not choose provider adapters or provider credentials.
- Backend selection must stay inside media-factory-owned surfaces.
- Media execution must fail closed when `CHUMMER_MEDIA_FACTORY_ENABLE_IMAGE_EXECUTION=0`.
- Unsupported backend tokens must fail fast instead of silently falling back.
- Receipts must record the actual selected backend plus the controlling env vars, not a hard-coded provider label.

## Current gap

The live preview/image bridge is the only external-provider-backed lane today, but all capability families already resolve through owner-repo contracts, lifecycle/runtime services, restore evidence, and media-factory-private backend control. Future provider additions widen depth; they do not reopen ownership.
