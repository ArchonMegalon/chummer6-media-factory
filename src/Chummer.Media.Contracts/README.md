# Chummer.Media.Contracts

Canonical render-verified DTO package for `chummer-media-factory`.

Contract families:

- render requests (`Rendering/*`)
- render job queue state (`Jobs/*`)
- packet/document capability result contracts (`Packet*`)
- route cinema capability result contracts (`RouteCinema*`)
- campaign briefing bundle receipts (`CampaignBriefing*`), including locale-bundle rows with stable receipt ids, first-class requested-locale/fallback summary fields, and bounded fallback sibling bundle receipts
- starter onboarding artifact receipts (`StarterArtifact*`), including requested-locale and fallback locale groups plus first-class bundle, caption, preview, and support-note receipt rows for starter primers, first-session briefings, and support-safe onboarding companions
- runsite orientation bundle receipts (`RunsiteOrientation*`)
- structured media recipe receipts (`StructuredMediaRecipe*`), including structured publication-ready refs plus first-class role, publication, caption, and preview ref receipt rows with aggregate job and publication coverage
- build explain companion receipts (`BuildExplainCompanion*`), including approved explain packet-scoped sibling refs plus first-class role, caption, and preview receipt rows with direct asset-url and lifecycle truth for downstream explain shelves
- origin dossier narration receipts (`OriginDossierNarration*`), including approved origin-packet audio siblings, provider identity, and first-class role, caption, and preview receipt rows for derivative audiobook bundles without canon-generation authority
- creator promo kit receipts (`CreatorPromoKit*`), including approved manifest-scoped video, poster, and preview-card siblings plus first-class artifact, caption, and preview receipt groups with direct asset-url and lifecycle truth for creator publication shelves
- moderated testimonial publication receipts (`ModeratedTestimonial*`), including consent-backed, moderation-backed video, audio, transcript-card, and preview-card siblings plus first-class artifact, caption, and preview receipt groups for governed public-proof publication
- explain presenter sibling receipts (`ExplainPresenter*`), including approved explanation-packet audio or presenter siblings, grounding-scope identity, and first-party text fallback receipts without calculation authority
- install-aware concierge bundle receipts (`InstallAwareConcierge*`), including release explainer, support closure, and public concierge siblings plus first-class aggregate caption/preview/sibling-note refs and grouped caption, preview, and sibling-note receipt rows
- replay/exchange preview receipts (`ReplayExchangePreview*`), including recap, replay, and exchange preview-card plus inspectable sibling receipts with first-class bundle, kind, caption, and preview grouping for portable artifact shelves
- media asset manifest and lifecycle state (`Assets/*`)
- manifest store substrate operations (`Kernel/ManifestStoreContracts.cs`)
- render-job substrate transitions (`Kernel/RenderJobSubstrateContracts.cs`)
- preview and thumbnail linkage (`Kernel/PreviewLinkContracts.cs`)
- retention sweep execution contracts (`Kernel/RetentionSweepContracts.cs`)
- provider-neutral binary adapter contracts (`Storage/*`)
- deterministic lineage traversal contracts (`Lineage/*`)

Namespace policy:

- root namespace must be `Chummer.Media.Contracts`
- all public contract namespaces must stay under:
  - `Chummer.Media.Contracts.Rendering`
  - `Chummer.Media.Contracts.Jobs`
  - `Chummer.Media.Contracts.Assets`

Out of scope:

- narrative authoring
- campaign/session context
- approval policy and delivery policy
- route, map, or tactical truth
- rules/canon authoring and provider-routing policy
