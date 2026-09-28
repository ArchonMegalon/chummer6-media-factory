# Bounded real-provider continuity test — 28 September 2026

Result: **opening image succeeds; later-age reference validation fails**.
This is not a completed two-age visual proof or a Play release.

## Scope and runtime

The operator approved at most two new private synthetic OneMin images on
28 September, using existing credits. No purchases, public image publication,
FirstBook generation, production entitlement renewal or lifetime-cap change.
The isolated test used actual compiled Hub admission/composer/client components
and the deployed Media worker, without fabricating accepted user chapters or
FirstBook receipts. A separate durable synthetic allowance limited admission to
two and expires that day. Original production sponsorship remained untouched.

Runtime sources: Hub `29da3f48309a6ff2a0344f4ed2b6cae9063465e1`, Media
`7ec676f18127dc9f5a0c8437836f46dff92588aa`. Deployed Media image:
`sha256:e49d774d5f2f6f5203add45ffd295041750f1707c39f7a5447bf8d3d200ffc6f`.
Its lifetime limit stayed eight. No provider credentials or raw private responses
are included here.

## Observed result

- The child scene generated one real OneMin PNG, automatically persisted under
  `automatic-private-book/v1`. SHA-256:
  `2c9895348c4a6a5f028d8a664f8f734568dd026d7f28eb73a77733dc7756ffe2`;
  length 1,985,351 bytes, dimensions 1536 × 1024. The image was visually inspected:
  a young elf with dark skin and braided hair repairing a radio at a kitchen table.
  Fine identity details are not independently verified by this single image.
- A separate cold client read returned the identical hash, byte length,
  protagonist/reference binding and private insertion policy, without rendering.
- The teenage scene was admitted once, using the original retained PNG. It
  stopped at `media_factory:origin_reference_rejected` at 07:58:34 UTC. This
  boundary follows the reference asset upload and precedes the paid image-edit
  POST. There is no second generated image, edit result or visual comparison.
- The initial adapter retained only a generic rejection, not the upload response.
  Which metadata field disagreed is therefore unknown; no parser relaxation is
  justified by this result. The [official asset contract](https://docs.1min.ai/docs/api/asset-api)
  was checked, but documentation is not a substitute for the missing live response.
- Read-only recovery still reports `uncertain`. The scene fence and both used
  synthetic admission slots remain intact. No retry, new job, fallback or quota
  refund was performed. The original image remains retrievable.
- Database integrity was `ok`; counts were two persisted scenes (one historical
  and one new), one uncertain scene and one retained character reference. These
  are local attempt counts, not an observed provider credit balance or charge.

## Narrow follow-up

The adapter now records a closed, value-free rejection reason for response shape,
privacy, MIME, size, key or file binding, preserving all existing validation.
Twenty-four focused adapter tests pass in local, network-disabled Docker,
including twelve rejected-response cases proving one upload and no image edit,
retry or private response-value leakage. An initial bare-host invocation lacked
the `scripts` import path and failed; it is not counted as a successful run.
The diagnostic code is not yet deployed and cannot reconstruct the lost response.

### Approved upload-only diagnosis and correction

Telegram question 5438 requested exactly one upload of the same synthetic PNG,
with no generation, purchase, publication or fence/quota reset. The user approved
that scope with a new direct `ja` on 28 September. A separately fenced local
Docker process made that one asset POST. It retained the bounded response in
the private local packet, never in repository logs. Response SHA-256:
`7479077052de442f26574b2e08c1a6152be96ac9278026292fa7d71f1a1a7e94`.

The response contained matching MIME, integer byte size, PNG key and file path,
but **no `acl` field**; the safe rejection was `origin_reference_privacy_rejected`.
The public asset documentation still describes `acl: private`. The actual
provider response instead supplied a signed S3 URL. A bounded, read-only check
of this exact uploaded synthetic object observed unsigned GET **403**, signed
GET **200**, and the identical original PNG bytes/hash. No image edit was sent.

The adapter correction accepts missing ACL metadata only with that exact-origin,
bucket/path-bound access check and byte equality. It does not accept explicit
public/null ACLs, redirects, anonymous success, missing objects or arbitrary URLs.
OneMin credentials never go to S3; signed URLs and private values stay out of
receipts. A live read-only invocation of the corrected adapter accepted the
already uploaded object; it performed no further upload or generation.

All **28 focused renderer tests** pass in network-disabled local Docker, including
the new positive custody flow and negative URL, privacy, bounds and replay cases.
The first run exposed a missing image Content-Type in the new mocked success
fixture; the fixture was corrected and the same complete subset passed.
The correction is not yet deployed. The earlier teenage attempt remains fenced,
both synthetic admission slots remain used, and no quota or sponsorship changed.
Visual child/teen comparison, real automatic reader/EPUB flow and Android delivery
remain open. Private artifacts stay in the local test packet only.
