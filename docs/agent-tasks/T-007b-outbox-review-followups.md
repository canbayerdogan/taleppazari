# T-007b — Outbox review follow-ups

- **Phase:** 1
- **Status:** todo
- **Depends on:** T-007 (done)
- **Approval:** required
- **Branch:** `task/T-007b-outbox-followups`

## Goal
Fix the medium and low findings of the T-007 fresh-agent review that are worth fixing now. No new features.

## Context to read
1. `AGENTS.md`
2. `docs/agent-tasks/T-007-outbox-inbox.md`
3. `src/Hosts/TalepPazari.Worker/Outbox/**`
4. `src/Modules/Example/**` (Infrastructure outbox store, inbox decorator, registration, `CONTEXT.md`)
5. `tests/TalepPazari.IntegrationTests/**`
6. `docs/module-template/how-to-add-a-module.md`

## Allowed to change
- `src/Hosts/TalepPazari.Worker/Outbox/**`
- `src/Modules/Example/**`
- `tests/TalepPazari.IntegrationTests/**`
- `docs/module-template/how-to-add-a-module.md`, `AGENTS.md` (Commands only)

## Must NOT change
- ADRs, `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `.github/*`, `docs/agent-tasks/README.md`
- `tests/TalepPazari.ArchitectureTests/**`; SharedKernel public interfaces (if one must change, stop and ask)

## Deliverables
1. **M1 (no personal data in errors):** `LastError` stores only the exception type name. Warning/Error logs carry exception type and MessageId, not the exception message; the full exception is logged at Debug only. Update how-to and Example `CONTEXT.md`.
2. **M2:** every `Mark*` statement also requires `"ProcessedAt" IS NULL AND "DeadLetteredAt" IS NULL`. Update the SQL shown in the how-to.
3. **M3 (test):** a decorated handler that throws leaves no inbox row and no effect; a retry then applies the effect exactly once. Use the real inbox decorator, not a bare handler.
4. **M4 (test):** hold `SELECT ... FOR UPDATE` on some rows in a separate open transaction; a claim must skip exactly those rows and return without waiting. Release, then the rows are claimable. Remove or make deterministic the "both dispatchers took part" assertion (L2).
5. **L1:** atomicity tests assert the specific database exception, so a validation failure cannot pass them.
6. **L3:** the inbox decorator throws on `Guid.Empty` message id (with a test).
7. **L4:** `ON CONFLICT ("MessageId", "HandlerName") DO NOTHING`.
8. **L6:** a handler throws an exception whose message contains the item id; assert `LastError` contains only the exception type and not the id (and the Warning/Error log does not contain it, if a test logger is practical).
9. **L7:** AGENTS.md: say the forced-Docker mode is what T-009 CI will use.
10. **M5 (document only):** add a "Known limits" note to the how-to and Example `CONTEXT.md`: a shutdown or failed write mid-batch leaves the remaining claimed messages with their incremented `Attempts` and lease; frequent interruptions can dead-letter a healthy message; to revisit with the deployment target.

## Acceptance criteria
- [ ] Mutation evidence (temporary, reverted, shown in the report): for M2, M3, M4, L3 and M1, a deliberate regression makes the new test fail.
- [ ] Architecture tests pass unchanged. `dotnet build TalepPazari.slnx` has zero warnings; `dotnet test TalepPazari.slnx` passes with Docker; `dotnet format TalepPazari.slnx --verify-no-changes` is clean.
- [ ] No migration is needed. If a schema change turns out to be necessary, stop and ask.

## Out of scope
- Releasing claimed messages on shutdown (M5 behavior change), keyed/inbox-only registration of handlers (L5), a `WorkerService` loop test (L8), retention, ordering, T-008 topics.

## Decisions already made (do not reopen)
- `LastError` holds the exception type only; messages are never stored.
- M5 stays an accepted, documented limit for now.

## Report
Fill `docs/templates/task-report.template.md` in the PR description. No local paths, user names or machine details.
