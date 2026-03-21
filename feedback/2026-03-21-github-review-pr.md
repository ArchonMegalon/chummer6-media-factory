# GitHub Codex Review
Status: READ (2026-03-21)

PR: https://github.com/ArchonMegalon/chummer6-media-factory/pull/2

Findings:
- [high] src/Chummer.Media.Contracts/Compatibility/RunServices/MediaFactoryContracts.cs [contracts] contracts-render-boundary-leak-compatibility-dtos
Compatibility DTOs include upstream campaign/session and narrative-context semantics inside the contracts package, e.g. `PacketAttachmentTargetKind.Campaign` and `RouteCinemaRequest(string CampaignId, ..., string SceneContext)`.; Concrete lines: `MediaFactoryContracts.cs` lines 158, 193, 200, 203 introduce `Campaign*` and scene-context fields in `Chummer.Media.Contracts` public records.
Expected fix: Keep `Chummer.Media.Contracts` render-only by removing/migrating campaign/session/narrative-context DTO members from the contracts package (or move these compatibility shapes out of public package surface).
- [high] scripts/ai/contract-boundary-tests.sh [tests] tests-boundary-scan-excludes-compatibility-folder
Boundary regex checks now exclude `Compatibility/**` via `--glob '!**/Compatibility/**'` (script lines 36 and 45), which suppresses detection of render-boundary drift in that folder.; `bash scripts/ai/contract-boundary-tests.sh` currently passes, while a direct scan without that exclusion finds forbidden fields: `CampaignId` at `MediaFactoryContracts.cs:193` and `:200`.
Expected fix: Restore boundary enforcement over compatibility surfaces (remove the exclusion or add an equivalent explicit compatibility check) so render-only violations fail CI.
- [high] .codex-design/review/REVIEW_CONTEXT.md [review] review-context-checklist-regression
Committed `HEAD` version is reduced to a short 6-line P1 list and drops required verification checklist sections (contract-boundary script, `verify.sh`, package-boundary and lifecycle checklist, summary format).; This weakens the repo’s stated missing-tests/review enforcement for local fallback reviews.
Expected fix: Restore explicit verification checklist and review-output contract content in `.codex-design/review/REVIEW_CONTEXT.md`.
