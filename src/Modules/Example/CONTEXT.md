# Example — module context

> Reference module only. It shows the module layout from ADR-002 and is removed when the first real module (Identity) exists. Do not build features on it.

## Purpose
Show, with the smallest possible code, how a module is laid out, registered, persisted and exposed over HTTP.

## Owns
- `ExampleItem`: an id, a name and a creation time. Nothing else.
- `ExampleItemCreatedReceipt`: **demo effect only.** Written by the second handler when it consumes `ExampleItemCreatedV1`, so tests can count deliveries. It has no business meaning.
- The module's outbox and inbox tables (T-007).

## Does NOT own
- Anything real. No other module may depend on it.

## Public surface (the only thing other modules may use)
- Contracts project: `TalepPazari.Modules.Example.Contracts`
- Query/command facades: none (no consumer exists; see the how-to for where one goes)
- Integration events published (name, version): `ExampleItemCreatedV1`, through the outbox, in the same transaction as the item
- Integration events consumed (name, from module): `ExampleItemCreatedV1` from Example itself (demo only: `RecordExampleItemCreatedReceiptHandler`)
- HTTP endpoints (route prefix): `/api/v1/example` (`POST /api/v1/example/items`)

## Allowed dependencies
- Modules (Contracts only): none
- External: EF Core with the Npgsql provider (Infrastructure), FluentValidation (Application), ASP.NET Core (Api project only)

## Data
- Schema name: `example`
- Key tables/entities: `example."ExampleItems"`, `example."ExampleItemCreatedReceipts"` (demo effect only), `example."OutboxMessages"`, `example."InboxMessages"`, migrations history in `example."__EFMigrationsHistory"`
- Outbox and inbox retention (cleanup of processed rows) is not decided (ADR-003 Open); rows are kept.

## Invariants and rules
- Name is required, not blank, at most 200 characters: FluentValidation at the boundary (400), `ExampleItem.Create` in the domain, `varchar(200) NOT NULL` plus a check constraint in the database.
- The validator and the domain reject any whitespace-only name. The database check constraint (`btrim("Name") <> ''`) is only a backstop: it trims spaces only.
- Length: the application counts UTF-16 code units, `varchar(200)` counts characters, so the application is stricter.
- Authorization: the endpoint is anonymous on purpose because Identity does not exist yet. Real endpoints must authorize per resource in the Application layer (AGENTS.md hard rule); the mechanism arrives with Identity in Phase 2.
- Id is a non-empty UUID v7 generated in the application; `CreatedAt` is UTC from `TimeProvider`.

## Key flows
- Create item: `CreateExampleItemRequest` → endpoint maps it to `CreateExampleItemCommand` → `CreateExampleItemHandler` (validate, create entity, stage it, stage `ExampleItemCreatedV1` in the outbox, commit both with one `SaveChanges`) → endpoint maps the result to `ExampleItemResponse` → 201, or 400 `ValidationProblem`.
- Deliver the event (Worker host): the outbox dispatcher claims the row through `ExampleOutboxStore` → `InboxIntegrationEventHandler<ExampleItemCreatedV1>` inserts the inbox row, runs `RecordExampleItemCreatedReceiptHandler` and commits both → the dispatcher marks the row processed. On failure it retries with back-off and dead-letters at `Outbox:MaxAttempts`.

## Related ADRs and tasks
- ADR-002, ADR-003, ADR-004; T-005 (this module), T-006 (architecture tests), T-007 (outbox), T-008 (API conventions)

## Gotchas for agents
- Example-level choices, not architecture decisions: domain invariants throw `ArgumentException`, and `CreateExampleItemResult` is a module-local result type. A shared Result and error model are decided when a second module needs them.
- The HTTP contract (`CreateExampleItemRequest`, `ExampleItemResponse`) lives in the Api project and is internal; Application types are not exposed over HTTP.
- Visibility: Application types used by another project of this module (handler, command, result, `AddExampleApplication`) are public; the validator is internal (registered explicitly in `AddExampleApplication`). Infrastructure types are internal except `ExampleModule`. Boundaries between modules are enforced by project references, not by visibility. No `InternalsVisibleTo`.
- Table and column names use default EF Core naming (PascalCase), so SQL needs quoted identifiers: `example."ExampleItems"`, `"Name"`. A naming convention is not decided yet.
- The connection string is resolved when a DbContext is first created, not at startup; a host starts without a database.
- `IUnitOfWork` and `IOutbox` are keyed services (key `ExampleApplicationRegistration.ServiceKey`), because every module implements the same SharedKernel interfaces.
- Every `IIntegrationEventHandler<T>` is registered only through the inbox decorator (`AddInboxHandler` in `ExampleModule`); an integration test checks this. The inbox key is the handler's full type name, so renaming the handler class makes handled messages look new.
- `ExampleModule.MigrateDatabaseAsync` is for integration tests and tooling only. Hosts never call it.
- Migration files are tool-generated; do not hand-edit them for style (a scoped `.editorconfig` section covers them).

## Last verified
- Verified against main at 3e8a598 (T-005 branch point), 2026-10-26
- Outbox/inbox sections verified on the T-007 branch (based on c632934), 2026-11-02
