# Media Capability Signoff

Purpose: close `MF-011` and `MF-012` with explicit capability evidence instead of leaving media completion trapped in extraction-era queue text.

## Adapter authority

`chummer6-media-factory` is the only repo that may own provider/private execution choice for:

- document render and packet-generation lanes
- preview image lanes
- portrait-generation lanes
- bounded video lanes
- route-visual and route-video artifact lanes
- campaign briefing bundle lanes for locale-matched cold-open, mission briefing, caption, and preview siblings
- starter onboarding artifact bundle lanes for localized starter primers, first-session briefings, and support-safe onboarding companions
- runsite orientation bundle lanes for host clips, route previews, and optional audio or tour siblings
- structured media recipe lanes for video, audio, preview-card, and packet-bundle siblings
- build explain companion lanes for approved explain packets rendered into video, audio, preview-card, and packet companions
- creator promo kit lanes for approved creator manifests rendered into promo video, poster, and preview-card siblings
- explain presenter sibling lanes for approved explanation packets rendered into optional audio or presenter-video siblings with first-party text fallback
- install-aware concierge bundle lanes for release explainer, support closure, and public concierge siblings rendered into video, audio, and preview-card companions with bounded sibling notes
- GM prep packet lanes for governed opposition, scene, and prep-library entries rendered into packet, preview, and optional briefing siblings
- archive and retention execution

Upstream repos may request media intent. They do not choose provider adapters, provider credentials, storage class, or retention policy.

## Stable capability families

The current owner-contract surface already defines stable capability families for:

- documents and packets via `PacketFactoryResult`, `PacketArtifactHandle`, `DocumentPreviewImage`, `DocumentPdf`, and `DocumentThumbnailImage`
- portraits via `PortraitImageVariant`
- bounded video via `NarrativeBriefVideo`, `CinematicVideo`, and `PersonaMessageVideo`
- route cinema via `RouteCinemaResult` and `RouteCinemaArtifactHandle`
- campaign briefing bundles via `CampaignBriefingBundleRequest`, `CampaignBriefingBundleReceipt`, `CampaignBriefingArtifactReceipt`, `CampaignBriefingLocaleReceipt`, `CampaignBriefingLocaleBundleReceipt`, `CampaignBriefingFallbackSiblingReceipt`, `ColdOpenCaptionReceiptId`, `MissionBriefingCaptionReceiptId`, `ColdOpenPreviewReceiptId`, `MissionBriefingPreviewReceiptId`, `CampaignColdOpen`, `CampaignMissionBriefing`, `CampaignCaption`, and `CampaignPreview`
- starter onboarding artifacts via `StarterArtifactBundleRenderRequest`, `StarterArtifactBundleReceipt`, `StarterArtifactReceipt`, `StarterArtifactReadyRef`, `StarterArtifactLocaleReceiptGroup`, `StarterArtifactBundleLocaleReceiptGroup`, `StarterArtifactArtifactRefReceipt`, `StarterArtifactCaptionRefReceipt`, `StarterArtifactPreviewRefReceipt`, `StarterArtifactSupportNoteReceipt`, `StarterPrimerVideo`, `FirstSessionBriefingAudio`, and `SupportSafeOnboardingPreviewCard`
- runsite orientation via `RunsiteOrientationBundleRequest`, `RunsiteOrientationBundleReceipt`, `RunsiteOrientationArtifactReceipt`, `RunsiteHostClip`, `RunsiteRoutePreview`, `RunsiteAudioCompanion`, and `RunsiteTourSibling`
- structured media recipes via `StructuredMediaRecipeRequest`, `StructuredMediaRecipeBundleReceipt`, `StructuredMediaRecipeArtifactReceipt`, `StructuredMediaRecipePublicationReadyRef`, `StructuredMediaRecipeRoleReceiptGroup`, `StructuredMediaRecipePublicationRefReceipt`, `StructuredMediaRecipeCaptionRefReceipt`, `StructuredMediaRecipePreviewRefReceipt`, `StructuredRecipeVideo`, `StructuredRecipeAudio`, `StructuredRecipePreviewCard`, and `StructuredRecipePacketBundle`
- build explain companions via `BuildExplainCompanionRenderRequest`, `BuildExplainCompanionRenderReceipt`, `BuildExplainCompanionArtifactReceipt`, `BuildExplainCompanionReadyRef`, `BuildExplainCompanionRoleReceiptGroup`, `BuildExplainCompanionRefReceipt`, `BuildExplainCaptionRefReceipt`, `BuildExplainPreviewRefReceipt`, `BuildExplainCompanionVideo`, `BuildExplainCompanionAudio`, `BuildExplainCompanionPreviewCard`, and `BuildExplainCompanionPacketCompanion`
- creator promo kits via `CreatorPromoKitRenderRequest`, `CreatorPromoKitRenderReceipt`, `CreatorPromoKitArtifactReceipt`, `CreatorPromoKitReadyRef`, `CreatorPromoKitRoleReceiptGroup`, `CreatorPromoKitArtifactRefReceipt`, `CreatorPromoCaptionRefReceipt`, `CreatorPromoPreviewRefReceipt`, `CreatorPromoVideo`, `CreatorPromoPoster`, and `CreatorPromoPreviewCard`
- explain presenter siblings via `ExplainPresenterSiblingRenderRequest`, `ExplainPresenterSiblingRenderReceipt`, `ExplainPresenterSiblingArtifactReceipt`, `ExplainPresenterTextFallbackReceipt`, `ExplainPresenterSiblingReadyRef`, `ExplainPresenterSiblingRoleReceiptGroup`, `ExplainPresenterCompanionRefReceipt`, `ExplainPresenterCaptionRefReceipt`, `ExplainPresenterPreviewRefReceipt`, `ExplainPresenterSiblingAudio`, and `ExplainPresenterSiblingPresenterVideo`
- install-aware concierge bundles via `InstallAwareConciergeRenderRequest`, `InstallAwareConciergeBundleReceipt`, `InstallAwareConciergeArtifactReceipt`, `InstallAwareConciergeCompanionReadyRef`, `InstallAwareConciergeRoleReceiptGroup`, `InstallAwareConciergeCompanionRefReceipt`, `InstallAwareConciergeCaptionRefReceipt`, `InstallAwareConciergePreviewRefReceipt`, `InstallAwareConciergeSiblingNoteReceipt`, `InstallAwareReleaseExplainerVideo`, `InstallAwareSupportClosureAudio`, and `InstallAwarePublicConciergePreviewCard`
- replay/exchange preview bundles via `ReplayExchangePreviewRenderRequest`, `ReplayExchangePreviewRenderReceipt`, `ReplayExchangePreviewArtifactReceipt`, `ReplayExchangePreviewBundleReceipt`, `ReplayExchangePreviewKindReceiptGroup`, `ReplayExchangePreviewReadyRef`, `ReplayExchangePreviewArtifactRefReceipt`, `ReplayExchangePreviewCaptionRefReceipt`, `ReplayExchangePreviewPreviewRefReceipt`, `RecapPreviewCard`, `RecapInspectableSibling`, `ReplayPreviewCard`, `ReplayInspectableSibling`, `ExchangePreviewCard`, and `ExchangeInspectableSibling`
- GM prep packets via `GmPrepPacketRenderRequest`, `GmPrepPacketBundleReceipt`, `GmPrepPacketEntryReceipt`, `GmPrepPacketSubjectReceiptGroup`, `GmPrepPacketArtifactReceipt`, `GmPrepOppositionPacket`, `GmPrepOppositionPreview`, `GmPrepScenePacket`, `GmPrepScenePreview`, `GmPrepLibraryPacket`, and `GmPrepLibraryBriefing`
- archive and retention via `AssetLifecyclePolicy`, `AssetCatalogItem`, `AssetLifecycleSweepResult`, and the runtime restore/sweep path

## Provenance and lifecycle rules

- every render job yields an owner-repo job row and asset result
- provider/backend choice remains media-factory-private
- preview execution is switchable via `CHUMMER_MEDIA_FACTORY_IMAGE_BACKEND`
- preview execution fails closed via `CHUMMER_MEDIA_FACTORY_ENABLE_IMAGE_EXECUTION=0`
- unsupported backend tokens fail fast instead of silently falling back
- restore, retention, replay-safe dedupe, and storage-class continuity remain executable through `Chummer.Media.Factory.Runtime.Verify`
- campaign briefing bundles require requested-locale cold-open and mission briefing entries, keep the requested locale as the primary sibling, bound fallback locales to explicit locale-matched sibling bundles, and preserve asset urls plus lifecycle truth on emitted artifact receipts
- campaign briefing locale-bundle and fallback-sibling receipt rows keep stable receipt ids and grouped sibling evidence so downstream shelves can launch cold-open and briefing artifacts without reconstructing locale posture from raw job rows
- campaign briefing locale-bundle and fallback-sibling receipt rows also preserve slot-aware caption and preview sibling ids so downstream shelves can launch the correct cold-open and mission-briefing companions without scanning nested artifact rows
- campaign briefing bundle receipts also carry first-class `RequestedLocaleBundleReceiptId`, `FallbackLocales`, and `FallbackLocaleBundleReceiptIds` fields so downstream shelves can launch the primary bundle and enumerate bounded fallback siblings without regrouping raw locale rows
- campaign briefing job dedupe includes requested locale, slot, entry locale, fallback posture, category, output format, and caller dedupe, while receipt hashes use length-prefixed locale, artifact-kind, and output-format segments so delimiter-heavy locale variants cannot collapse onto one render job or receipt id
- campaign briefing normalized locale-bundle ordering keeps locale receipts, locale bundle receipts, fallback sibling receipts, and summary job ids stable when callers reorder the same bundle entries
- starter onboarding artifact receipts stay render-verified by requiring an `ApprovedStarterSourcePackId`, `SourcePackRevisionId`, `StarterLaneId`, and per-artifact locale plus sibling-only payloads before any media job can enqueue
- parseable starter onboarding JSON payloads fail closed when required scope fields are missing or the root payload is not an object, so JSON strings, arrays, or note-only objects cannot bypass exact scope matching through text fallback
- starter onboarding bundles require requested-locale and fallback locale triads for starter primer, first-session briefing, and support-safe onboarding siblings, while fallback locales stay bounded to at most two locales
- starter onboarding video and audio siblings require caption refs, video and preview-card siblings require preview refs, and support-safe onboarding siblings require bounded support-note refs
- starter onboarding rendering rejects duplicate artifact refs inside one starter-lane request and uses length-prefixed dedupe and receipt hashing across locale, caption, preview, and support-note inputs so delimiter-heavy variants cannot collapse distinct outputs onto one job or receipt id
- starter onboarding locale and bundle-locale receipt groups preserve aggregate job ids, artifact refs, caption refs, preview refs, support notes, and grouped artifact rows so downstream starter surfaces do not need to reconstruct locale evidence from raw artifact receipts
- runsite orientation receipts carry `PreviewTruthPosture=pre-session-orientation-only-not-tactical-truth`, host/audio/tour role receipt ids, `RoutePreviewReceiptIds`, `RoutePreviewArtifactReceipts`, route segment ids, and media-factory job ids so upstream surfaces can launch orientation artifacts without treating them as tactical route truth
- structured media recipe receipts carry structured `PublicationReadyRefs`, publication-ready asset URLs, caption refs, preview refs, first-class `RoleReceiptGroups`, `PublicationRefReceipts`, `CaptionRefReceipts`, `PreviewRefReceipts`, grouped `ArtifactReceipts`, aggregate `JobIds`, grouped caption/preview `PublicationRefs`, role-specific receipt ids, per-artifact caption/preview refs on grouped receipt artifact rows, and media-factory job ids for video, audio, preview-card, and packet-bundle siblings
- structured media recipe execution rejects duplicate publication refs inside one bundle so publication-ready receipt rows stay unambiguous for downstream shelves
- structured media recipe role, caption, and preview receipt groups sort emitted ids and refs explicitly so replayed bundles keep stable publication evidence ordering
- structured recipe job dedupe includes a length-prefixed hash over artifact category, output format, and publication ref, while receipt hashes include caption and preview refs, so colliding caller dedupe keys or delimiter-heavy refs cannot collapse different publication-ready outputs onto one render job
- build explain companion receipts stay render-verified by requiring an `ApprovedExplainPacketId` plus sibling-only payloads, never engine mutations or media-authored truth
- build explain companion rendering fails closed on null artifact lists and null sibling entries before normalization, so approved explain packet validation does not degrade into incidental runtime null failures
- build explain companion rendering rejects sibling payloads that drift away from the approved explain packet id or explain packet revision id before any media job can enqueue, and JSON payloads must match those scope fields exactly instead of passing on substring mentions alone
- JSON and keyed text build explain scope values trim surrounding whitespace before exact scope matching so padded approved packet payloads stay valid without reopening substring spoof paths
- build explain caption and preview refs trim surrounding whitespace before grouped receipt rows and receipt hashes emit, so padded sibling refs stay replay-safe instead of forking stable companion evidence
- build explain caption and preview refs dedupe case-insensitively and keep one canonical ref spelling before grouped receipt rows emit, so mixed-case duplicates stay replay-safe when callers reorder the same approved explain packet siblings
- build explain companion receipts carry first-class `CompanionReadyRefs`, `CompanionRefReceipts`, `CaptionRefReceipts`, `PreviewRefReceipts`, grouped `ArtifactReceipts`, aggregate `JobIds`, and media-factory job ids for video, audio, preview-card, and packet-companion siblings
- build explain companion rendering trims surrounding whitespace and rejects case-insensitive duplicate companion refs inside one approved explain packet, and uses length-prefixed dedupe and receipt hashing so delimiter-heavy refs cannot collapse distinct outputs onto one job or receipt id
- build explain companion dedupe and receipt identity stay scoped to approved explain packet sibling truth rather than request metadata, so source or requested timestamp drift cannot fork stable job ids, receipt ids, ready refs, or grouped role receipts
- creator promo receipts stay render-verified by requiring an `ApprovedManifestId` and `ManifestRevisionId` plus sibling-only payloads before any media job can enqueue, never publication policy, trust ranking, or moderation semantics
- creator promo payloads fail closed unless JSON scope fields match exactly or non-JSON payloads carry exact keyed values or delimiter-safe scope tokens, so near-match manifest or revision ids cannot pass by substring collision
- creator promo kit rendering requires promo video, promo poster, and preview-card siblings, while promo videos require caption refs and every creator promo sibling requires preview refs
- creator promo caption and preview refs dedupe case-insensitively into one canonical spelling before grouped receipt rows and receipt hashes emit, so mixed-case retries stay replay-safe when callers reorder the same approved manifest siblings
- creator promo rendering rejects duplicate artifact refs inside one approved manifest request and uses length-prefixed dedupe plus receipt hashing across sibling identity, caption refs, and preview refs so delimiter-heavy variants cannot collapse distinct outputs onto one job or receipt id
- creator promo ready refs, artifact-ref receipts, and grouped role, caption, and preview receipt rows preserve aggregate job ids, grouped artifact refs, direct asset urls, and lifecycle truth so creator publication shelves do not need to reconstruct promo evidence from raw artifact receipts
- creator promo dedupe and receipt identity stay scoped to approved manifest sibling truth rather than request metadata, so source or requested timestamp drift cannot fork stable job ids, receipt ids, ready refs, or grouped role receipts
- explain presenter sibling receipts stay render-verified by requiring an `ApprovedExplanationPacketId`, `ExplanationPacketRevisionId`, and `GroundingScopeRef` plus sibling-only payloads before any media job can enqueue
- replay/exchange preview receipts stay render-verified by keeping recap, replay, and exchange bundles first-class, preserving lineage plus compatibility/provenance/bounded-loss refs on grouped receipts, and emitting first-class preview-card, inspectable-sibling, ready-ref, caption-ref, and preview-ref receipt rows without becoming portability or campaign authority
- explain presenter sibling rendering preserves first-party text fallback through `FirstPartyTextFallback` and `ExplainPresenterTextFallbackReceipt` so optional media surfaces never become the only way to inspect the answer
- parseable explain presenter JSON payloads fail closed when required packet-identity or grounding-scope fields are missing or the root payload is not an object, so JSON strings, arrays, or note-only objects cannot bypass exact scope matching through text fallback
- JSON and keyed text explain presenter scope values trim surrounding whitespace before exact scope matching so padded approved explanation packet payloads stay valid without reopening substring spoof paths
- explain presenter audio and presenter siblings require caption refs while presenter-video siblings require preview refs, and grouped `CaptionRefReceipts` plus `PreviewRefReceipts` stay first-class for downstream shelves
- explain presenter caption and preview refs dedupe case-insensitively and keep one canonical ref spelling before grouped receipt rows emit, so mixed-case duplicates stay replay-safe when callers reorder the same approved explanation packet siblings
- explain presenter sibling rendering rejects duplicate companion refs inside one approved explanation packet and uses length-prefixed dedupe and receipt hashing across caption, preview, and text-fallback inputs so delimiter-heavy variants cannot collapse distinct outputs onto one job or receipt id
- explain presenter sibling dedupe and receipt identity stay scoped to approved explanation packet sibling truth rather than request metadata, so source or requested timestamp drift cannot fork stable job ids, receipt ids, ready refs, grouped role receipts, or text fallback receipts
- explain presenter rendered timestamps resolve from completed media jobs instead of newer request timestamps, so deduped retries cannot rewrite bundle render time while receipt identity stays stable
- install-aware concierge receipts stay render-verified by requiring an `InstallAwarePacketId`, `InstalledBuildReceiptId`, and `ArtifactIdentityId` plus sibling-only payloads before any media job can enqueue
- parseable install-aware JSON payloads fail closed when required scope fields are missing or the root payload is not an object, so JSON strings, arrays, or note-only objects cannot bypass exact scope matching through text fallback
- install-aware concierge rendering requires release explainer, support closure, and public concierge bundles to each emit video, audio, and preview-card siblings, while every artifact carries bounded `SiblingNoteRefs` and first-class `CaptionRefReceipts`, `PreviewRefReceipts`, and `SiblingNoteReceipts`
- install-aware concierge rendering rejects duplicate companion refs inside one install-aware packet and uses length-prefixed dedupe and receipt hashing across caption, preview, and sibling-note refs so delimiter-heavy variants cannot collapse distinct outputs onto one job or receipt id
- GM prep packet rendering stays render-verified by requiring a `GovernedSourcePackId`, `SourcePackRevisionId`, `PacketRef`, and `SourceEntryId` plus sibling-only payloads before any media job can enqueue
- Parseable JSON GM prep payloads fail closed when required scope fields are missing or the root payload is not an object, so JSON strings, arrays, or note-only objects cannot bypass exact scope matching through text fallback
- Non-JSON GM prep payloads require exact keyed values or delimiter-safe scope tokens, so near-match source pack ids, packet refs, and source entry ids cannot pass by raw substring collision
- JSON and keyed text GM prep scope values trim surrounding whitespace before exact scope matching so padded governed payloads stay valid without reopening substring spoof paths
- GM prep packet rendering requires at least one opposition entry and preserves first-class packet, preview, and optional briefing receipt ids per governed entry
- GM prep packet rendering rejects duplicate source entries and packet refs inside one governed render request
- GM prep request-level rendering id, governed source pack id, source pack revision id, and source values normalize surrounding whitespace before scope enforcement so valid padded requests keep stable job ids and receipt ids
- GM prep packet subject receipt groups preserve grouped entry, packet, preview, briefing, and job ids so downstream shelves do not need to reconstruct governed packet evidence from raw artifact receipts
- GM prep packet entry receipt ids and subject receipt group ids stay scoped to governed source pack id, source pack revision id, and rendering id so reused packet refs cannot alias grouped evidence across governed packs
- GM prep packet dedupe includes governed source pack id, source pack revision id, rendering id, subject kind, source entry id, packet ref, artifact role, category, output format, and caller dedupe, while receipt hashes use length-prefixed subject-kind, artifact-role, and output-format segments so delimiter-heavy variants cannot collapse onto one render job or receipt id

## Release statement

Media completion is now good enough for program closure because documents, portraits, bounded video, route artifacts, campaign briefing bundles, starter onboarding bundles, runsite orientation bundles, structured media recipe bundles, build explain companion bundles, explain presenter sibling bundles, install-aware concierge bundles, GM prep packet bundles, and archive/retention all have explicit owner contracts, lifecycle governance, and executable verification. Future provider expansion is additive depth, not missing service ownership.
