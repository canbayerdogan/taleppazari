# How to add a module

A checklist for adding a module under `src/Modules/<Name>/`. It follows [ADR-002](../adr/ADR-002-module-boundaries-and-layout.md), [ADR-003](../adr/ADR-003-inter-module-communication.md) and [ADR-004](../adr/ADR-004-persistence-and-code-standards.md). The `Example` module (`src/Modules/Example/`) is the reference: copy its shape, not its content.

Replace `<Name>` with the module name in PascalCase (for example `Identity`) and `<schema>` with its schema name in lower case (for example `identity`). Run every command from the repository root.

## 0. One-time setup (per machine)
1. Start the local database: see README "Local development" (`docker compose up -d`, wait for `healthy`).
2. Restore the pinned EF Core tool: `dotnet tool restore`.
3. Set the connection string once. Both hosts share one `UserSecretsId`, so this one command configures the Api and the Worker:

   ```powershell
   dotnet user-secrets set "ConnectionStrings:Default" "Host=127.0.0.1;Port=<POSTGRES_PORT>;Database=<POSTGRES_DB>;Username=<POSTGRES_USER>;Password=<POSTGRES_PASSWORD>" --project src/Hosts/TalepPazari.Api
   ```

   Use the values from your own `.env`. Use `127.0.0.1`, not `localhost`: the database port is published on IPv4 loopback only, and `localhost` may resolve to IPv6 first and time out. Alternatively set the environment variable `ConnectionStrings__Default`. Never put the connection string in `appsettings*.json` or in the repository.

## 1. Create the projects
Folder layout (one folder per project, project name = folder name = root namespace):

```
src/Modules/<Name>/
  CONTEXT.md
  TalepPazari.Modules.<Name>.Contracts/        integration events, DTOs, facade interfaces
  TalepPazari.Modules.<Name>.Domain/           entities, value objects, invariants
  TalepPazari.Modules.<Name>.Application/      use cases, validators, persistence interfaces
  TalepPazari.Modules.<Name>.Infrastructure/   DbContext, configurations, migrations, Add<Name>Module
  TalepPazari.Modules.<Name>.Api/              Map<Name>Endpoints, HTTP request/response records
```

- [ ] Create the five `.csproj` files. Target framework, nullable, implicit usings and analyzers come from `Directory.Build.props`; package versions come from `Directory.Packages.props` (no `Version` on `PackageReference`). Start from the same minimal content as `src/Shared/TalepPazari.SharedKernel/TalepPazari.SharedKernel.csproj`, then add the references listed below:

  ```xml
  <Project Sdk="Microsoft.NET.Sdk">

  </Project>
  ```

  The Api project also needs the ASP.NET Core framework reference:

  ```xml
  <Project Sdk="Microsoft.NET.Sdk">

    <ItemGroup>
      <FrameworkReference Include="Microsoft.AspNetCore.App" />
    </ItemGroup>

  </Project>
  ```

  Do not use `dotnet new classlib` output as is. If you use it, delete `Class1.cs` and the `TargetFramework`, `Nullable` and `ImplicitUsings` lines that `Directory.Build.props` already provides.
- [ ] Project references (copy from Example):
  - Contracts: none
  - Domain: none
  - Application → Domain, Contracts, `src/Shared/TalepPazari.SharedKernel` (unit of work, outbox and handler interfaces); packages `FluentValidation`, `Microsoft.Extensions.DependencyInjection.Abstractions`
  - Infrastructure → Application; packages `Microsoft.EntityFrameworkCore.Relational`, `Npgsql.EntityFrameworkCore.PostgreSQL`
  - Api → Application; `<FrameworkReference Include="Microsoft.AspNetCore.App" />`
  - Another module's project: **only** its `Contracts` project.
- [ ] Add the five projects to `TalepPazari.slnx` under a `/Modules/<Name>/` folder.
- [ ] Create `src/Modules/<Name>/CONTEXT.md` from `docs/templates/CONTEXT.template.md` (Example's `CONTEXT.md` shows a filled one).

## 2. Domain
- [ ] Entities have private setters, a static factory (`Create(...)`) and check their invariants there. No EF Core, ASP.NET Core or Data Annotations references.
- [ ] Ids are `Guid` passed in from the application (UUID v7). Times are `DateTimeOffset` in UTC.
- [ ] How invariants fail (Example throws `ArgumentException`) is an example-level choice. A shared Result and error model are decided when a second module needs them.

## 3. Application
- [ ] One folder per use case: command record, `AbstractValidator<T>`, handler class, result type. No mediator library.
- [ ] The handler validates first, generates the id with `Guid.CreateVersion7(timeProvider.GetUtcNow())`, takes time from `TimeProvider`, calls the domain, then stages changes through an interface defined in Application (for example `I<Entity>Repository.Add`, which does not save).
- [ ] The handler commits with the module's unit of work: stage the entity, stage any integration event with `outbox.Add(...)`, then `await unitOfWork.SaveChangesAsync(...)`. Both commit in one transaction, so the event exists if and only if the state change committed. Inject them keyed with the module's key: `[FromKeyedServices(<Name>ApplicationRegistration.ServiceKey)] IOutbox outbox` and `... IUnitOfWork unitOfWork` (every module implements the same SharedKernel interfaces).
- [ ] `Add<Name>Application(this IServiceCollection)` registers validators and handlers, and declares `public const string ServiceKey = "<Name>";`. Integration event handlers are registered as their concrete type only; Infrastructure exposes them behind the inbox decorator (step 8).
- [ ] Visibility: types that another project of the same module uses (handler, command, result, the registration method, repository interface) are `public`. Validators are `internal`: they are registered explicitly in `Add<Name>Application` and only the handler uses them, through `IValidator<T>`. No `InternalsVisibleTo`. Boundaries between modules are enforced by project references.
- [ ] Authorization: the Example endpoint is anonymous on purpose because Identity does not exist yet. Real endpoints must authorize per resource in the Application layer (AGENTS.md hard rule); the mechanism arrives with Identity in Phase 2.

## 4. Infrastructure
- [ ] `internal sealed class <Name>DbContext` with `modelBuilder.HasDefaultSchema(Schema)` and `ApplyConfiguration(...)` for each `IEntityTypeConfiguration<T>` (Fluent API only, `internal`).
- [ ] Declare the schema constant on the DbContext: `internal const string Schema = "<schema>";`. The migrations history table setting below uses it.
- [ ] Repository implementations are `internal`.
- [ ] `public static class <Name>Module` with `Add<Name>Module(this IServiceCollection, IConfiguration)`.
- [ ] `Add<Name>Module` calls `services.Add<Name>Application();` (hosts call only `Add<Name>Module`).
- [ ] `Add<Name>Module` registers the clock with `services.TryAddSingleton(TimeProvider.System);` (`TryAdd` so several modules can register it).
- [ ] Copy `ExampleModule` for the DbContext registration: read `ConnectionStrings:Default` **inside** the `AddDbContext` callback (so hosts start without a database) and set the history table to the module schema:

  ```csharp
  options.UseNpgsql(connectionString, npgsql =>
      npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, <Name>DbContext.Schema));
  ```

- [ ] Table and column names use default EF Core naming for now; in `psql` they need double quotes (`<schema>."Items"`).

## 5. Api
- [ ] `public static class <Name>Endpoints` with `Map<Name>Endpoints(this IEndpointRouteBuilder)`. Route prefix `/api/v1/<schema>` (T-008 owns the final convention).
- [ ] Request and response records (for example `Create<Entity>Request`, `<Entity>Response`) live in the Api project as `internal sealed record`s. Application types are not the HTTP contract.
- [ ] Endpoints only translate HTTP to the use case and back: map the request to the command, then success → `TypedResults.Created(...)` / `Ok(...)` with the response record, validation failure → `TypedResults.ValidationProblem(errors)`. No business rules.

## 6. Register in the hosts
- [ ] `src/Hosts/TalepPazari.Api`: reference `<Name>.Infrastructure` and `<Name>.Api`; in `Program.cs` call `builder.Services.Add<Name>Module(builder.Configuration);` and `app.Map<Name>Endpoints();`.
- [ ] `src/Hosts/TalepPazari.Worker`: reference `<Name>.Infrastructure`; call `builder.Services.Add<Name>Module(builder.Configuration);`.
- [ ] Hosts reference module `Infrastructure` and `Api` projects for composition only. No `IModule` interface; one explicit call per module.

## 7. Create and apply a migration
```powershell
dotnet build TalepPazari.slnx
dotnet ef migrations add <MigrationName> --project src/Modules/<Name>/TalepPazari.Modules.<Name>.Infrastructure --startup-project src/Hosts/TalepPazari.Api --context <Name>DbContext --output-dir Persistence/Migrations
dotnet ef database update --project src/Modules/<Name>/TalepPazari.Modules.<Name>.Infrastructure --startup-project src/Hosts/TalepPazari.Api --context <Name>DbContext
```

- [ ] The Api host is the startup project (it carries `Microsoft.EntityFrameworkCore.Design` with `PrivateAssets="all"`). The connection string from step 0 must be set, even for `migrations add`.
- [ ] Keep migrations in `Persistence/Migrations`. Migration files are tool-generated and covered by the scoped `.editorconfig` section for `src/Modules/**/Persistence/Migrations/*.cs`; do not hand-edit them for style.
- [ ] Review the generated migration: only schema `<schema>`, no foreign keys to other schemas.
- [ ] Migrations are applied by this explicit command only, never at startup.
- [ ] `<Name>Module.MigrateDatabaseAsync(IServiceProvider, CancellationToken)` (copy from `ExampleModule`) is for integration tests and tooling only. Hosts never call it. (No architecture test checks this yet; one could be added later.)
- [ ] Check: `"\dt <schema>.*" | docker compose exec -T postgres sh -c 'psql -U $POSTGRES_USER -d $POSTGRES_DB'` lists your tables and `__EFMigrationsHistory`.

## 8. Facades and integration events
Integration events use a per-module outbox and inbox (ADR-003). The Worker host's dispatcher claims messages from every module, delivers them in-process and records the result; Example shows every piece.

- [ ] **Integration events** this module publishes: `Contracts/IntegrationEvents/<Event>V1.cs`, a `public sealed record` named `<Name>V<N>` (enforced by the architecture tests). The outbox stores the event under its full type name, so renaming or moving a published event breaks messages that are still pending: add a new version instead.
- [ ] **Outbox and inbox tables** in the module schema (copy `Messaging/OutboxEntry*` and `Messaging/InboxEntry*` from Example, then `dotnet ef migrations add`): `<schema>."OutboxMessages"` with the partial index on `NextAttemptAt`, and `<schema>."InboxMessages"` keyed by (MessageId, HandlerName).
- [ ] **Infrastructure pieces**, all `internal`, registered in `Add<Name>Module` (copy from `ExampleModule`):
  - `<Name>UnitOfWork : IUnitOfWork` and `<Name>Outbox : IOutbox`, keyed with `<Name>ApplicationRegistration.ServiceKey`.
  - `<Name>IntegrationEventTypes : IIntegrationEventTypes` listing every event the module publishes (singleton). The outbox refuses unlisted types; the dispatcher dead-letters stored names no registry knows.
  - `<Name>OutboxStore : IOutboxStore` (scoped). Every time value is a parameter from the dispatcher's `TimeProvider`; never use the database `now()`. Every `Mark*` statement must require the claim token **and** `"ProcessedAt" IS NULL AND "DeadLetteredAt" IS NULL`, so a finished row is never changed again (see `ExampleOutboxStore`).
  - `InboxIntegrationEventHandler<TEvent>`: every `IIntegrationEventHandler<T>` the module consumes is registered **only** through it (`AddInboxHandler<TEvent, THandler>`). The inbox key is the handler's full type name including namespace, so renaming a handler class makes already-handled messages look new. The decorator refuses a message without an id (an unset event context), and its insert names the key: `ON CONFLICT ("MessageId", "HandlerName") DO NOTHING`.
- [ ] **Consuming** another module's event: reference its `Contracts` project, implement `IIntegrationEventHandler<TheirEventV1>` in your Application, and register it behind **your** module's inbox decorator. The inbox row and your effect commit in one transaction in your schema.
- [ ] **Delivery is at-least-once.** A message is retried as a whole when any handler fails (handlers that already succeeded are skipped by the inbox); at `Outbox:MaxAttempts` it is dead-lettered for all handlers. Event ordering and outbox/inbox retention are not decided (ADR-003 Open): do not rely on either.
- [ ] **Lease:** the Worker claims `Outbox:BatchSize` messages per module under one lease of `Outbox:LeaseDuration` and handles them one after another. Keep `BatchSize × worst-case handler time` below `LeaseDuration`, or another Worker may deliver the same messages again (harmless because of the inbox, but wasted work).
- [ ] **Errors and logs carry no data.** An exception message may contain event or personal data, so `LastError` stores only the exception type name (other dead-letter reasons are fixed text plus type names). Warning and Error log entries carry the exception type, the message id or module name and a fixed hint, never the exception message or the exception object. The full exception is logged at Debug only; to see it, set the Worker's `Logging:LogLevel` entry for the category `TalepPazari.Worker.Outbox` to `Debug` (for example `"TalepPazari.Worker.Outbox": "Debug"` under `Logging:LogLevel` in a local configuration file or user secrets).
- [ ] **Known limits** (accepted for now, to revisit with the deployment target): a shutdown or a failed result write in the middle of a batch leaves the remaining claimed messages with their incremented `Attempts` and their lease until it expires. Frequent interruptions (restarts, database blips) can therefore dead-letter a healthy message at `Outbox:MaxAttempts`.
- [ ] **Facades** (synchronous calls from other modules, preferably read-only queries): the interface and its DTOs go in `Contracts` (for example `Contracts/I<Name>Queries.cs`); the implementation is `internal` in Infrastructure or Application and registered in `Add<Name>Module`. Add one only when a consumer exists.
- [ ] Consuming another module: reference only its `Contracts` project.

## 9. Verify
- [ ] `dotnet build TalepPazari.slnx`: zero warnings.
- [ ] `dotnet run --project src/Hosts/TalepPazari.Api`, then call the endpoint with `curl.exe`. If your environment uses an HTTP proxy, add `--noproxy localhost` to `curl.exe`.
- [ ] With the database stopped (`docker compose stop`), `dotnet run --project src/Hosts/TalepPazari.Worker` still logs `Worker started`, then logs failed outbox passes and backs off without stopping.
- [ ] Integration tests for the module's outbox and handlers go in `tests/TalepPazari.IntegrationTests` (see AGENTS.md "Commands").
- [ ] Update `CONTEXT.md` and the generated maps if present.
