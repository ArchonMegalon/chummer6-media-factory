# Chummer media-factory split guide

Date: 2026-03-09

This repo is now real, but it is still scaffold-stage. Treat the approved split guide as the local source of truth for the next extraction wave:

- create `Chummer.Media.Contracts` as the canonical render-only package plane
- keep narrative drafting, approvals policy, delivery, and campaign/session orchestration upstream in `chummer.run-services`
- build the shared asset kernel first: manifests, binary storage, render jobs, previews, TTL, retention, and lineage
- only then move deterministic document rendering, portrait execution, and video execution into this repo

Non-negotiable boundary:

- render-only jobs and asset lifecycle belong here
- canon decisions, rules math, session relay, Spider policy, delivery policy, and general AI routing do not
