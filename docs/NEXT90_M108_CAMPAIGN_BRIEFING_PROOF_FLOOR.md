# Next90 M108 Campaign Briefing Proof Floor

This repo-local proof floor closes `next90-m108-media-factory-campaign-briefing-renders` inside `chummer6-media-factory`.

## Package

- frontier id: `4459920059`
- milestone id: `108`
- package id: `next90-m108-media-factory-campaign-briefing-renders`
- proof floor commit: `ef3f006`
- owned surfaces: `campaign_briefing_bundle_rendering`, `campaign_artifact_receipts`
- allowed paths: `src`, `tests`, `docs`, `scripts`

## Proof anchors

- `src/Chummer.Media.Factory.Runtime/Assets/CampaignBriefingBundleService.cs` renders locale-matched cold-open and mission briefing media, caption, and preview siblings through media-factory job execution only.
- `src/Chummer.Media.Contracts/Compatibility/RunServices/MediaFactoryContracts.cs` defines `CampaignBriefingBundleRequest`, `CampaignBriefingBundleReceipt`, `CampaignBriefingLocaleBundleReceipt`, and `CampaignBriefingFallbackSiblingReceipt`.
- `tests/CampaignBriefingBundleSmoke/Program.cs` proves requested-locale cold-open and mission briefing siblings are mandatory, fallback locales stay bounded, caption and preview ids stay slot-aware, and delimiter-heavy locale variants cannot collapse dedupe or receipt ids.
- `tests/test_campaign_briefing_bundle_contracts.py` fail-closes contract drift so campaign briefing bundles stay first-class and render-only.
- `tests/test_m108_campaign_briefing_proof.py` fail-closes the generated proof floor so M108 stays pinned as a completed package with the current closure commit.
- `tests/test_m108_successor_package_authority.py` fail-closes queue, registry, generated-proof, and do-not-reopen drift so future shards verify the closed package instead of repeating it.
- `scripts/ai/materialize_media_release_proof.py` emits the M108 closure package into repo-local release proof receipts.
- `scripts/ai/verify.sh` runs the smoke project and proof tests as part of the standard media-factory verify lane.

## Guard conditions

- campaign briefing bundles must reject requests that omit the requested-locale `ColdOpen` entry
- campaign briefing bundles must reject requests that omit the requested-locale `MissionBriefing` entry
- campaign briefing bundles must render media, caption, and preview siblings for every locale entry
- campaign briefing bundles must keep the requested locale as the primary sibling and require every other locale to be a fallback sibling
- campaign briefing bundles must keep every requested or fallback locale bundle cold-open and mission-briefing complete
- campaign briefing bundles must allow at most two fallback locales
- campaign briefing receipts must preserve direct asset urls, locale receipt ids, locale bundle receipt ids, and per-entry job ids
- campaign briefing locale-bundle and fallback-sibling receipts must preserve slot-aware caption and preview sibling ids
- campaign briefing artifact receipts must preserve approval state, retention state, and storage class alongside asset urls
- campaign briefing dedupe keys and receipt hashes must use length-prefixed locale, artifact-kind, and output-format segments so delimiter-heavy variants cannot collapse onto one media job or receipt id
- normalized locale-bundle ordering must keep locale receipts, locale bundle receipts, fallback sibling receipts, and summary job ids stable when callers reorder the same bundle entries
- proof must not cite task-local telemetry, active-run handoff notes, or blocked helper commands as closure evidence
