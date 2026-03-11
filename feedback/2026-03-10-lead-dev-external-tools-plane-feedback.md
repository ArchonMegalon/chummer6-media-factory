# Lead-dev feedback: media-factory external integrations

Date: 2026-03-10

Media-factory is the only repo allowed to own media/render/archive adapters.

Initial vendor map to preserve:

* MarkupGo - document render
* PeekShot - preview and thumbnail generation
* Mootion - bounded video
* AvoMap - route visualization
* Internxt - cold archive

Required rules:

* every media job produces a Chummer manifest
* provider output is never the canonical asset record by itself
* provenance, safety, retention, and archive decisions are captured explicitly
* provider choice remains adapter-private and kill-switchable
