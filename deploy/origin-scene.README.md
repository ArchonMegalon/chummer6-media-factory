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
separately reviewed local configuration must provide the existing quota-manager
route and scoped OneMin credentials, network egress, and `--enable-dispatch`.
Do not remove consent/admission checks, invent a quota manager or silently use a
different provider. Phygital is not an implemented renderer here.

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
