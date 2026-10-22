# ADR-003 — Inter-module communication

- **Status:** Proposed
- **Date:** 2026-10-22
- **Deciders:** project owner

## Context
Modules own their data ([ADR-002](ADR-002-module-boundaries-and-layout.md)), but business workflows span modules (for example, an accepted offer affects demands, notifications and reputation). We need reliable cross-module communication that keeps modules decoupled and does not depend on a shared transaction, even though all modules use one PostgreSQL database.

## Decision
- **Integration events** are written to a **per-module outbox** in the same transaction as the state change that produced them.
- The **Worker** host dispatches outbox events **in-process** to handlers.
- Each handler keeps an **inbox / idempotency record**, so a repeated delivery has no repeated effect.
- Delivery is **at-least-once**.
- Event contracts are **versioned** and live in the publisher's `Contracts` project.
- Failed handling is retried with backoff and dead-lettered after repeated failure. The details belong to T-007.
- **Synchronous calls** between modules go only through `Contracts` facades, preferably read-only queries.
- **No transaction spans modules**, even though one database is used. Cross-module workflows are eventually consistent.
- **RabbitMQ** (or any external broker) is not introduced until an ADR shows a need: module extraction or measured throughput.

## Alternatives considered
- **Shared transaction across modules** (one database, so one transaction is possible). Strong consistency, simple code. Rejected: couples modules at the data level and makes extraction impossible; failures in one module roll back another.
- **In-process events without an outbox** (publish after commit). Simple. Rejected: an event is lost if the process stops between commit and publish.
- **External message broker from day one** (RabbitMQ). Ready for extraction. Rejected: extra infrastructure and operations with no measured need; the outbox keeps the option open.
- **Synchronous command calls between modules as the default.** Simple call flow. Rejected: temporal coupling and cascading failures; allowed only through facades, preferably for queries.

## Consequences
- Positive: no lost events between state change and publish; modules stay decoupled.
- Positive: moving to an external broker later changes the dispatcher, not the publishers.
- Negative: handlers must be idempotent; workflows are eventually consistent and the UI must tolerate delay.
- Negative: outbox and inbox tables need monitoring and cleanup.
- Neutral: event contract changes follow versioning rules and contract tests ([ADR-005](ADR-005-testing-strategy.md)).

## Risks and mitigations
- **Duplicate delivery.** Mitigated by inbox/idempotency records per handler.
- **Poison events blocking dispatch.** Mitigated by retry with backoff and dead-lettering (details in T-007).
- **Breaking event changes.** Mitigated by versioned contracts and contract tests.
- **Hidden synchronous coupling via facades.** Mitigated by review: facades preferably read-only.

## Enforcement
- Architecture tests (T-006): modules reference each other only via `Contracts`.
- Integration tests (T-007): outbox write in the same transaction, at-least-once dispatch, idempotent handling.
- Contract tests for published events ([ADR-005](ADR-005-testing-strategy.md)).
- Review: no `TransactionScope` or shared DbContext across modules; any broker introduction references an accepted ADR.

## Revisit when
- A module is extracted into a separate service.
- Measured event throughput or dispatch latency exceeds what in-process dispatch from the outbox can handle.

## Open
- **Event ordering** guarantees (if any) are not decided.
- **Outbox and inbox retention** (how long processed records are kept) is not decided.
