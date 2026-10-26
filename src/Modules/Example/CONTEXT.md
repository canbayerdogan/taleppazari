# Example — module context

> Reference module only. It shows the module layout from ADR-002 and is removed when the first real module (Identity) exists. Do not build features on it.

## Purpose
Show, with the smallest possible code, how a module is laid out, registered, persisted and exposed over HTTP.

## Owns
- `ExampleItem`: an id, a name and a creation time. Nothing else.

## Does NOT own
- Anything real. No other module may depend on it.

## Public surface (the only thing other modules may use)
- Contracts project: `TalepPazari.Modules.Example.Contracts`
- Query/command facades: none (no consumer exists; see the how-to for where one goes)
- Integration events published (name, version): `ExampleItemCreatedV1` (defined only; publishing arrives with the outbox in T-007)
- Integration events consumed (name, from module): none
- HTTP endpoints (route prefix): `/api/v1/example` (`POST /api/v1/example/items`)

## Allowed dependencies
- Modules (Contracts only): none
- External: EF Core with the Npgsql provider (Infrastructure), FluentValidation (Application), ASP.NET Core (Api project only)

## Data
- Schema name: `example`
- Key tables/entities: `example."ExampleItems"`, migrations history in `example."__EFMigrationsHistory"`

## Invariants and rules
- Name is required, not blank, at most 200 characters: FluentValidation at the boundary (400), `ExampleItem.Create` in the domain, `varchar(200) NOT NULL` plus a not-blank check constraint in the database.
- Id is a non-empty UUID v7 generated in the application; `CreatedAt` is UTC from `TimeProvider`.

## Key flows
- Create item: endpoint → `CreateExampleItemHandler` (validate, create entity, save) → 201 with the item, or 400 `ValidationProblem`.

## Related ADRs and tasks
- ADR-002, ADR-003, ADR-004; T-005 (this module), T-006 (architecture tests), T-007 (outbox), T-008 (API conventions)

## Gotchas for agents
- Example-level choices, not architecture decisions: domain invariants throw `ArgumentException`, and `CreateExampleItemResult` is a module-local result type. A shared Result and error model are decided when a second module needs them.
- Visibility: Application types used by another project of this module (handler, command, result, `AddExampleApplication`) are public. Infrastructure types are internal except `ExampleModule`. Boundaries between modules are enforced by project references, not by visibility. No `InternalsVisibleTo`.
- Table and column names use default EF Core naming (PascalCase), so SQL needs quoted identifiers: `example."ExampleItems"`, `"Name"`. A naming convention is not decided yet.
- The connection string is resolved when a DbContext is first created, not at startup; a host starts without a database.
- Migration files are tool-generated; do not hand-edit them for style (a scoped `.editorconfig` section covers them).

## Last verified
- Commit: <filled after merge> — Date: 2026-10-26
