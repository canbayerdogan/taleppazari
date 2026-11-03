# T-008a — API conventions

- **Phase:** 1
- **Status:** todo
- **Depends on:** T-003, T-005, T-006, T-007 (done); ADR-006 (Accepted)
- **Approval:** required
- **Branch:** `task/T-008a-api-conventions`

## Goal
Every Api endpoint behaves the same way: versioned routes, RFC 9457 errors, an OpenAPI document guarded by a snapshot, cursor pagination and a fixed validation-message language. Observability (correlation ID, logging, telemetry, health) is T-008b.

## Context to read
1. `AGENTS.md`
2. `docs/adr/ADR-002-*.md`, `ADR-004-*.md`, `ADR-005-*.md`, `ADR-006-*.md`
3. `src/Hosts/TalepPazari.Api/**`, `src/Modules/Example/**` (including `CONTEXT.md`)
4. `docs/module-template/how-to-add-a-module.md`
5. `tests/**` (architecture tests and the integration-test fixture: read, do not weaken)

## Facts about the code today (verify, do not assume)
- The Api host maps a liveness check, calls `AddExampleModule` and `MapExampleEndpoints`. `MapExampleEndpoints` hardcodes `/api/v1/example` and returns `TypedResults.ValidationProblem` on validation failure.
- There is no global exception handler, no OpenAPI document and no list endpoint. Malformed or missing JSON bodies return the framework's default 400.
- FluentValidation messages follow the server's UI culture (one developer machine returned Turkish).
- Architecture tests (T-006): a module `Api` project may depend only on its own Application and Contracts and exposes exactly one public type, `<Name>Endpoints`; hosts use module types only from Infrastructure/Api assemblies; Application may use only its own Domain/Contracts, SharedKernel, FluentValidation and DI abstractions; every project under `src/` must match a known layout, so a new shared project needs an architecture decision.
- `tests/TalepPazari.IntegrationTests` exists (Testcontainers; skips when Docker is absent unless `TALEPPAZARI_REQUIRE_DOCKER=true`).
- `InternalsVisibleTo` is not allowed in production projects.

## Allowed to change
- `src/Hosts/TalepPazari.Api/**`
- `src/Modules/Example/**` (Api, Application, Infrastructure: the list endpoint and its query; no migration is expected)
- `src/Shared/**` only if approved in your plan (cross-cutting; interfaces and plain records only)
- `tests/**` (new API test project or an extension of an existing one), `TalepPazari.slnx`
- `Directory.Packages.props` (each new package needs a license and maintenance check with evidence; stable, non-prerelease versions published at least two weeks ago)
- `docs/architecture/api-conventions.md` (new), `docs/README.md` (index row only), `docs/module-template/**`, `AGENTS.md` (Commands and Map)
- Architecture tests only if a rule must learn a new, approved location (propose it in your plan; never weaken a rule to make code pass)

## Must NOT change
- ADRs, `Directory.Build.props`, `global.json`, `.github/*`, `docs/agent-tasks/README.md`
- The Worker host (observability is T-008b)

## Deliverables
1. **Versioning.** `/api/v1` is defined in one place in the Api host; modules add only their own prefix. Propose the mechanism (for example a host-owned route group passed to `Map<Name>Endpoints`). Update the how-to and the Example module.
2. **Problem details (RFC 9457) for every non-success response:** a global exception handler plus validation errors, malformed or missing JSON, unsupported media type, unknown route and method not allowed. One consistent shape. Problem details never contain stack traces, internal type names or exception messages. An unhandled exception is logged with the exception type and the route template only (never query string or body); the full exception is logged at Debug only (same rule as T-007b). Final logging setup is T-008b.
3. **Validation message language.** Messages do not depend on the host's culture. Propose the mechanism.
4. **OpenAPI.** One document for the Api host, generated, deterministic (no server URLs, timestamps or machine data). The document endpoint is mapped in Development only for now. The snapshot test must not depend on that endpoint being exposed. Document how a deliberate snapshot update is made (never automatic in CI).
5. **Cursor pagination convention with a reference implementation:** `GET /api/v1/example/items`.
   - Opaque cursor, ordering by `Id` (UUID v7, ADR-004), a documented default and maximum page size, an invalid cursor returns a 400 problem details, stable when items are inserted between pages.
   - Query handler in Application, query in Infrastructure, endpoint anonymous on purpose (no Identity yet; comment as in the existing endpoint).
6. `docs/architecture/api-conventions.md` (about one page), how-to updates (route convention, error shape, pagination), Example `CONTEXT.md`, `AGENTS.md` Commands.

## Acceptance criteria
- [ ] A forced exception returns a problem-details body without stack trace, internal names or exception message (the failing endpoint exists only in the test host, not in production code).
- [ ] Malformed JSON, missing body, wrong content type and an unknown route return `application/problem+json` in the same shape.
- [ ] Validation messages are identical under a Turkish and the invariant culture (test).
- [ ] The OpenAPI document is generated, and a snapshot test guards it. A deliberate endpoint change fails it (evidence, then revert).
- [ ] Pagination tests: first page, next page through the cursor, last page without a next cursor, invalid cursor, over-maximum limit, and an item inserted between pages causes no duplicate or skipped item.
- [ ] The `/api/v1` literal appears once in the Api host.
- [ ] Architecture tests pass unchanged, or every change is listed and justified. `dotnet build TalepPazari.slnx` has zero warnings, `dotnet test TalepPazari.slnx` passes, `dotnet format TalepPazari.slnx --verify-no-changes` is clean.
- [ ] Tests that need no database run without Docker.
- [ ] No migration and no schema change.

## Verification
Run build, tests and format check. Show sample request/response pairs (`curl.exe`, fake data only) for 201, a validation 400, a malformed-JSON 400, a 404 and a page with a cursor. Say in "Not verified" what you did not check.

## Out of scope
- Authentication and authorization (Phase 2), rate limiting (Phase 4), idempotency keys (ADR-006: no retried unsafe operation exists yet)
- Correlation ID, logging library, telemetry, health endpoints, Worker changes (T-008b)

## Decisions already made (do not reopen)
- URL path versioning, RFC 9457, cursor pagination (ADR-006).
- Problem details never expose stack traces, internal names or exception messages.
- Validation messages are not localized for now (fixed language); localization is a later product decision.
- The OpenAPI endpoint is not exposed outside Development until the deployment target is chosen.
- No `InternalsVisibleTo`. A public partial `Program` is acceptable if the test host needs it (no architecture rule covers host visibility).
- Only stable (non-prerelease) packages.

## Open questions to raise instead of deciding
- **OpenAPI generator.** Compare the built-in ASP.NET Core support with at least one alternative on license, maintenance and fit; recommend one and wait.
- **Where do shared API pieces live** (cursor encoding, problem-details helpers)? Module `Api` projects cannot reference the host and expose only `<Name>Endpoints`. Options include host-only code, SharedKernel (plain records), copies in the Example module until a second module needs them, or a new shared project (needs an ADR-002 decision and an architecture-test change). Recommend one and wait.
- **Test host and project location.** How the Api is started in tests (for example `WebApplicationFactory`, with a license and maintenance check) and whether tests go into a new project or an existing one. Recommend one and wait.
- **Fixed-culture mechanism** and the page-size numbers.
- Anything in the Accepted ADRs or architecture tests that blocks a deliverable.

## Report
Fill `docs/templates/task-report.template.md` in the PR description. Do not include local paths, user names or machine details.
