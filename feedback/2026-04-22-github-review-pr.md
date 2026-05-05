# GitHub Codex Review

PR: local://media-factory

Findings:
- [high] src/Chummer.Media.Contracts/Compatibility/RunServices/MediaFactoryContracts.cs [contracts] contracts-quarantine-still-exports-upstream-dtos
`MediaFactoryContracts.cs` still publicly exports upstream-authoring DTOs such as `PacketFactoryRequest`, `PacketAttachment*`, and `RouteCinema*` with fields like `Subject`, `Attachments`, `WaypointScript`, `TravelSummary`, and `ReviewState` ([lines 149-223]).; The guardrail script now accepts those mixed DTOs as long as they carry an `EXTRACT-008A quarantine` obsolete marker instead of failing on their continued presence ([scripts/ai/contract-boundary-tests.sh:68-90]).; This still violates the repo review rule to keep DTOs render-verified rather than merely quarantining upstream semantics inside the public media contract package.
Expected fix: Remove these mixed compatibility DTOs from the public `Chummer.Media.Contracts` surface or move them behind the upstream owner seam, and make the boundary test fail on their presence rather than permitting quarantine markers.
- [high] Chummer.Media.Factory.Runtime.Verify/Program.cs [tests] runtime-verify-missing-reject-lifecycle-coverage
The verifier exercises approval, pin/persist, backup/restore, cache expiry, and job expiry, but it never drives an approval-gated asset through `Rejected` state ([Program.cs:7-108]).; `AGENTS.md` and `.codex-design/review/REVIEW_CONTEXT.md` require lifecycle coverage for approval/persist/reject, yet the executable regression path only proves the approve/persist branch.; Because `scripts/ai/verify.sh` relies on this runtime verifier, the reject path can regress without any executable signal.
Expected fix: Add a reject-path verification case that rejects an approval-gated asset and asserts rejected retention state, rejection timestamp behavior, no persisted/pinned carryover, and correct restore behavior after backup/restore.
- [high] .codex-design/repo/IMPLEMENTATION_SCOPE.md [review] milestone-m8-completion-truth-still-overstates-dto-closure
HEAD marks `M8 finished media plant` as `complete` based on `DS-01`..`DS-05` alone ([IMPLEMENTATION_SCOPE.md lines 120-127 in HEAD]).; The DTO execution evidence explicitly says the generic DTO prompt is not fully closed by `DS-01`..`DS-05`, inventories remaining compatibility residue, and maps follow-on work to `DS-06`..`DS-09` ([docs/EXTRACT-008-DS-execution-evidence.md:239-305]).; `WORKLIST.md` simultaneously keeps `EXTRACT-008A` queued because the compatibility DTO residue is still open ([WORKLIST.md:29-30]).
Expected fix: Update milestone coverage/completion truth so M8 and the DTO-boundary lane reflect the still-open `EXTRACT-008A` compatibility residue instead of claiming full closure from `DS-01`..`DS-05`.
