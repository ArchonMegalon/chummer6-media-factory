# EXTRACT-008 DTO Split Boundary Backlog

## Objective

Convert the remaining generic uncovered-scope prompt about media DTO split into runnable, evidence-gated work that keeps `Chummer.Media.Contracts` render-only and pushes narrative-authoring, delivery, and campaign/session context contracts upstream.

## Runnable backlog

1. DS-01 Contract inventory and ownership classification
- Enumerate every public DTO in `src/Chummer.Media.Contracts`.
- Classify each DTO field as one of: render execution input, render/job lifecycle state, asset storage/retention/lineage metadata, or forbidden upstream concern.
- Evidence: published inventory table with explicit ownership classification and no unclassified field.

2. DS-02 Forbidden concern guardrails
- Add/update static checks that fail verification if contract names/fields introduce upstream-only concerns (narrative authoring, campaign/session truth, delivery policy, canon/rules decisions, provider-routing policy).
- Evidence: guardrail checks wired into `scripts/ai/verify.sh` path or equivalent repo verification command.

3. DS-03 Split/removal pass for mixed DTO residue
- If any mixed DTO/field is found, split it into render-only media contract plus upstream contract TODO handoff note, without importing upstream packages into this repo.
- Keep media-side DTOs deterministic and provider-neutral.
- Evidence: diff showing only render/job/asset lifecycle ownership remains in media contracts.

4. DS-04 Boundary conformance tests
- Add contract tests that assert render-only namespace policy and forbid dependencies on engine/play/ui-kit/provider SDK/runtime delivery models.
- Include tests for approval/persist/reject lifecycle semantics without policy-rule leakage.
- Evidence: passing contract tests in local verify run.

5. DS-05 Queue/worklist and seam doc synchronization
- Map this backlog into queue/worklist artifacts so generic auditor prompts are replaced with executable steps.
- Cross-reference `docs/MF-005-service-seams-and-handoffs.md` and `docs/EXTRACT-006-run-services-seam-cutover-backlog.md` to keep ownership boundaries consistent.
- Evidence: queue/worklist entries reference `EXTRACT-008` directly and map any active generic DTO-split queue prompt to DS-01..DS-05 as the runnable implementation path.

## Completion signal

This slice is complete when:
- `EXTRACT-008` is present in queue/worklist as the runnable mapping for DTO split uncovered-scope findings,
- verification enforces render-only DTO boundaries,
- and no active generic DTO-split queue prompt is left unmapped to DS-01..DS-05 runnable units.

Execution evidence: `docs/EXTRACT-008-DS-execution-evidence.md`.
