# ADR-004 — Persistence and code standards

- **Status:** Accepted (2026-10-23)
- **Date:** 2026-10-22
- **Deciders:** project owner

## Context
Every module owns its schema and DbContext ([ADR-002](ADR-002-module-boundaries-and-layout.md)). Without shared standards, modules would model entities, validation, time and migrations differently, and the Domain layer would leak persistence concerns. The project rules already forbid Data Annotations and forced base classes.

## Decision
- **Database:** PostgreSQL, EF Core Code First.
- **Mapping:** EF Fluent API in `IEntityTypeConfiguration<T>` classes. No Data Annotations for mapping or validation.
- **Entities:** private setters; state changes through behavior-bearing methods and factories.
- **Base types:** `IEntity` only for identity; audit fields only where needed. No forced base class.
- **Identifiers:** `Guid`, time-ordered (UUID v7), generated in the application, not by the database.
- **Validation in three places:**
  - boundary validation at the application boundary with FluentValidation (or an approved alternative after a license/maintenance check);
  - invariants inside the domain;
  - constraints in the database.
- **Time:** `DateTimeOffset`, stored as UTC.
- **Money and other decimals:** explicit precision for every decimal column.
- **Concurrency:** optimistic concurrency where needed.
- **Migrations:** per module, reviewed, never auto-applied at production startup.
- **Pagination:** cursor pagination, as defined in [ADR-006](ADR-006-api-conventions.md).

## Alternatives considered
- **Data Annotations.** Less code. Rejected: mixes persistence and validation into the domain model and is forbidden by the project rules.
- **Common base entity class** (Id, audit fields, soft delete). Less repetition. Rejected: forces fields on entities that do not need them and couples all modules to one class.
- **Database-generated IDs** (identity/serial). Compact and ordered. Rejected: ID is only known after insert, which complicates outbox events and aggregates created in the application.
- **Random GUIDs (v4).** Application-generated. Rejected: poor index locality on insert.
- **Validation only in the domain** or **only at the boundary.** Simpler. Rejected: boundary validation gives good error messages, domain invariants protect correctness, database constraints protect against bugs; each catches what the others miss.
- **Auto-applying migrations at startup.** Convenient. Rejected for production: concurrent instances race, and failed migrations block startup without review.

## Consequences
- Positive: consistent modeling across modules; the Domain layer stays free of EF Core.
- Positive: IDs are known before persistence and sort roughly by creation time.
- Negative: more configuration code (Fluent API classes, validators).
- Negative: migrations need a deployment step separate from application startup.
- Neutral: soft delete is not decided here (see Open).

## Risks and mitigations
- **Missing decimal precision or non-UTC timestamps.** Mitigated by review and by EF model tests where practical.
- **Validation drift between layers.** Mitigated by tests at each layer for the rules that matter.
- **FluentValidation license or maintenance changes.** Mitigated by the license/maintenance check before adoption and the option of an approved alternative.

## Enforcement
- Architecture tests (T-006): Domain does not reference EF Core; no Data Annotations attributes on entities or DTOs.
- Integration tests against real PostgreSQL ([ADR-005](ADR-005-testing-strategy.md)) cover mappings, constraints and migrations.
- Review checklist: private setters, decimal precision, UTC, migration reviewed, no startup auto-migration in production configuration.

## Revisit when
- A module needs data access patterns that EF Core cannot serve within measured performance targets.
- FluentValidation fails the license or maintenance check.
- Identifiers shown to users must not reveal creation time (UUID v7 embeds a timestamp); reconsider the identifier format for such cases.

## Open
- **Soft delete** is not decided. To be decided together with data retention in Phase 2.
