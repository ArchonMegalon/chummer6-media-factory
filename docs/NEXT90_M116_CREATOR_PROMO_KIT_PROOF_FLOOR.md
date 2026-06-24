# NEXT90 M116 Creator Promo Kit Proof Floor

- Package: `next90-m116-media-factory-creator-promo-kits`
- Frontier: `4956678153`
- Milestone: `116`
- Status: `in_progress`
- Completion action: `implementation_only`
- Proof floor commit: `unlanded`

`scripts/ai/verify_m116_creator_promo_kits.sh` stays directly executable so workers can invoke the package verifier without wrapping it in an ad hoc shell fallback.

## Scope

- Owned surfaces: `creator_promo_kits`, `publication_preview_artifacts`
- Allowed paths: `src`, `tests`, `docs`, `scripts`
- Registry task: `116.4`

## Guard rails

- creator promo kit rendering stays render-verified by requiring an approved manifest id and manifest revision id plus sibling-only payloads before media jobs enqueue, and JSON payloads must match those scope fields exactly instead of passing on substring mentions alone
- parseable JSON creator promo payloads fail closed when required scope fields are missing or mismatched, so JSON strings, arrays, or wrong-sibling objects cannot bypass exact scope matching through text fallback
- non-JSON creator promo payloads require exact keyed values or delimiter-safe scope tokens, so near-match approved manifest ids and manifest revision ids cannot pass by raw substring collision
- creator promo kit rendering requires one promo video, one promo poster, and one preview-card sibling before the bundle can render
- creator promo video siblings require caption refs while every creator promo sibling requires at least one preview ref
- creator promo rendering rejects duplicate artifact refs inside one approved manifest render request
- bundle-scoped dedupe keys include approved manifest id, manifest revision id, rendering id, sibling role, category, output format, artifact ref, and caller dedupe key
- receipt hashes include caption and preview refs so creator promo receipts stay tied to emitted preview and caption siblings
- receipt hashes use length-prefixed caption and preview ref segments so delimiter-heavy creator promo refs cannot collapse distinct outputs onto one receipt id
- normalized sibling ordering keeps receipt ids, artifact refs, ready refs, and grouped role, caption, and preview receipt rows stable when callers reorder the same approved manifest siblings
- case-insensitive caption and preview dedupe selects one canonical ref spelling before receipt hashing and aggregate ref emission so mixed-case duplicate refs stay stable when callers reorder them
- source and requested timestamp metadata stay outside bundle-scoped dedupe and receipt identity so replayed approved manifest renders cannot fork stable job ids, receipt ids, ready refs, or grouped role receipts
- creator promo ready refs preserve per-artifact ref, receipt id, job id, job state, output format, caption refs, preview refs, asset id, asset url, cache ttl, approval state, retention state, and storage class
- role, caption, and preview receipt groups preserve aggregate job ids, grouped artifact refs, and grouped artifact rows so downstream shelves do not need to reconstruct creator promo evidence from raw artifact receipts
- rendered timestamps resolve from completed media jobs so later deduped retries cannot rewrite bundle render time with a newer request timestamp
- creator promo package authority requires exactly one canonical queue row per mirror and exactly one registry task block

## Proof sources

- `src/Chummer.Media.Factory.Runtime/Assets/CreatorPromoKitRenderingService.cs`
- `src/Chummer.Media.Contracts/Compatibility/RunServices/MediaFactoryContracts.cs`
- `tests/CreatorPromoKitSmoke/Program.cs`
- `tests/test_creator_promo_kit_rendering.py`
- `tests/test_m116_creator_promo_proof.py`
- `tests/test_m116_successor_package_authority.py`
- `scripts/ai/materialize_media_release_proof.py`
- `scripts/ai/verify_m116_creator_promo_kits.sh`
