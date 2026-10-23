# ADR-002 — Module boundaries and layout

- **Status:** Proposed
- **Date:** 2026-10-22
- **Deciders:** project owner

## Context
[ADR-001](ADR-001-modular-monolith-first.md) chooses a modular monolith. A modular monolith only works if every module has a predictable shape, a small public surface and exclusive ownership of its data. Agents and people need one layout they can follow and that tests can check.

## Decision
Each module lives under `src/Modules/<Name>/` and has these projects:

| Project | Content | Visibility |
|---|---|---|
| `Contracts` | DTOs, integration events, query/command facade interfaces | public to other modules |
| `Domain` | entities, value objects, domain invariants | module-internal |
| `Application` | use cases, boundary validation, authorization | module-internal |
| `Infrastructure` | EF Core, repositories, module registration | module-internal |
| `Api` | HTTP endpoints | module-internal (registered by the Api host) |

Rules:
- Another module may reference **only** the `Contracts` project of a module.
- The hosts (Api, Worker) may reference a module's `Infrastructure` and `Api` projects for composition only (service registration and endpoint mapping). The module-to-module rule applies between modules, not between hosts and modules.
- Implementation types are `internal` where practical.
- One PostgreSQL **schema** per module.
- One **DbContext** and one **migration set** per module.
- No cross-schema joins and no cross-schema foreign keys.
- A minimal **SharedKernel** contains only: identity abstractions, clock, `Result`, outbox abstractions. It contains no domain logic.

The layout is Proposed: the owner may adjust it when reviewing.

## Alternatives considered
- **Endpoints in Infrastructure** (no separate `Api` project). Fewer projects per module. Rejected: mixes HTTP concerns with persistence, makes "no business rules in endpoints" harder to check, and makes it harder to keep HTTP dependencies out of everything except the endpoint layer.
- **Fewer projects per module** (one project with folders). Less ceremony. Rejected: folder boundaries cannot be enforced by project references; `internal` visibility would no longer separate layers.
- **Shared DbContext / shared schema.** Simpler queries across features. Rejected: breaks data ownership and makes extraction impossible.
- **Rich SharedKernel** (shared base entities, shared domain types). Less duplication. Rejected: becomes a coupling hub; changes ripple through every module.

## Consequences
- Positive: a uniform shape for every module; boundaries are checkable by project references and architecture tests.
- Positive: data ownership is explicit (schema per module).
- Negative: more projects in the solution; some data that would be a join must be copied via events or fetched via a facade.
- Negative: reporting across modules needs read models, not joins.
- Neutral: cross-module communication is defined in [ADR-003](ADR-003-inter-module-communication.md).

## Risks and mitigations
- **Contracts growing into a shared domain model.** Mitigated by review: Contracts contain DTOs, events and facade interfaces only.
- **SharedKernel creep.** Mitigated by review: additions need a reason that applies to all modules and must not contain domain logic.
- **Too many projects slowing builds.** Measure; revisit layout if it becomes a real problem.

## Enforcement
- Architecture tests (T-006): a module references other modules only via `Contracts`; hosts reference module `Infrastructure` and `Api` projects for composition only; Domain does not reference EF Core, HTTP, messaging or provider libraries; SharedKernel does not reference any module.
- Migration and DbContext review: each DbContext maps only to its own schema.
- Module template (T-005) produces this layout.
- CI (T-009) runs the architecture tests.

## Revisit when
- The owner adjusts the layout during review of this ADR.
- Build time or solution size measurably hurts development.
- A module needs a different shape (for example, a read-model-only module such as Search) and the rules above do not fit.
