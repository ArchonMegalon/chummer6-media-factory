# Worklist Queue

Purpose: keep the live media-factory queue readable. Historical duplicate publication notes and queue-overlay churn now live in `AUDIT_LOG.md`.

## Status Keys
- `queued`
- `in_progress`
- `blocked`
- `done`

## Queue
| ID | Status | Priority | Task | Owner | Notes |
|---|---|---|---|---|---|
| MF-006 | done | P1 | Materialize milestone coverage for the scaffold-stage extraction. | agent | Closed: milestone truth is explicit, and the repo no longer pretends “scaffold exists” means “boundary is complete.” |
| EXTRACT-001A | done | P1 | Make `Chummer.Media.Contracts` a real canonical render-only package plane. | agent | Closed: package metadata, namespace policy, Release pack verification, and evidence are in place. |
| EXTRACT-006 | done | P1 | Prove hub-to-media seam cutover expectations instead of hand-waving them. | agent | Closed: acceptance artifacts and verification now spell out the render-only handoff contract. |
| EXTRACT-007 | done | P1 | Materialize the asset-kernel backlog and completion evidence. | agent | Closed: manifests, binary storage, render jobs, previews, TTL/retention, and lineage expectations are explicit and evidenced. |
| EXTRACT-008 | done | P1 | Keep media DTOs render-only and push narrative/delivery meaning upstream. | agent | Closed: DTO boundary split evidence now exists instead of hiding in queue prose. |
| MF-007 | done | P2 | Archive duplicate queue/publication churn out of the live media worklist. | agent | Completed 2026-03-14: historical queue replay moved to `AUDIT_LOG.md`, and this file now reflects the real service boundary. |

## Current repo truth

- Repo-local live queue: non-empty (`mode: prepend`, 5 active items in `.codex-studio/published/QUEUE.generated.yaml` as of 2026-03-14)
- This repo is still early, but it is now honestly early: scaffold-stage execution proof exists, and the remaining blocker is live render cutover depth, not undocumented confusion
- Remaining program blocker still lives in central design truth as `C1`
- Current queue head is the seam milestone-mapping duplicate; the package-plane milestone-mapping duplicate remains queued downstream and is already satisfied by `EXTRACT-001A` evidence.

## Historical log

- Full queue-overlay churn, duplicated publication prompts, and audit replay notes now live in `AUDIT_LOG.md`.
- Re-entry `2026-03-14`: package-plane milestone-mapping slice was reaffirmed against `EXTRACT-001A` (`CP-01`..`CP-03`) with no new executable backlog delta.
