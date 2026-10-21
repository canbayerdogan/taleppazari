# T-002 — Draft ADR-001..007

- **Phase:** 1
- **Status:** todo
- **Depends on:** T-001
- **Approval:** required
- **Branch:** `task/T-002-draft-adrs`

## Goal
Turn the owner's decision summaries below into seven ADR files using `docs/templates/adr.template.md`. This is documentation only. All ADRs are written with status **Proposed**; the owner accepts them later.

## Context to read
1. `AGENTS.md`
2. `SUMMARY.md`
3. `docs/templates/adr.template.md`
4. This brief (decision summaries are the source)

## Allowed to change
- `docs/adr/ADR-001-*.md` … `ADR-007-*.md`, `docs/adr/README.md` (index table)

## Must NOT change
- Any code, `SUMMARY.md`, `AGENTS.md`

## Rules for drafting
- Do not add decisions that are not listed below. If you see a gap, add it to the ADR's "Revisit when" or to your open questions.
- Write real alternatives with honest trade-offs (including why they were rejected).
- Every ADR needs an "Enforcement" section: how the decision is checked (architecture test, CI, review).
- Keep each ADR to about one to two pages.

## Decision summaries

**ADR-001 Modular monolith first.** One codebase, organized in modules with enforced boundaries; two hosts (Api, Worker) from the same codebase; stateless, multiple instances. Extraction of a module into a separate service needs a new ADR with measured need (scaling, team, release cadence). Rejected: microservices from day one (operational cost, boundaries not yet validated); layered monolith without module boundaries. Risks to cover: single process failure domain, mitigated by multiple instances, Api/Worker separation, health checks, timeouts, limits, safe rollout.

**ADR-002 Module boundaries and layout.** Proposed layout per module under `src/Modules/<Name>/`: `Contracts` (public: DTOs, integration events, query/command facade interfaces), `Domain`, `Application`, `Infrastructure` (EF Core, repositories, module registration), `Api` (HTTP endpoints). Rules: another module may reference only `Contracts`; implementation types are `internal` where practical; one PostgreSQL schema per module; one DbContext and migration set per module; no cross-schema joins or foreign keys; minimal SharedKernel (identity abstractions, clock, Result, outbox abstractions, no domain logic). Layout is Proposed: the owner may adjust it when reviewing.

**ADR-003 Inter-module communication.** Integration events written to a per-module outbox in the same transaction as the state change; Worker dispatches in-process to handlers; inbox/idempotency record per handler; at-least-once delivery; versioned event contracts live in the publisher's `Contracts`. Synchronous calls between modules only through `Contracts` facades, preferably read-only queries. No transaction spanning modules, even though one database is used. RabbitMQ is not introduced until an ADR shows a need (extraction or measured throughput).

**ADR-004 Persistence and code standards.** PostgreSQL, EF Core Code First, Fluent API in `IEntityTypeConfiguration<T>` classes; no Data Annotations; entities with private setters and behavior-bearing methods/factories; `IEntity` only for identity, audit fields only where needed (no forced base class); FluentValidation (or an approved alternative after a license/maintenance check) at the application boundary, invariants inside the domain, constraints in the database; `DateTimeOffset` stored as UTC; explicit decimal precision; optimistic concurrency where needed; migrations per module, reviewed, never auto-applied at production startup; cursor pagination.

**ADR-005 Testing strategy.** Unit tests for domain; integration tests against real PostgreSQL (containers); architecture tests for boundary rules; contract tests for events and OpenAPI snapshots; security tests added as features arrive (authorization, ownership, input handling); CI gates described in T-009.

**ADR-006 API conventions.** `/api/v1` versioning; OpenAPI per host; errors as RFC 9457 problem details; correlation ID header propagated to logs and events; cursor pagination; resource-based authorization in the application layer; no business rules in endpoints; idempotency keys for unsafe operations where retries are expected.

**ADR-007 Observability and configuration.** Structured logging with correlation IDs; OpenTelemetry traces and metrics; liveness and readiness endpoints; configuration via environment/secret store, no secrets in the repository; log redaction rules (never log passwords, tokens, message bodies or unnecessary personal data).

## Acceptance criteria
- [ ] Seven ADR files exist with the template sections, status `Proposed`.
- [ ] `docs/adr/README.md` index lists them.
- [ ] No ADR contains a decision not present in this brief.
- [ ] Open questions are listed in the PR report.

## Verification
List the files and run a Markdown link check if available; otherwise state "not verified".

## Out of scope
- ADRs for personal-data protection, retention, search, media, notifications, reputation (later phases).
