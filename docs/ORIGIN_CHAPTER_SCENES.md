# Private Origin chapter illustrations

Status: locally tested execution/retention building block, **not a deployed
automatic book worker or Android delivery endpoint**. Phygital+ has no verified
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
are identical; truth includes `origin-scene:<identity>`, evidence includes
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

## Local hosting contract

Use a dedicated **Media Factory-owned** Docker volume, mode 0700, with the SQLite
file mode 0600. Do not put it in Hub's app directory, a web root, Android, EA's
credential store or a distributable directory. Set
`CHUMMER_MEDIA_FACTORY_STATE_DIR` to that same private worker volume before
importing the renderer; the existing quota manager and isolated credential source
remain required. No provider secrets belong in these requests or manifests.

Use SQLite's online backup API for the database and retain the corresponding
private renderer receipts. Copying a live database file is not a verified backup.
An ambiguous dispatch is never reset automatically. The store has a bounded 128
records per owner / 2048 globally and fails before generation when full; capacity
maintenance must preserve no-replay history. Account erasure and service recovery
must be connected before exposing this as a live product endpoint.

## Remaining delivery work

The private Hub-to-worker admission/readback channel, user consent UI, account
erasure integration and Android download into its existing owner/chapter-bound
scene store are not wired yet. There is no daemon or new public listener in this
increment. Do not advertise automatic Phygital+/OneMinAI illustration delivery
from passing these unit tests. Existing Android EPUB/manual image delivery is
independent and remains usable.

Focused checks:

```sh
python3 -m unittest discover -s tests -p 'test_origin_scene*.py'
python3 -m unittest discover -s tests -p test_render_guide_asset_download_guard.py
```
