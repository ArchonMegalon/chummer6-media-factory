# Media Capability Signoff

Purpose: close `MF-011` and `MF-012` with explicit capability evidence instead of leaving media completion trapped in extraction-era queue text.

## Adapter authority

`chummer6-media-factory` is the only repo that may own provider/private execution choice for:

- document render and packet-generation lanes
- preview image lanes
- portrait-generation lanes
- bounded video lanes
- route-visual and route-video artifact lanes
- archive and retention execution

Upstream repos may request media intent. They do not choose provider adapters, provider credentials, storage class, or retention policy.

## Stable capability families

The current owner-contract surface already defines stable capability families for:

- documents and packets via `PacketFactoryResult`, `PacketArtifactHandle`, `DocumentPreviewImage`, `DocumentPdf`, and `DocumentThumbnailImage`
- governed packet-planning seams via `PacketFactoryRequest`, `PacketAttachmentBatchRequest`, `GovernedPrepPacketPlannerService`, and `CreatorPublicationPlannerService`, so reusable prep, opposition, and creator-publication packets compile in media-factory without redefining campaign truth
- portraits via `PortraitImageVariant`
- bounded video via `NarrativeBriefVideo`, `CinematicVideo`, and `PersonaMessageVideo`
- route cinema via `RouteCinemaResult` and `RouteCinemaArtifactHandle`
- archive and retention via `AssetLifecyclePolicy`, `AssetCatalogItem`, `AssetLifecycleSweepResult`, and the runtime restore/sweep path

## Provenance and lifecycle rules

- every render job yields an owner-repo job row and asset result
- provider/backend choice remains media-factory-private
- preview execution is switchable via `CHUMMER_MEDIA_FACTORY_IMAGE_BACKEND`
- preview execution fails closed via `CHUMMER_MEDIA_FACTORY_ENABLE_IMAGE_EXECUTION=0`
- unsupported backend tokens fail fast instead of silently falling back
- restore, retention, replay-safe dedupe, and storage-class continuity remain executable through `Chummer.Media.Factory.Runtime.Verify`

## Release statement

Media completion is now good enough for program closure because documents, portraits, bounded video, route artifacts, and archive/retention all have explicit owner contracts, lifecycle governance, and executable verification. Future provider expansion is additive depth, not missing service ownership.
