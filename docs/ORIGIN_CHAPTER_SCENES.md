# Private Origin chapter illustrations

Status, 28 September 2026: the automatic private worker and compatible Hub are
locally deployed. Android integration is tested locally, **not delivered in a new
Play release**. One real synthetic opening image persisted and cold-read correctly;
the later-age attempt stopped at reference-upload validation, before an image-edit
POST. Visual continuity is therefore **not yet verified**. Phygital+ has no verified
adapter here. See [the bounded provider test](evidence/origin-continuity-canary-20260928.md).
The approved upload-only diagnosis found missing ACL metadata. The adapter now
supports the observed signed-S3 response only after an anonymous access denial
and exact-byte signed readback. This correction is locally tested; it does not
replay the failed image or establish multi-age visual continuity.

`scripts/origin_scene_store.py` consumes the existing
`chummer6-hub.horizon_governed_render_request.v1` contract for
`origin-dossier / origin-dossier-media / ea_governed_render`.
Hub composes this from an exact reader-accepted chapter and excerpt. Hub remains
responsible for current owner/install authorization, explicit external-processing
consent and atomic quota admission. The contract is an internal message, not a
bearer capability. Never pass an arbitrary phone JSON object directly to this
module, and never infer consent from the presence of an owner hash.

The opaque owner digest is SHA-256 of the exact authenticated Hub subject UTF-8.
Scene identity is SHA-256 of the NUL-delimited owner digest, workspace ID, chapter
ID, canonical chapter digest and selected prose digest. NUL is forbidden in IDs.
The legacy artifact payload is `chummer.origin.chapter-scene/v1`: those four chapter
fields plus `prompt` and `altText`. New continuity-bound requests use
`chummer.origin.chapter-scene/v2`, adding `protagonistId` and `referenceSceneId`.
Automatic private-book requests use v3 with
`insertionPolicy: automatic-private-book/v1`, covered by explicit illustrated-book
consent. Legacy consent and existing manual images are not silently upgraded.
The work item/artifact/deduplication identities
are identical; truth includes `origin-dossier:scene:<identity>`, evidence includes
`origin-text:<textDigest>`. This is not a mutable provider URL or character rule.

## One growing protagonist

The protagonist ID is SHA-256 of the NUL-delimited owner digest, workspace ID
and `origin-protagonist/v1`. Hub resolves the first reader-accepted chapter by
following its exact owner-bound predecessor chain; its scene identity becomes
the reference for every subsequent chapter. The prompt includes the initial
character brief and the current accepted life stage. Identity remains stable;
age, proportions, clothes and setting follow the chapter, without importing
future scars, implants or choices into childhood.

Media binds this first scene durably before dispatch. Another chapter, device or
locale cannot silently create a different first reference for that book, even
after an uncertain or rejected opening render. Later renders require the exact
persisted reference's PNG, hash, owner and workspace. Missing, corrupt, foreign,
expired or legacy v1 references fail before another provider call. Historical
v1 images remain readable; they are not retrospectively declared continuous.
Once a book has a v2 reference, new v1 requests for that book are refused before
dispatch. Reopening an existing paid v1 request still returns its original state.

OneMinAI receives those actual PNG bytes through its authenticated private
[asset endpoint](https://docs.1min.ai/docs/api/asset-api), followed by one
`gpt-image-1-mini` `IMAGE_EDITOR` request using the returned asset key. This is
not just a repeated text prompt. Upload/response bounds and private custody are
checked. The documented explicit `acl: private` response remains supported.
If ACL metadata is absent, only the observed OneMin bucket/path on the exact S3
origin is admitted: its unsigned GET must return 403 and its signed GET must
return the identical uploaded bytes within the input-size bound. Both reads
reject redirects and carry no OneMin API key. Explicit public/null ACLs, wrong
paths, anonymous success, missing objects, changed bytes and transport failures
stop before the edit. Signed links remain private. These are observed access
checks, not a guarantee about future provider retention or configuration.
Ambiguous upload/edit outcomes never fall back to text generation.
The client manifest records `protagonistId`, `referenceSceneId` and
`referenceImageHash`; provider URLs/asset keys are not public manifest fields.

These bindings prove which reference was used, **not visual similarity**. A real
multi-life-stage provider canary and visual review remain necessary before
claiming that the resulting art preserves the face reliably. The first real pair
test did not complete. No fallback generated an unrelated character. The v3
policy automatically persists validated private images without pretending a
per-image human review; the legacy manual-review protocol remains available only
for existing legacy requests.

## Behavior

- One image per exact selected chapter version; a changed prompt cannot silently
  replace an existing request. Changed prose gets a separate identity.
- A committed SQLite dispatch fence precedes provider entry. Concurrent requests,
  cold reads, timeout recovery and process restart cannot repeat paid generation.
  `dispatching` after a crash or `uncertain` requires operator reconciliation.
- OneMinAI single-dispatch uses one reserved account, model and size, exactly one
  image-feature POST and no redirects. Continuations first upload the bounded
  private reference once. Guide-mode behavior remains separate. Phygital requests
  fail before generation when only the OneMinAI renderer is available.
- Downloads use a public-IP-pinned HTTPS connection, no redirects/proxies,
  an exact known host set and a 4 MiB stream limit. Still PNGs are fully decoded,
  with dimensions at most 4096 per side. Provider URLs stay private.
- A manifest and exact bytes commit atomically. v3 automatic private-book images
  commit as `persisted` after owner revalidation; legacy/manual images commit in
  `review` and need explicit review of their exact hash. Rejection or expiry removes the
  deliverable blob while keeping a no-replay tombstone. Persisted images are not
  removed by the seven-day pending-image sweep.
- Reads verify content length, SHA-256, owner and chapter identity. Reopening or
  exporting never renders. Readback is reauthorized after provider completion.
- A manifest records actual backend, exact private provider-receipt digest,
  admission digest and PNG identity. It does not claim publication or rule truth.
- Account erasure removes image/manifest bytes and retains an owner tombstone.
  A render that completes after erasure cannot resurrect its image. Private
  provider receipts retain integrity/accounting hashes, not chapter prose,
  provider URLs or raw response previews. Provider-side retention is separate.

## Local hosting contract

Use a dedicated **Media Factory-owned** Docker volume, mode 0700, with the SQLite
file mode 0600. Do not put it in Hub's app directory, a web root, Android, EA's
credential store or a distributable directory. Set
`CHUMMER_MEDIA_FACTORY_STATE_DIR` to that same private worker volume before
importing the renderer; the existing quota manager and isolated credential source
remain required. No provider secrets belong in these requests or manifests.

`scripts/origin_scene_worker.py` serves HTTP only over a new Unix-domain socket in
a private 0700 directory, with socket mode 0600. It refuses to unlink a preexisting
socket. Hub and worker need the same narrowly scoped socket directory mounted in
their local Docker containers, not a shared workspace or Docker daemon socket.
Use `--socket`, `--token-file`, `--database`, `--private-render-directory`. No token
value belongs in arguments, images, source or logs. The independent token file
is owner-only, 32–256 printable ASCII bytes; each call rereads it for rotation.

Authenticated internal POST operations are `/v1/health`, `render`, `read`,
`decide`, `erase-owner`. Bodies have strict framing and a 64 KiB limit. There is
one render slot and eight bounded HTTP workers. Read returns exact PNG bytes as
bounded base64, not a provider URL. This token is Hub orchestration authority,
not a phone credential or user-consent substitute.

`OriginSceneStore.backup()` creates a new owner-only snapshot using SQLite's
online backup, checks integrity and fsyncs the file/directory. Its image bytes,
manifests, erasure and dispatch fences survive a cold open. The snapshot is
**recovery-only**: health reports `dispatchEnabled=false` and new paid generation
is refused. No ambiguous dispatch is reset. Older snapshots cannot know about
later paid jobs or account deletion; reconcile current Hub authorization,
deletion history and the independent provider journal before exposing recovered
data or deliberately enabling dispatch. This is not automatic off-host backup or
proof of complete host-loss recovery. Private accounting receipts need their
separate retention/backup policy; do not copy a live bare database file.

The store has a bounded 128 records per owner / 2048 globally and fails before
generation when full. Capacity maintenance must preserve no-replay history.

The dispatch overlay also requires an explicit absolute lifetime allowance through
`CHUMMER_ORIGIN_SCENE_DISPATCH_LIMIT` (1–2048). This is local admission capacity,
not the provider's credit balance: health deliberately reports
`providerCreditBalance: null`. Exhausting a small test allowance disables new
dispatch even when existing provider credits remain. Under an explicit standing
existing-credit approval, deployment may raise this allowance up to the existing
2048-record safety ceiling. Keep the same database, provider journal, erasure
fences and protagonist references; never reset the counter or replay uncertain
jobs. Persist the selected deployment setting and its approval reference. This
does not purchase credits, authorize automatic top-ups or alter provider quotas.

## Remaining delivery work

The corresponding Hub change implements signed-install scene routes, exact
quota admission/readback and account erasure. Compatible Hub/Media runtimes are
deployed with original data and rollback images retained. Android's automatic
reader/EPUB integration has focused managed checks; the current Debug APK passed
the missing-full-chapter progress and process-restart route. It is not a Play
release or a real two-age illustrated-book delivery. Deploy the locally tested
reference-upload compatibility correction, then verify the actual
older-age image and live reader/EPUB route. Existing uncertain jobs stay fenced.

Focused checks:

```sh
PYTHONPATH=scripts python3 -m unittest discover -s tests -p 'test_origin_scene*.py'
python3 -m unittest discover -s tests -p test_render_guide_asset_download_guard.py
```
