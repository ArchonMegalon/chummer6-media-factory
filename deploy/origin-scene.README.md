# Local private Origin scene worker

Build locally from this repository:

```sh
docker build --pull=false -f deploy/origin-scene.Dockerfile -t chummer-origin-scene:local .
```

Use the resulting immutable image ID as `CHUMMER_ORIGIN_SCENE_IMAGE`.
Provision a dedicated `CHUMMER_ORIGIN_SCENE_ROOT` owned by UID/GID 1000,
mode 0700, with private `socket`, `data`, `render` and `auth` directories.
`auth/worker.token` must be a separate random 32–256 byte printable ASCII secret,
without a newline, mode 0600. Never put it in the image, repository or served files.
Compose refuses to create missing host paths.

The default deployment has **no network and no provider dispatch**. Health proves
the local store/erasure transport, not provider availability. It can read/review
already retained assets and process account erasure. To admit new images, a
separately reviewed local configuration supplies one scoped OneMin key file,
network egress, a lifetime scene-attempt limit and `--enable-dispatch`.
Use `origin-scene.dispatch.compose.yml` in addition to the default Compose file.
Set `CHUMMER_ORIGIN_SCENE_DISPATCH_LIMIT` to the explicitly selected **absolute**
maximum attempts in this database (start with 1 for the synthetic canary).
Provision `auth/onemin.key` mode 0600, owned by the worker UID, with only the
selected existing key. Never mount EA's environment/key pool or an operator token.

Origin uses Hub's existing durable user allowance plus Media's existing SQLite
pre-dispatch fence and a local lifetime batch cap. It no longer depends on the
general guide adapter's EA HTTP reservation service. This is an explicit local
execution-model correction, not an EA reservation receipt or a provider-balance
claim. The guide path and its checks remain unchanged. No separate quota service
is created. The cap includes uncertain, rejected, expired and erased attempts and
does not refill on restart. Raising it requires an intentional deployment change;
never delete/replace the database to reset it. Restored snapshots remain disabled.

The recipe is one `gpt-image-1-mini`, low-quality, 1536x1024 image per admitted
scene. No model/key/size retries, redirects, proxies, top-ups or purchases.
An explicitly consented v3 `automatic-private-book/v1` request is retained for
automatic private reader/EPUB insertion after PNG validation and owner recheck.
It does not claim human image review. Legacy manual requests still require their
existing review decision; paid orders and old consent are never relabelled.

The opening image is retained as the book's original protagonist reference.
Later scenes upload that exact private PNG to the reference-capable editor and
keep its identity/digest, while the approved chapter determines the character's
age, clothing and setting. A missing or uncertain original stops the sequence;
it must not silently generate an unrelated replacement person. This reference
binding is tested, but visual recognizability across real generated age stages
still needs an actual provider-pair check.
Do not remove consent/admission checks or silently use a different provider.
Phygital is not an implemented renderer here. Private execution without the exact
committed Origin context still refuses per-call in-memory quota fallback.
An image response also does not prove its billed credit cost: observed usage stays
unknown until provider reconciliation, rather than being filled with an estimate.
Provider response recovery retains only record identity/status/shape, not private
prose, account details or signed download URLs. Relative output paths must match
their exact signed asset URL; only the documented storage origins are accepted.
A processing or failed response is not success and never triggers another POST.

Hub uses its `docker-compose.origin-scenes-local.yml` overlay. Mount only the
socket directory and dedicated token read-only into Hub; never mount the image
database, provider receipts or provider credentials there. No TCP listener or
Cloudflare route is required for the worker.

SIGTERM stops accepting requests and drains admitted operations before removing
the owned socket. An exclusive private lock prevents simultaneous workers.
After a crash only a refused same-inode socket may be reclaimed; live sockets,
regular files, symlinks and uncertain ownership fail closed. SQLite pre-dispatch
fences survive both normal restart and process death; an uncertain image request
is read/reconciled, never automatically regenerated.

Retain the previous image and private data during updates. Use the store's SQLite
backup operation for snapshots, not a bare live database copy. Snapshot recovery
is read/erasure-only until deletion and spend reconciliation is completed.
This local service alone does not prove off-host disaster recovery, a real image
generation, an Android device journey or a Play publication.
