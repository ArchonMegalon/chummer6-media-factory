# Private Origin chapter illustrations

Status: locally tested private worker and retention, **not a deployed
automatic book worker or delivered Android feature**. Phygital+ has no verified
adapter here. OneMinAI is the existing image adapter, with a narrow single-dispatch
mode for private books. No provider call was made by the tests for this increment.

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
The artifact payload is `chummer.origin.chapter-scene/v1`: those four chapter
fields plus `prompt` and `altText`. The work item/artifact/deduplication identities
are identical; truth includes `origin-dossier:scene:<identity>`, evidence includes
`origin-text:<textDigest>`. This is not a mutable provider URL or character rule.

## Behavior

- One image per exact selected chapter version; a changed prompt cannot silently
  replace an existing request. Changed prose gets a separate identity.
- A committed SQLite dispatch fence precedes provider entry. Concurrent requests,
  cold reads, timeout recovery and process restart cannot repeat paid generation.
  `dispatching` after a crash or `uncertain` requires operator reconciliation.
- OneMinAI single-dispatch uses one reserved account, model and size, exactly one
  POST and no redirects. Guide-mode behavior remains separate. Phygital requests
  fail before generation when only the OneMinAI renderer is available.
- Downloads use a public-IP-pinned HTTPS connection, no redirects/proxies,
  an exact known host set and a 4 MiB stream limit. Still PNGs are fully decoded,
  with dimensions at most 4096 per side. Provider URLs stay private.
- A manifest and exact bytes commit atomically, in `review`. Only explicit review
  of the exact content hash may persist them. Rejection or expiry removes the
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

## Remaining delivery work

The corresponding Hub change implements signed-install scene routes, exact
quota admission/readback and account erasure. Both changes still need local
deployment plus Android consent/download/review/adoption into its existing
owner/chapter-bound scene store. No daemon has been enabled by this change.
Do not advertise automatic Phygital+/OneMinAI illustration delivery from passing
unit tests. Existing Android EPUB/manual image delivery is independent.

Focused checks:

```sh
python3 -m unittest discover -s tests -p 'test_origin_scene*.py'
python3 -m unittest discover -s tests -p test_render_guide_asset_download_guard.py
```
