# AGENTS

## Mission
Own render-verified jobs, assets, storage, and lifecycle state for Chummer media outputs.

## Boundaries
- Validate all asset storage changes.
- Keep all media # DTOs render-verified.
- Do not introduce rules, session relay, lore, or provider-routing logic here.
- Treat upstream orchestration input, as opposed to narrative as upstream orchestration input, not media-factory rendering.

## Review guidelines
- Flag any media # DTO that mixes rendering with narrative-authoring or canon-generation as P1.
- Flag any dependency on engine implementation, play implementation, or UI-kit as P1.
- Flag asset lifecycle state machines without approval/persist/reject coverage as P1.

<!-- fleet-design-mirror:start -->
## Fleet Design Mirror
- Load `.codex-design/product/README.md`, `.codex-design/repo/IMPLEMENTATION_SCOPE.md`, and `.codex-design/review/REVIEW_CONTEXT.md` when present.
- Treat `.codex-design/` as the approved local mirror of the cross-repo Chummer design front door.
<!-- fleet-design-mirror:end -->
