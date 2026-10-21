# TalepPazarı — Project Summary

> Purpose: compact entry point for people and AI coding agents. Defines product boundaries, architecture style and engineering constraints. It is NOT the full specification; details live in `docs/`.

## 1. Product in one paragraph

TalepPazarı is a peer-to-peer marketplace for product/service **listings** and a separate **demand** market. A user can publish what they have (listing) or what they need (demand); providers respond to demands with **offers**. Users talk, agree, and complete any real-world transaction privately. The platform does **not** process payments, arrange shipping, run warehouses, or manage inventory.

## 2. Core product concepts

- **Listing:** an item or service offered by a user.
- **Demand:** a user's request for an item or service.
- **Offer:** a provider's response to a demand (revisions are kept as history).
- **Communication:** private conversations between authorized participants.
- **Agreement and reputation:** mutual confirmation of an interaction, followed by eligible reviews. Never present an interaction as platform-verified when payment/delivery happens outside the platform.
- **Trust and safety:** reporting, moderation, abuse prevention, audit trail and dispute handling are foundational.
- **Listing limits:** each user has a small active-listing limit at first (about 5), higher tiers later. Model this as a policy/entitlement, never hard-code it.
- **Categories:** curated hierarchy with category-specific attributes. Prevent wrong categorization by design.
- **Search:** location-aware and typo-tolerant. Turkish text handling matters (ı/İ, ğ/g, ş/s, ö/o, ü/u, ç/c).

## 3. Architecture style: modular monolith

- One codebase and solution, organized into **modules** with strictly enforced boundaries (see `docs/adr/` once accepted).
- Two runtime hosts built from the same codebase: **Api** (user-facing requests) and **Worker** (background jobs, outbox dispatch, notifications, media processing). Both are stateless and can run as multiple instances.
- Each module owns its schema, DbContext, migrations, public contracts and events.
- Modules talk to each other only via `Contracts` projects and integration events.
- A module may be extracted into a separate service only through a new ADR backed by measured need (scaling, team, release cadence).

### Modules

| Module | Responsibility | Phase |
|---|---|---|
| Identity | accounts, authentication | 2 |
| Profile | public profile, visibility preferences, protected personal data | 2 |
| CategoryCatalog | category hierarchy, attribute schemas | 2 |
| Media | upload sessions, metadata, lifecycle | 2 |
| Listing | listings | 3 |
| Demand | demands | 3 |
| Offer | offers, revisions, acceptance decision | 3 |
| Communication | conversations, messages | 3 |
| Notification | in-app and e-mail notifications | 3 |
| AgreementReputation | agreement confirmation, reviews | 3 |
| Moderation | reports, review queue (basic) | 3 |
| Search | text, filter, geo search over read models | 3 |

Later (not planned in detail): MediaIntelligence, AudienceCampaign, Reporting, push campaigns, mobile app.

## 4. Technology baseline

- **Runtime/API:** ASP.NET Core on **.NET 10 LTS**, C# with nullable reference types. Latest supported patch; no preview releases for foundations without an ADR.
- **Persistence:** PostgreSQL (PostGIS for geo queries), EF Core Code First, Fluent API.
- **Messaging:** in-process integration events with Outbox/Inbox first. RabbitMQ only when an ADR justifies it (module extraction or measured throughput).
- **API:** REST, OpenAPI, versioned (`/api/v1`).
- **Observability:** structured logs, metrics, traces, correlation IDs, health checks.
- **Testing:** unit, integration (real PostgreSQL), architecture, contract, security.
- **Web (Phase 4):** React with TypeScript (Next.js). Mobile comes later and reuses the same API.
- Check the license and maintenance status of every new dependency. Any major deviation needs an ADR.

## 5. Engineering principles

1. SOLID and separation of concerns, applied pragmatically.
2. Domain logic is independent of HTTP, persistence, messaging and external providers.
3. No Data Annotations. Use EF Fluent API, boundary validation, domain invariants and database constraints.
4. Base entity abstractions only where they add value; no forced inheritance.
5. No cross-module joins, foreign keys or navigation properties. Share identifiers and versioned contracts, not domain models.
6. Cross-module workflows are eventually consistent.
7. Security and privacy from day one: least privilege, resource-level authorization, rate limiting, safe uploads, secret management.
8. Passwords are one-way hashed. Personal data encryption follows an ADR; never invent cryptography.
9. Do not expose exact addresses or private contact details in listings or search by default.
10. Retention, deletion, moderation access and evidence preservation follow an explicit data-governance policy (legal review pending). Do not promise indefinite retention.
11. Never log passwords, tokens, message bodies or unnecessary personal data.
12. AI-generated summaries are indexes, not truth. Code, tests, contracts and accepted ADRs win.

## 6. Phases

See `docs/roadmap.md`. Current: **Phase 1 — Foundation**. Do not implement marketplace features during Phase 1.

## 7. Working with AI agents

See `AGENTS.md`. Small scoped tasks, one branch and one PR each, plan before code, report with verification evidence.

## 8. Initial non-goals

- Payments, shipping, warehouse or inventory management.
- Image embeddings, AI classification, ad/campaign targeting, dedicated search engine, mobile app.
- Splitting modules into separate services.
- A full code-graph platform before measurements show it is needed.
