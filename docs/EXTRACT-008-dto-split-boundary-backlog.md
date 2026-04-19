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

## Runnable append (2026-04-15)

6. DS-06 Compatibility-shim residue inventory
- Enumerate every public DTO in `src/Chummer.Media.Contracts/Compatibility/RunServices/MediaFactoryContracts.cs` and classify each field as render/job/asset lifecycle metadata or forbidden upstream concern.
- Treat packet authoring, route narration, review state, and delivery-facing attachment semantics as upstream concerns even when they sit behind compatibility naming.
- Evidence: appended residue table in `docs/EXTRACT-008-DS-execution-evidence.md` with every compatibility field classified.

7. DS-07 Compatibility contract split plan
- Publish the concrete split plan for compatibility DTOs that still mix render-only surfaces with upstream packet/route authoring meaning.
- For each mixed type, name the media-side contract that remains, the upstream-owned contract family that should absorb the removed meaning, and the temporary compatibility/deprecation posture required to land the split safely.
- Evidence: backlog/evidence docs identify exact mixed types and the target split or quarantine action for each.

8. DS-08 Guardrail expansion for compatibility residue
- Extend `scripts/ai/contract-boundary-tests.sh` or equivalent verification so compatibility DTOs cannot silently reintroduce packet authoring, route narration, review-state, or campaign-context identifiers under non-obvious names.
- Evidence: explicit guardrail coverage for the compatibility surface is described in execution evidence and exercised by repo verification.

9. DS-09 Queue/worklist remap for reopened DTO split residue
- Update queue/worklist truth so the active generic DTO-split prompt maps to the original `DS-01`..`DS-05` closure plus the new compatibility-residue follow-on lane, instead of claiming the slice is already fully closed.
- Evidence: `WORKLIST.md` references the follow-on work explicitly and no current queue note says the DTO split is fully satisfied while mixed compatibility DTOs remain.

## Completion signal

This slice is complete when:
- `EXTRACT-008` is present in queue/worklist as the runnable mapping for DTO split uncovered-scope findings,
- verification enforces render-only DTO boundaries across both canonical and compatibility surfaces,
- no active generic DTO-split queue prompt is left unmapped to `DS-01`..`DS-09` runnable units,
- and compatibility DTO residue is either split out of `Chummer.Media.Contracts` or explicitly quarantined behind a documented deprecation/removal plan.

Execution evidence: `docs/EXTRACT-008-DS-execution-evidence.md`.
