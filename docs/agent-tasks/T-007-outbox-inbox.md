# T-007 — Outbox/Inbox and event dispatch

- **Phase:** 1
- **Status:** todo
- **Depends on:** T-004, T-005, T-006 (done); ADR-003 (Accepted), ADR-005 (Accepted)
- **Approval:** required
- **Branch:** `task/T-007-outbox-inbox`

## Goal
Reliable in-process integration events: written atomically with state changes, dispatched by the Worker, consumed idempotently.

## Context to read
1. `AGENTS.md`
2. `docs/adr/ADR-002-*.md`, `ADR-003-*.md`, `ADR-005-*.md`
3. `src/Modules/Example/**` (including `CONTEXT.md`), `src/Shared/TalepPazari.SharedKernel/**`, `src/Hosts/TalepPazari.Worker/**`
4. `docs/module-template/how-to-add-a-module.md`
5. `tests/TalepPazari.ArchitectureTests/**` (rules your code must satisfy; read, do not weaken)

## Facts about the code today (verify, do not assume)
- `ExampleItemRepository.AddAsync` calls `SaveChangesAsync` immediately. Atomic outbox writes need the state change and the outbox row saved in one transaction.
- `ExampleItemCreatedV1` exists in `Contracts` but is never published.
- The Worker must keep starting and logging `Worker started` when the database is down (T-005 criterion). The dispatcher must tolerate an unavailable database (log, back off, keep running).
- Architecture tests (T-006) are active: Infrastructure exposes one public type (`<Name>Module`), Domain depends only on the BCL and SharedKernel, Application only on its own Domain/Contracts, SharedKernel, FluentValidation and DI abstractions, hosts use module types only from Infrastructure/Api, every project under `src/` must match a known layout.
- Event ordering and outbox/inbox retention are still open in ADR-003. Do not decide them here.

## Allowed to change
- `src/Shared/TalepPazari.SharedKernel/**` (outbox/inbox abstractions only; report each addition)
- `src/Modules/Example/**` (demonstrate the pattern, including a new migration for outbox/inbox tables in schema `example`)
- `src/Hosts/TalepPazari.Worker/**`
- `tests/**` (new integration test project and its helpers), `TalepPazari.slnx`
- `Directory.Packages.props` (each new package needs a license and maintenance check, with evidence, in your plan)
- `docs/module-template/**`, `AGENTS.md` (Commands and Map only)
- Architecture tests only if a rule must learn about a new, approved location (propose the change in your plan; never weaken a rule to make code pass)

## Must NOT change
- ADRs, `Directory.Build.props`, `global.json`, `.github/*`, `docs/agent-tasks/README.md`

## Deliverables
- Per-module outbox table in the module's schema; the event is written in the same transaction as the state change.
- Worker dispatcher: polls, dispatches to in-process handlers, retries with backoff, marks messages dead after a configured number of attempts.
- Inbox/idempotency record so a handler processes the same event once.
- Example module: publishes `ExampleItemCreatedV1`, a second handler consumes it.
- Safe operation with several Worker instances (for example row locking so two instances do not dispatch the same message). Explain your approach.
- Hooks for correlation ID, logs and metrics (full setup is T-008).
- Integration tests on real PostgreSQL (see open questions) in a new test project, with a documented command in `AGENTS.md`.
- Update `how-to-add-a-module.md` and Example `CONTEXT.md` for the outbox pattern.

## Acceptance criteria
- [ ] Integration tests prove: atomic write (event exists if and only if the state change committed), at-least-once delivery, idempotent handling on redelivery, retry then dead-letter on repeated failure.
- [ ] Crashing the Worker between dispatch and acknowledgement does not lose events (describe the simulated crash).
- [ ] Two dispatcher instances do not process the same message concurrently (test or documented evidence).
- [ ] The Worker still starts and logs `Worker started` with the database stopped.
- [ ] No message broker dependency was added.
- [ ] Architecture tests still pass unchanged, or any change to them is listed and justified.
- [ ] `dotnet build TalepPazari.slnx` has zero warnings; `dotnet test TalepPazari.slnx` passes; `dotnet format --verify-no-changes` is clean.
- [ ] Integration tests do not touch the developer's local `example` data (separate database or schema).

## Verification
Run build, tests and format check. Describe the crash scenario. Say in "Not verified" what you did not check.

## Out of scope
- RabbitMQ or any broker, event ordering guarantees, outbox/inbox retention and cleanup (ADR-003 Open), CI wiring (T-009), final observability setup (T-008).

## Decisions already made (do not reopen)
- Delivery is at-least-once; handlers are idempotent via an inbox record (ADR-003).
- Per-module outbox in the module's own schema; no cross-module transaction.
- Time comes from `TimeProvider`; ids are UUID v7 generated in the application.

## Open questions to raise instead of deciding
- **Where does the dispatcher code live?** SharedKernel may hold abstractions only (ADR-002). Options include module Infrastructure, a new shared project (which needs an architecture decision and a test update), or another place. Compare and recommend one; wait for approval.
- **Custom implementation versus a maintained, permissively licensed library.** Present both with license and maintenance evidence (check for recent license changes), recommend one, wait for approval.
- **How integration tests get PostgreSQL.** Compare Testcontainers and a dedicated database on the compose instance (license, maintenance, Docker requirement, isolation from development data, CI fit for T-009). Recommend one, wait for approval.
- **Unit of work shape.** How the repository and outbox share one transaction without breaking the layering rules.
- Anything in Accepted ADRs or architecture tests that blocks a deliverable.

## Report
Fill `docs/templates/task-report.template.md` in the PR description. Do not include local paths, user names or machine details.
