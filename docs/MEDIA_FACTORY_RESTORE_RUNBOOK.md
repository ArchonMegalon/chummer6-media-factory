# Media Factory Restore Runbook

Purpose: keep the media-factory share of `F1` explicit and runnable.

This runbook is the operator-facing proof path for asset storage state, retention behavior, replay-safe render dedupe, and restore continuity after media execution left `chummer6-hub`.

## Scope

This drill covers:

- asset-lifecycle backup and restore continuity
- render-job backup and restore continuity
- replay-safe lifecycle mutation counters
- replay-safe render-job dedupe counters
- retention sweep behavior after restore
- pinned long-term asset continuity after restore

It does not claim ownership of:

- session/campaign truth
- narrative generation policy
- cross-provider routing policy outside render execution

## Canonical backup contract

- backup contract family: `media_factory_state_backup_v1`
- owner runtime: `Chummer.Media.Factory.Runtime`
- verification runner: `Chummer.Media.Factory.Runtime.Verify`

## Drill commands

Run from the repo root:

```bash
bash scripts/ai/verify.sh
dotnet run --project /docker/fleet/repos/chummer-media-factory/Chummer.Media.Factory.Runtime.Verify/Chummer.Media.Factory.Runtime.Verify.csproj
```

The verification runner must prove:

- pinned approval assets survive backup and restore
- long-term storage posture survives backup and restore
- replay-safe lifecycle mutation counts survive backup and restore
- replay-safe render dedupe counts survive backup and restore
- retention sweep still expires cache-only assets after restore
- expired jobs remain inspectable and age out correctly after restore
- the backup package still reports `media_factory_state_backup_v1`

## Restore acceptance

The media-factory side of `F1` is healthy when:

- the restore drill preserves at least one pinned long-term asset without source-local fallback
- approval and media pipeline projections still show replay counters after restore
- cache-only assets still expire under retention sweep after restore
- render-job dedupe does not forget prior replay-safe reuse after restore
- the restore runner completes through `Chummer.Media.Factory.Runtime.Verify`

If any of these conditions fail, the media-factory share of `F1` is not closed.
