# Roadmap

Four phases. Each phase ends only when its exit criteria are met and reviewed by the owner.

## Phase 1 — Foundation
Working agreements, repository skeleton, hosts, local environment, outbox/inbox, API and observability standards, CI, architecture tests, repo map generator.

**Exit criteria**
- A new module can be added from the template and its boundaries are enforced by CI.
- Fresh clone to running Api and Worker with documented commands.
- CI is green and required on `main`.
- Accepted ADRs exist for architecture style, module boundaries, inter-module communication, persistence/code standards, testing, API conventions, observability/config.

## Phase 2 — Identity and shared modules
Identity, Profile (protected personal data), CategoryCatalog, Media.

**Exit criteria**
- A user can register, sign in and manage a profile with visibility preferences.
- Personal data protection follows an accepted ADR with key-management design.
- Category tree and category attribute schemas are usable; uploads follow a safe lifecycle.

## Phase 3 — Marketplace core
Listing, Demand, Offer, Communication, Notification, AgreementReputation, basic Moderation, Search (PostgreSQL full-text, trigram typo tolerance, PostGIS distance).

**Exit criteria**
- End-to-end flow: publish a listing or demand, receive offers, converse, confirm agreement, leave a review.
- Concurrent offer acceptance yields exactly one accepted offer.
- Reports can be filed and reviewed with an audit trail.

## Phase 4 — Web, hardening, beta
Web client (React/Next.js), rate limiting, security tests, load tests, deployment pipeline, dashboards, small beta.

**Exit criteria**
- Real users can complete the Phase 3 flow in the browser.
- Security and load test results reviewed; known risks documented.
- Rollback and backup/restore procedures tested.

## Post-MVP backlog (not scheduled)
Mobile app, push campaigns and audience targeting, image similarity and AI category suggestions, AI-assisted moderation, dedicated search engine, premium tiers, reporting service, extraction of modules into services (only with measured need).
