# T-007 — Outbox/Inbox and event dispatch

- **Phase:** 1
- **Status:** todo
- **Depends on:** T-004, T-005
- **Approval:** required
- **Branch:** `task/T-007-outbox-inbox`

## Goal
Reliable in-process integration events: written atomically with state changes, dispatched by the Worker, consumed idempotently.

## Context to read
1. `AGENTS.md`
2. `docs/adr/ADR-003-*.md`
3. `src/Modules/Example/**`, `src/Shared/TalepPazari.SharedKernel/**`, `src/Hosts/TalepPazari.Worker/**`

## Allowed to change
- `src/Shared/TalepPazari.SharedKernel/**` (outbox/inbox abstractions only)
- `src/Modules/Example/**` (to demonstrate the pattern)
- `src/Hosts/TalepPazari.Worker/**`
- `tests/**` (integration tests), `docs/module-template/**`

## Must NOT change
- ADRs, other modules' internals (none exist yet)

## Deliverables
- Per-module outbox table in the module's schema; event written in the same transaction as the state change.
- Worker dispatcher: polls, dispatches to in-process handlers, retries with backoff, marks failed messages as dead after a configured number of attempts.
- Inbox/idempotency record so a handler processes the same event once.
- Example module: publishes one event, a second handler consumes it.
- Logs and metrics hooks with correlation IDs (full observability setup comes in T-008).

## Acceptance criteria
- [ ] Integration tests on real PostgreSQL prove: atomic write (event exists if and only if the state change committed), at-least-once delivery, idempotent handling on redelivery, retry then dead-letter on repeated failure.
- [ ] Crashing the Worker between dispatch and acknowledgement does not lose events.
- [ ] No message broker dependency was added.

## Verification
Run the integration tests and describe the crash scenario you simulated.

## Out of scope
- RabbitMQ, cross-instance coordination beyond what the design needs to avoid double dispatch (explain your approach to multiple Worker instances).

## Open questions to raise instead of deciding
- Small custom implementation versus a maintained, permissively licensed library: present both, recommend one, wait for approval.
