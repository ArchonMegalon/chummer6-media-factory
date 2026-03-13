# Lead-dev feedback: media-factory external integrations

Date: 2026-03-10

Media-factory is the only repo allowed to own media/render/archive adapters.

Initial vendor map to preserve:

* MarkupGo - document render
* Browserly - browser-assisted capture and reference extraction for upstream render inputs
* PeekShot - preview and thumbnail generation
* Mootion - bounded video
* AvoMap - route visualization
* Internxt - cold archive

Required rules:

* every media job produces a Chummer manifest
* provider output is never the canonical asset record by itself
* browser-assisted capture stays evidence/provenance input until promoted into a Chummer-owned asset manifest
* provenance, safety, retention, and archive decisions are captured explicitly
* provider choice remains adapter-private and kill-switchable
