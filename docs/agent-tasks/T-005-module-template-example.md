# T-005 — Module template and Example module

- **Phase:** 1
- **Status:** done
- **Depends on:** T-003, T-004 (ADR-002, ADR-003, ADR-004 are Accepted)
- **Approval:** required
- **Branch:** `task/T-005-module-template`

## Goal
Prove the module layout with one tiny `Example` module and document how to add a real module. The Example module is a reference and is removed when the first real module (Identity) exists.

## Context to read
1. `AGENTS.md`
2. `docs/adr/ADR-002-*.md`, `ADR-003-*.md`, `ADR-004-*.md` (Accepted)
3. `docs/templates/CONTEXT.template.md`
4. `src/Hosts/*`, `src/Shared/*`, `compose.yaml`, `README.md` (Local development)

ADR-005..007 are still Proposed. The configuration rule under "Decisions already made" is stated here so this task does not depend on their acceptance.

## Allowed to change
- `src/Modules/Example/**`
- `src/Shared/TalepPazari.SharedKernel/**` (minimal abstractions only; report each addition and why)
- `src/Hosts/**`: module registration and composition code, project references, `UserSecretsId`
- `TalepPazari.slnx` (add the Example projects)
- `Directory.Packages.props` (new packages; each needs a license and maintenance check in your plan)
- `.config/dotnet-tools.json` (new; pinned `dotnet-ef`)
- `docs/module-template/**`
- `README.md` (Local development section only: how to point the hosts at the local database)
- `AGENTS.md` (Commands and Map sections)

## Must NOT change
- ADRs, other docs, `.github/*`, `docs/agent-tasks/README.md`
- `compose.yaml`, `.env.example`, `Directory.Build.props`
- Do not create a `tests/` directory or any test project (T-006 creates the layout)

## Deliverables
- Five projects under `src/Modules/Example/` per ADR-002, named `TalepPazari.Modules.Example.Contracts`, `.Domain`, `.Application`, `.Infrastructure`, `.Api`. Propose the exact folder layout in your plan.
- **Domain:** one entity `ExampleItem` with private setters, a factory method and its invariants inside the entity. Guid v7 (`Guid.CreateVersion7()`) generated in the application layer; `DateTimeOffset` UTC via `TimeProvider`. No EF Core reference.
- **Application:** plain use-case classes (no mediator library) and one FluentValidation validator at the boundary.
- **Infrastructure:** own `ExampleDbContext`, schema `example`, Fluent `IEntityTypeConfiguration<T>`, migrations history table in schema `example` (`example.__EFMigrationsHistory`), one migration, and `AddExampleModule(this IServiceCollection, IConfiguration)`.
- **Api:** `MapExampleEndpoints(this IEndpointRouteBuilder)` with one endpoint, `POST /api/v1/example/items` (201 with body; validation failure returns 400 through the built-in `Results.ValidationProblem`). No business rules in the endpoint.
- **Contracts:** one integration event record, versioned in its name. It is only defined, not published (outbox is T-007). No facade interface (no consumer exists yet; the how-to explains where one goes).
- **Hosts:** Api calls `AddExampleModule` and `MapExampleEndpoints`; Worker calls `AddExampleModule` only. No `IModule`-style interface in SharedKernel.
- `src/Modules/Example/CONTEXT.md` filled from the template.
- `docs/module-template/how-to-add-a-module.md`: a checklist someone can follow in under 30 minutes. It includes the connection setup, creating and applying a migration, and where facades and events go.
- `AGENTS.md` Commands: tool restore, migration apply, and connection-string setup commands.

## Acceptance criteria
- [ ] With the local database from T-004 running and the connection string configured, the endpoint creates a row. The table exists in schema `example`, and `example.__EFMigrationsHistory` exists.
- [ ] Only `Contracts` is referenced across module boundaries (nothing else exists to reference yet; show the project reference graph).
- [ ] No Data Annotations anywhere in the module; Domain has no EF Core or ASP.NET Core reference.
- [ ] `dotnet build TalepPazari.slnx` has zero warnings (warnings are errors, including NuGet audit).
- [ ] The Worker still starts and logs `Worker started` when the database is NOT running (DbContext registration must not connect at startup).
- [ ] No secret, password or connection string is committed. Placeholders only in docs.
- [ ] Following the checklist, a second dummy module can be created without extra decisions. Describe your dry run. The dummy module is not committed.

## Verification
Run and paste results: `dotnet tool restore`, build, set the connection string, apply the migration, run the Api, `curl.exe` POST to the endpoint, and check the table with the PostGIS-style piped `psql` command from `AGENTS.md`. Also start the Worker with the database stopped. No automated tests exist yet; say so in "Not verified".

## Out of scope
- Outbox/Inbox and event publishing (T-007), architecture tests and any test project (T-006), API/observability conventions, final error format, readiness checks (T-008).
- A deployment-time migration mechanism and CI wiring (T-009).

## Decisions already made (do not reopen)
- Configuration: `ConnectionStrings:Default`, one key for all modules for now. Development default is `dotnet user-secrets` (per host project); the same key can be supplied as the environment variable `ConnectionStrings__Default`. No secrets in the repository, no `.env`-reading package, no connection string in appsettings files.
- Migrations: `dotnet-ef` as a pinned local tool. They are applied by an explicit command and never automatically at startup in any environment for now.
- No mediator library, plain use-case classes. FluentValidation is included, subject to your license and maintenance check.
- Route literal `/api/v1/example/...`; T-008 owns the final convention and may move it.
- No SharedKernel module interface. Registration is explicit extension methods per module.
- `TimeProvider` is the clock; do not add a custom clock abstraction.

## Open questions to raise instead of deciding
- Naming convention for integration event records (suggest `...V1`); T-006 will enforce the final rule.
- `dotnet ef` startup project versus a design-time DbContext factory: recommend one.
- Package choices and exact versions (EF Core, Npgsql provider, FluentValidation, Design package): licenses and maintenance status.
- Anything in the Accepted ADRs that blocks implementation.

## Report
Fill `docs/templates/task-report.template.md` in the PR description. Do not include local paths, user names or machine details.
