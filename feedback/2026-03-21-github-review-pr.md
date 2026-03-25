# GitHub Codex Review
Status: READ (2026-03-21)

PR: https://github.com/ArchonMegalon/chummer6-media-factory/pull/2

Findings:
- [high] src/Chummer.Media.Contracts/Compatibility/RunServices/MediaFactoryContracts.cs [contracts] contracts-render-context-leak-compat
Public contracts include upstream context fields in render DTOs: `PacketFactoryRequest(... string? SceneId = null ...)` (line 152), `RouteCinemaRequest(string RouteContextId, ..., string SceneId)` (lines 193-196), and `RouteCinemaResult(... string RouteContextId, ..., string SceneId, ...)` (lines 200-203).; Repo boundary docs mark campaign/session context out of scope for `Chummer.Media.Contracts` and require render-only DTO surfaces.
Expected fix: Remove/migrate upstream context identifiers from the public contracts package (or move these compatibility shapes out of package-public render contracts) so DTOs remain render-only.
- [high] src/Chummer.Media.Contracts/Compatibility/RunServices/MediaFactoryContracts.cs [contracts] contracts-approval-state-semantic-drift
Compatibility lifecycle enum uses `Draft/Approved/Rejected` (lines 11-16) instead of pending semantics expected by repo review policy (`pending/approved/rejected`).; This creates parallel approval semantics in public contracts and risks lifecycle-state drift across contract families.
Expected fix: Align compatibility approval-state semantics with canonical pending/approved/rejected lifecycle contract meaning, or explicitly map/contain this shape outside canonical public contracts.
- [high] scripts/ai/contract-boundary-tests.sh [tests] tests-boundary-scan-missing-context-terms
Boundary regex checks currently match tokens like Campaign/Session/Narrative/etc. (lines 35-46) but do not include `Scene*` or `RouteContext*` terms.; The script passes while compatibility DTOs still expose `SceneId` and `RouteContextId` public members.
Expected fix: Extend boundary tests to fail on upstream context identifiers used in this repo policy (e.g., `Scene*`, `RouteContext*`) or add an equivalent explicit compatibility-contract guard.
