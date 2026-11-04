# T-008b — Observability and health

- **Phase:** 1
- **Status:** todo
- **Depends on:** T-008a2, T-009 (done); T-007 (done); ADR-007 (Accepted), ADR-006 (header name)
- **Approval:** required
- **Branch:** `task/T-008b-observability`

## Goal
One operation can be followed from the Api request through the outbox into the Worker. Logs are structured and safe, traces and metrics are exported through a configurable exporter, and the Api reports liveness and readiness.

## Context to read
1. `AGENTS.md`
2. `docs/adr/ADR-003-*.md`, `ADR-006-*.md`, `ADR-007-*.md`
3. `src/Hosts/**`, `src/Shared/**`, `src/Modules/Example/**` (outbox code and `CONTEXT.md`)
4. `docs/architecture/api-conventions.md`, `docs/module-template/how-to-add-a-module.md`
5. `tests/**` (architecture tests, Api tests, integration-test fixture and `TestLoggerProvider`: read, do not weaken)
6. `.github/workflows/ci.yml` (read only)

## Facts about the code today (verify, do not assume)
- Outbox: `OutboxMessage` and `IIntegrationEventContext` carry `TraceParent` and a nullable `CorrelationId`. `ExampleOutbox` writes `TraceParent` from `Activity.Current` and `CorrelationId` as null. The dispatcher's log scope already contains both.
- Outbox telemetry exists in the Worker: an `ActivitySource` and a `Meter` named `TalepPazari.Outbox` with counters `outbox.messages.dispatched`, `.failed`, `.dead_lettered`. No exporter is wired in any host and no listener test exists.
- The Api host has `ProblemJsonWriter` (adds `traceId`), `ApiExceptionHandler`, `ResponseStartedExceptionMiddleware` and `UnhandledExceptionLog` under `ErrorHandling/`. An exception after the response started is logged (type and route template), the request is aborted and the exception is swallowed, so hosting diagnostics and metrics see a finished request, not an error (T-008a review L3).
- A client that cancels a request produces an `OperationCanceledException`; `ApiExceptionHandler` currently treats it like any exception and logs it at Error (T-008a review L1).
- The framework's Information-level request log writes the full URL including the query string. Today only `appsettings.json` keeps `Microsoft.AspNetCore` at Warning; nothing in code or tests enforces it.
- Logging rule (T-007b/T-008a): Warning and above carry only the exception type and ids or the route template, never exception messages, payloads, query strings or bodies; the full exception is logged at Debug only. EF Core and Npgsql write their own logs, which nothing controls yet. `EnableSensitiveDataLogging` and `EnableDetailedErrors` are not set.
- The Api has a `/health/live` placeholder and no readiness endpoint. The Worker has no HTTP surface.
- Both hosts must start when the database is down (connection string resolved lazily).
- Architecture tests (T-006) apply unchanged. SharedKernel has no package reference.
- CI runs on every pull request (build, format, all tests with `TALEPPAZARI_REQUIRE_DOCKER=true`, CodeQL, secret scan). This change must pass it.

## Allowed to change
- `src/Hosts/**`, `src/Shared/**` (cross-cutting; interfaces and plain records unless approved otherwise in your plan)
- `src/Modules/Example/**` (Infrastructure only: the outbox writes the correlation ID, the module registers its readiness check; no Domain or Application change unless you show it is unavoidable and ask)
- `tests/**`, `TalepPazari.slnx`
- `Directory.Packages.props` (each new package needs a license and maintenance check with evidence, including any recent license change; stable, non-prerelease versions published at least two weeks ago)
- `docs/architecture/observability.md` (new), `docs/architecture/api-conventions.md` (only where the new behavior changes a statement), `docs/module-template/**`, `AGENTS.md` (Commands and Map)
- Architecture tests only if a rule must learn a new, approved location (propose first; never weaken a rule)

## Must NOT change
- ADRs, `Directory.Build.props`, `global.json`, `.github/*` (if the change needs a CI edit, stop and ask), `docs/agent-tasks/README.md`

## Deliverables
1. **Correlation ID** (ADR-007 owns the rule, ADR-006 names the header `X-Correlation-ID`).
   - Accept the incoming value only if it passes length and character validation, otherwise generate one. Propose the exact limits in your plan.
   - Return it in the response header and include it in every log entry of the request.
   - The outbox writes it into the `CorrelationId` column. The Worker restores it from the message into the log scope and into events published by handlers. With no ambient value it stays null.
   - How it travels in-process and where its abstraction lives is an open question below.
2. **Structured logging with redaction.** Passwords, tokens, message bodies and unnecessary personal data are never logged. Request bodies and query strings are never logged. Configure log levels so EF Core and Npgsql output cannot leak a password or connection string; show what they write when the database is down and when the password is wrong.
3. **Request-log policy enforced by code and test.** The query string must stay out of every log entry even if someone lowers the `Microsoft.AspNetCore` level through configuration or an environment variable. Propose the mechanism (see open questions). The test lowers the level through configuration and proves a sample secret in the query string appears in no captured entry at any level.
4. **Client abort and swallowed exceptions.**
   - A request cancelled by the client is not an Error: propose its log level and keep the response behavior defined in T-008a.
   - An exception swallowed after the response started must be visible to traces and metrics as an error (for example the activity status and an error tag, or a counter), without adding the exception message anywhere.
5. **OpenTelemetry traces and metrics** in both hosts with a configurable exporter (console by default, switchable off). Include the existing `TalepPazari.Outbox` source and meter; keep their names unless you propose a rename with a reason. No vendor SDK; the backend is not chosen (ADR-007 Open).
6. **Health.**
   - `/health/live` reports the process only and does not check dependencies.
   - `/health/ready` is database-aware through a check that the module registers itself (internal, registered in `Add<Name>Module`); readiness covers only the host's own critical dependencies (ADR-007).
   - Responses carry status only: no connection strings or exception text.
   - Worker health is **not implemented here** (open question below).
7. `docs/architecture/observability.md` (about one page), how-to additions (health check registration, correlation ID), Example `CONTEXT.md`, `AGENTS.md` Commands.

## Acceptance criteria
- [ ] The same correlation ID appears in the response header, a log line and the outbox row (test). A Worker handler logs it, and an event published by that handler carries it.
- [ ] An incoming correlation ID that is too long or has unexpected characters is replaced; a valid one is kept (tests for both).
- [ ] Redaction test: a sample password, token and message body do not appear in any captured log entry.
- [ ] Query-string test: a sample secret in the query string appears in no captured entry at any level, also when the framework log level is lowered by configuration.
- [ ] With the database down and with a wrong database password, captured logs contain no password and no connection string (evidence).
- [ ] A client-cancelled request is not logged at Error (test). An exception swallowed after the response started shows as an error in the telemetry, without the exception message (test).
- [ ] Readiness reports unhealthy when the database is unreachable and healthy when it runs: an automated test, plus a manual run against the local compose database stopped and started (show both results). Liveness stays 200 with the database down.
- [ ] Both hosts start with the database stopped (the Worker logs `Worker started`).
- [ ] A test listener proves the outbox counters and activity are emitted, and the exporter switch works (console by default). This closes the T-007 "no listener test" gap.
- [ ] Mutation evidence (temporary, reverted, failing output shown) for: the correlation validation, the query-string policy, the swallowed-exception signal and the readiness check.
- [ ] Architecture tests pass unchanged, or every change is listed and justified. Build has zero warnings, tests pass, `dotnet format TalepPazari.slnx --verify-no-changes` is clean (real exit codes).
- [ ] Tests that need no database run without Docker.
- [ ] The CI checks pass on the pull request. If `Directory.Packages.props` changed, state in the report whether the dependency review check listed the new packages (this tells us whether it reads NuGet versions under central package management).

## Verification
Run the CI steps from `AGENTS.md` in order. Show a sample request/response and the matching log line (fake data only), plus the health results. Say in "Not verified" what you did not check.

## Size rule
This brief is large. In your plan, list the expected number of changed files and propose a split into two commits or two tasks if the change would exceed about 60 files or mix unrelated risks (for example correlation and logging safety in one part, OpenTelemetry and health in the other). Wait for my decision on the split before editing.

## Out of scope
- Authentication and authorization, rate limiting, dashboards, alerting, a telemetry backend, a secret store
- Worker health mechanism (report options only), outbox/inbox retention, event ordering
- Changes to problem-details shape beyond adding a correlation member if you propose it and it is approved
- CI changes (`.github/*`)

## Decisions already made (do not reopen)
- Header name `X-Correlation-ID`; the correlation rule is owned by ADR-007.
- The logging rule applies project-wide: Warning and above never include exception messages, payloads, query strings or bodies; the full exception is Debug only.
- Hosts must stay startable with the database down.
- The correlation ID is not a secret and is never used for authorization.
- Only stable packages; no vendor-specific SDK.
- `ProblemJsonWriter` stays the single writer of problem details.

## Open questions to raise instead of deciding
- **Logging library.** Compare at least two (for example Serilog, the built-in `Microsoft.Extensions.Logging` with a JSON console formatter, NLog) on license (check for recent changes), maintenance and fit, including how each does redaction, scopes and configuration. Recommend one and wait.
- **Redaction mechanism** for the chosen option.
- **Request-log policy mechanism** (deliverable 3): for example a filter or provider wrapper added in code that cannot be lowered by configuration, disabling the framework's request logging in code, or replacing it with a minimal own log entry (method, route template, status, duration). Compare and recommend one.
- **How the correlation ID travels in-process, and where its abstraction lives.** SharedKernel holds interfaces and plain records only; an ambient holder is code. Options include an interface in SharedKernel with the implementation in the hosts, log scope or `Activity` baggage, or another place. Recommend one and wait.
- **Client abort log level** and how the abort and the swallowed exception appear in traces and metrics.
- **Worker health mechanism** (ADR-007 Open): compare a small HTTP listener, a heartbeat record and an orchestrator-level check. Recommend one but do not implement; it is decided together with the deployment target.
- **EF Core and Npgsql log-level policy**, and whether the problem-details body should carry a correlation member besides `traceId`.
- **OpenTelemetry package choices** (stable only).
- Anything in the Accepted ADRs or architecture tests that blocks a deliverable.

## Report
Fill `docs/templates/task-report.template.md` in the PR description. Do not include local paths, user names or machine details.