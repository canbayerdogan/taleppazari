# T-008b — Observability and health

- **Phase:** 1
- **Status:** todo
- **Depends on:** T-008a, T-007 (done); ADR-007 (Accepted), ADR-006 (header name)
- **Approval:** required
- **Branch:** `task/T-008b-observability`

## Goal
One operation can be followed from the Api request through the outbox into the Worker. Logs are structured and safe, traces and metrics are exported through a configurable exporter, and the Api reports liveness and readiness.

## Context to read
1. `AGENTS.md`
2. `docs/adr/ADR-003-*.md`, `ADR-006-*.md`, `ADR-007-*.md`
3. `src/Hosts/**`, `src/Shared/**`, `src/Modules/Example/**` (outbox code and `CONTEXT.md`)
4. `docs/architecture/api-conventions.md`, `docs/module-template/how-to-add-a-module.md`
5. `tests/**` (architecture tests and the integration-test fixture: read, do not weaken)

## Facts about the code today (verify, do not assume)
- The outbox column `CorrelationId` is nullable and written as null (T-007). `TraceParent` comes from `Activity.Current`. `IIntegrationEventContext` carries both, and the dispatcher's log scope includes them.
- Outbox telemetry exists: an `ActivitySource` and a `Meter` named `TalepPazari.Outbox` with counters `outbox.messages.dispatched`, `.failed` and `.dead_lettered`. No exporter is wired in any host.
- The Api host has a `/health/live` placeholder and no readiness endpoint. The Worker has no HTTP surface.
- Logging rule from T-007b: Warning and Error logs carry the exception type and ids, never exception messages or payloads; the full exception is logged at Debug only. EF Core writes its own connection-failure logs, which nothing controls yet. `EnableSensitiveDataLogging` and `EnableDetailedErrors` are not set.
- Both hosts must start when the database is down (the connection string is resolved lazily).
- Architecture tests (T-006) apply as in T-008a. SharedKernel has no package reference.

## Allowed to change
- `src/Hosts/**`, `src/Shared/**` (cross-cutting; interfaces and plain records unless approved otherwise in your plan)
- `src/Modules/Example/**` (Infrastructure only: the outbox writes the correlation ID, the module registers its readiness check; no Domain change)
- `tests/**`, `TalepPazari.slnx`
- `Directory.Packages.props` (each new package needs a license and maintenance check with evidence, including any recent license change; stable, non-prerelease versions published at least two weeks ago)
- `docs/architecture/observability.md` (new), `docs/module-template/**`, `AGENTS.md` (Commands and Map)
- Architecture tests only if a rule must learn a new, approved location (propose first; never weaken a rule)

## Must NOT change
- ADRs, `Directory.Build.props`, `global.json`, `.github/*`, `docs/agent-tasks/README.md`

## Deliverables
1. **Correlation ID** (ADR-007 owns the rule, ADR-006 names the header `X-Correlation-ID`).
   - Accept the incoming value only if it passes length and character validation, otherwise generate one. Propose the exact limits in your plan.
   - Return it in the response header and include it in every log entry.
   - The outbox writes it into the `CorrelationId` column. The Worker restores it from the message into the log scope and into events published by handlers. With no ambient value it stays null.
   - How it travels in-process and where its abstraction lives is an open question below.
2. **Structured logging with redaction.** Passwords, tokens, message bodies and unnecessary personal data are never logged. Request bodies and query strings are never logged. Configure log levels so EF Core and Npgsql output cannot leak a password or connection string; show what they write when the database is down and when the password is wrong.
3. **OpenTelemetry traces and metrics** in both hosts with a configurable exporter (console by default, switchable off). Include the existing `TalepPazari.Outbox` source and meter; keep their names unless you propose a rename with a reason. No vendor SDK; the backend is not chosen (ADR-007 Open).
4. **Health.**
   - `/health/live` reports the process only and does not check dependencies.
   - `/health/ready` is database-aware through a check that the module registers itself (internal, registered in `Add<Name>Module`); readiness covers only the host's own critical dependencies (ADR-007).
   - Responses carry status only: no connection strings or exception text.
   - Worker health is **not implemented here** (open question below).
5. `docs/architecture/observability.md` (about one page), how-to additions (health check registration, correlation ID), Example `CONTEXT.md`, `AGENTS.md` Commands.

## Acceptance criteria
- [ ] The same correlation ID appears in the response header, a log line and the outbox row (test). A Worker handler logs it, and an event published by that handler carries it.
- [ ] An incoming correlation ID that is too long or has unexpected characters is replaced; a valid one is kept (tests for both).
- [ ] Redaction test: a sample password, token and message body do not appear in any captured log entry.
- [ ] With the database down and with a wrong database password, captured logs contain no password and no connection string (evidence).
- [ ] Readiness reports unhealthy when the database is unreachable and healthy when it runs: an automated test, plus a manual run against the local compose database stopped and started (show both results). Liveness stays 200 with the database down.
- [ ] Both hosts start with the database stopped (the Worker logs `Worker started`).
- [ ] A test listener proves the outbox counters and activity are emitted, and the exporter switch works (console by default). This closes the T-007 "no listener test" gap.
- [ ] Architecture tests pass unchanged, or every change is listed and justified. Build has zero warnings, tests pass, `dotnet format TalepPazari.slnx --verify-no-changes` is clean.
- [ ] Tests that need no database run without Docker.

## Verification
Run build, tests and format check. Show a sample request/response and the matching log line (fake data only), plus the health results. Say in "Not verified" what you did not check.

## Out of scope
- Authentication and authorization, rate limiting, dashboards, alerting, a telemetry backend, a secret store
- Worker health mechanism (report options only), outbox/inbox retention, event ordering
- Changes to problem-details shape beyond adding a correlation member if you propose it and it is approved

## Decisions already made (do not reopen)
- Header name `X-Correlation-ID`; the correlation rule is owned by ADR-007.
- The T-007b logging rule applies project-wide: Warning and above never include exception messages or payloads (type and ids only); the full exception is Debug only.
- Hosts must stay startable with the database down.
- The correlation ID is not a secret and is never used for authorization.
- Only stable packages; no vendor-specific SDK.

## Open questions to raise instead of deciding
- **Logging library.** Compare at least two (for example Serilog, the built-in `Microsoft.Extensions.Logging` with a JSON console formatter, NLog) on license (check for recent changes), maintenance and fit, including how each does redaction, scopes and configuration. Recommend one and wait.
- **Redaction mechanism** for the chosen option.
- **How the correlation ID travels in-process, and where its abstraction lives.** SharedKernel holds interfaces and plain records only; an ambient holder is code. Options include an interface in SharedKernel with the implementation in the hosts, log scope or `Activity` baggage, or another place. Recommend one and wait.
- **Worker health mechanism** (ADR-007 Open): compare a small HTTP listener, a heartbeat record and an orchestrator-level check. Recommend one but do not implement; it is decided together with the deployment target.
- **EF Core and Npgsql log-level policy**, and whether the problem-details body (T-008a) should carry a correlation member.
- **OpenTelemetry package choices** (stable only).
- Anything in the Accepted ADRs or architecture tests that blocks a deliverable.

## Report
Fill `docs/templates/task-report.template.md` in the PR description. Do not include local paths, user names or machine details.
