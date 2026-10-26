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
  TalepPazari.Modules.<Name>.Api/              Map<Name>Endpoints
```

- [ ] Create the five `.csproj` files with `<Project Sdk="Microsoft.NET.Sdk">`. Target framework, nullable and analyzers come from `Directory.Build.props`; package versions come from `Directory.Packages.props` (no `Version` on `PackageReference`).
- [ ] Project references (copy from Example):
  - Contracts: none
  - Domain: none
  - Application → Domain, Contracts; packages `FluentValidation`, `Microsoft.Extensions.DependencyInjection.Abstractions`
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
- [ ] The handler validates first, generates the id with `Guid.CreateVersion7(timeProvider.GetUtcNow())`, takes time from `TimeProvider`, calls the domain, then persists through an interface defined in Application (for example `I<Entity>Repository`).
- [ ] `Add<Name>Application(this IServiceCollection)` registers validators and handlers.
- [ ] Visibility: types that another project of the same module uses (handler, command, result, the registration method, repository interface) are `public`. No `InternalsVisibleTo`. Boundaries between modules are enforced by project references.

## 4. Infrastructure
- [ ] `internal sealed class <Name>DbContext` with `modelBuilder.HasDefaultSchema("<schema>")` and `ApplyConfiguration(...)` for each `IEntityTypeConfiguration<T>` (Fluent API only, `internal`).
- [ ] Repository implementations are `internal`.
- [ ] `public static class <Name>Module` with `Add<Name>Module(this IServiceCollection, IConfiguration)`. Copy `ExampleModule`: read `ConnectionStrings:Default` **inside** the `AddDbContext` callback (so hosts start without a database) and set the history table to the module schema:

  ```csharp
  options.UseNpgsql(connectionString, npgsql =>
      npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, <Name>DbContext.Schema));
  ```

- [ ] Table and column names use default EF Core naming for now; in `psql` they need double quotes (`<schema>."Items"`).

## 5. Api
- [ ] `public static class <Name>Endpoints` with `Map<Name>Endpoints(this IEndpointRouteBuilder)`. Route prefix `/api/v1/<schema>` (T-008 owns the final convention).
- [ ] Endpoints only translate HTTP to the use case and back: success → `TypedResults.Created(...)` / `Ok(...)`, validation failure → `TypedResults.ValidationProblem(errors)`. No business rules.

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
- [ ] Check: `"\dt <schema>.*" | docker compose exec -T postgres sh -c 'psql -U $POSTGRES_USER -d $POSTGRES_DB'` lists your tables and `__EFMigrationsHistory`.

## 8. Facades and integration events
- [ ] **Integration events** this module publishes: `Contracts/IntegrationEvents/<Event>V1.cs`, a `public sealed record` with a version suffix (final naming rule: T-006). Publishing goes through the outbox (T-007).
- [ ] **Facades** (synchronous calls from other modules, preferably read-only queries): the interface and its DTOs go in `Contracts` (for example `Contracts/I<Name>Queries.cs`); the implementation is `internal` in Infrastructure or Application and registered in `Add<Name>Module`. Add one only when a consumer exists.
- [ ] Consuming another module: reference only its `Contracts` project.

## 9. Verify
- [ ] `dotnet build TalepPazari.slnx`: zero warnings.
- [ ] `dotnet run --project src/Hosts/TalepPazari.Api`, then call the endpoint (`curl.exe` on Windows; add `--noproxy localhost` behind a proxy).
- [ ] With the database stopped (`docker compose stop`), `dotnet run --project src/Hosts/TalepPazari.Worker` still logs `Worker started`.
- [ ] Update `CONTEXT.md` and the generated maps if present.
