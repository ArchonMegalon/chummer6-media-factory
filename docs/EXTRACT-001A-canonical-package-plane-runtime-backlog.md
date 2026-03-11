# EXTRACT-001A Canonical Package Plane Runtime Backlog

## Objective

Convert repeated generic uncovered-scope prompts about `Chummer.Media.Contracts` into runnable, evidence-gated tasks that prove the canonical render-only package plane remains real and verifiable in this repo.

## Runnable backlog

1. CP-01 Publish package-plane acceptance evidence artifact
- Create `docs/EXTRACT-001A-canonical-package-plane-evidence.md` with pass/fail checks for:
  - package project presence (`src/Chummer.Media.Contracts/Chummer.Media.Contracts.csproj`)
  - canonical package metadata (`PackageId`, `AssemblyName`, `RootNamespace`)
  - render-only namespace/boundary guardrails
  - solution inclusion and build participation
- Evidence: dated artifact with explicit check results and source file links.

2. CP-02 Add verify-path packaging guardrail
- Extend `scripts/ai/verify.sh` to run `dotnet pack` for `Chummer.Media.Contracts` in Release mode (no restore/build duplication) and fail if no `.nupkg` is produced.
- Keep output minimal and deterministic.
- Evidence: verify log includes successful pack step and artifact path.

3. CP-03 Queue/worklist normalization for package-plane findings
- Replace generic queue prompts for candidate scope (`22418`, `22422`) with explicit `EXTRACT-001A/CP-*` entries.
- Update `WORKLIST.md` queue truth section so duplicate publications map to this runnable backlog instead of re-adding generic scope text.
- Evidence: `.codex-studio/published/QUEUE.generated.yaml` and `WORKLIST.md` both reference `EXTRACT-001A`.

## Completion signal

This slice is complete when:
- `EXTRACT-001A` runnable tasks are the active queue representation for package-plane uncovered-scope prompts,
- verify enforces successful package production for `Chummer.Media.Contracts`,
- and queue/worklist text no longer treats the package-plane prompt as an unscoped generic item.
