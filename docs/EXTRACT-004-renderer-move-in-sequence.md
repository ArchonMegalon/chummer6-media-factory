# EXTRACT-004 Renderer Move-In Sequence

## Purpose

Define the execution order for renderer move-in after shared asset-kernel completion:

1. deterministic documents
2. portraits
3. video

Each stage is gated so media-factory only absorbs renderer execution after required substrate seams are already stable.

## Preconditions

EXTRACT-003 kernel backlog (K1-K5) is complete and verified:
- manifest persistence wiring is landed
- binary storage adapter seam is landed
- preview linkage contracts are landed
- TTL/retention sweep contract is landed
- lineage traversal contract is landed

No renderer move-in starts unless all kernel seams above are available in the local contract and execution plan.

## Stage sequence and dependency gates

### Stage R1: Deterministic document rendering

Scope:
- template-bound HTML/PDF/image packet rendering only
- deterministic template + version + input execution path

Entry gates:
- kernel preconditions are complete
- deterministic render request fields are sufficient to execute without orchestration-side mutation
- idempotent job claim + dedupe key behavior is enforced for document jobs

Exit gates:
- same template version + same input model yields byte-stable document output
- document manifests persist content hash, binary locator, preview link, and lineage metadata
- structured failure envelope exists for template validation and renderer execution failure classes

### Stage R2: Portrait rendering

Scope:
- portrait execution and variant generation on the same shared job/asset substrate
- canonical portrait selection metadata as render output state (not approval policy)

Entry gates:
- all R1 exit gates are satisfied
- portrait plan contracts carry only render parameters and provenance, not narrative-authoring policy
- preview and thumbnail linkage for portrait derivatives is wired through kernel contracts

Exit gates:
- portrait renders persist immutable asset manifests with parent/variant lineage
- retries and supersession behavior use shared job/lifecycle invariants
- deterministic metadata projection exists for canonical-vs-superseded portrait asset identity

### Stage R3: Video rendering

Scope:
- bounded recap/route/NPC render execution only
- video preview-first flow on top of shared kernel + prior renderer patterns

Entry gates:
- all R2 exit gates are satisfied
- video plan contracts are render-only and policy-free (no delivery, no campaign/session decisions)
- cost/latency guardrails are represented as execution constraints, not orchestration policy

Exit gates:
- preview artifact is generated and linked before expensive full render promotion where workflow requires it
- full render outputs persist manifest, binary locator, content hash, and lineage to preview/source assets
- failure taxonomy differentiates provider refusal, timeout, and non-retryable validation faults

## Hold-the-line boundary checks

For every stage (R1-R3), block move-in if either occurs:
- DTOs or executor inputs introduce narrative drafting, canon generation, rules math, or session relay payloads
- contracts or abstractions leak provider SDK types, engine/runtime internals, or UI-kit dependencies

## Seam gate enforcement

Renderer move-in completion for all stages (R1-R3) is additionally gated by:

- `docs/EXTRACT-006-SEAM-04-renderer-move-in-gate.md`

No stage may be marked complete if seam evidence is failing, missing, stale, or regressed.

## Completion condition for EXTRACT-004

This slice is complete when this sequence and gate set is present in repo-local planning artifacts and can be used to order executable renderer extraction work without ambiguity.
