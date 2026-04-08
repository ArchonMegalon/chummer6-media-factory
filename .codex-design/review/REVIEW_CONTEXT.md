# Review context

## P1 boundary checks
- Flag any DTO that mixes rendering with narrative-authoring or canon-generation as P1.
- Flag campaign/session truth, approval policy, or delivery policy leaking into `chummer6-media-factory` as P1.
- Flag provider SDK types, storage implementation details, or UI contracts leaking into `Chummer.Media.Contracts` as P1.
- Flag any dependency on engine implementation, play implementation, or UI-kit as P1.
- Flag direct provider calls from client repos or `hub` once media-factory owns the render path as P1.
- Flag app-host filesystem blob storage, missing retention state, or non-idempotent heavy render execution as P1.
- Flag document rendering nondeterminism or missing portrait/video lineage preservation as P1.
- Flag asset lifecycle state machines without approval/persist/reject coverage as P1.

## Required verification flow
1. Run `bash scripts/ai/contract-boundary-tests.sh` when contract or DTO surfaces change.
2. Run `bash scripts/ai/verify.sh` before declaring completion.
3. Confirm package boundary discipline:
   - `Chummer.Media.Contracts` stays render-only and implementation-free.
   - no engine/play/ui-kit dependency leakage in source or package metadata.
4. Confirm lifecycle coverage discipline:
   - approval, persist, reject states are represented where lifecycle state machines are defined.
5. Confirm seam discipline:
   - run-services/hub/client repos call media-factory contracts, not provider SDKs directly.

## Review report format
1. Findings first, ordered by severity, with file/line references.
2. Open assumptions/questions second.
3. Short change summary last.
