# Chummer.Media.Contracts

Canonical render-only DTO package for `chummer-media-factory`.

Contract families:

- render requests (`Rendering/*`)
- render job queue state (`Jobs/*`)
- packet/document capability result contracts (`Packet*`)
- route cinema capability result contracts (`RouteCinema*`)
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
- rules/canon authoring and provider-routing policy
