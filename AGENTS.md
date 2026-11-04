# AGENTS.md — Instructions for AI coding agents

Read this file fully, then `SUMMARY.md`. Keep this file short; details live in `docs/`.

## Language and conventions
- Code, comments, docs, commits and PRs are in English.
- Conventional Commits. Branch name: `task/T-XXX-short-slug`. One task = one branch = one PR.
- Root namespace: `TalepPazari`.

## Source of truth (highest first)
1. Code and passing tests
2. Accepted ADRs (`docs/adr/`)
3. Contracts (module `*.Contracts` projects, `contracts/`)
4. Module `CONTEXT.md` files
5. `SUMMARY.md`, `docs/`
6. Generated maps (`docs/generated/`): indexes only; verify against code when in doubt

If sources conflict, stop and report the conflict. Do not pick one silently.

## How to work
1. Read the assigned brief in `docs/agent-tasks/`. Read only the files it lists plus the `CONTEXT.md` of any module you touch.
2. Before editing, reply with: understanding, files you intend to change, plan, risks, open questions. Wait for approval unless the brief says `approval: not-required`.
3. Stay in scope. Respect "Allowed to change" and "Must NOT change". No drive-by refactors, no speculative abstractions. New dependency = justify need, license, maintenance.
4. If a decision is missing, list options with trade-offs and ask. Never invent permanent architecture decisions.
5. Run the verification commands. Report exact commands and results, and say plainly what you did NOT verify.
6. Update docs, `CONTEXT.md` and ADR drafts that your change affects.
7. Finish with the report from `docs/templates/task-report.template.md` in the PR description.

## Hard architecture rules (enforced by architecture tests once they exist)
- Modular monolith. Modules live in `src/Modules/<Name>/`.
- A module may reference another module only through that module's `Contracts` project.
- Each module owns its schema, DbContext and migrations. No cross-module SQL joins, foreign keys or shared DbContext.
- Cross-module state changes use integration events (outbox/inbox), never a shared transaction.
- The Domain layer does not depend on EF Core, HTTP, messaging or external providers.
- No Data Annotations for validation or mapping. Use EF Fluent API, boundary validation (FluentValidation or an approved alternative), domain invariants in entities, and DB constraints.
- Base types only where useful (`IEntity` for identity, audit fields only where needed).
- Business rules never live only in endpoints or in the frontend.

## Security and privacy (this repository may be public)
- Never commit secrets, keys, tokens, real credentials, real user data or personal data. Use placeholders and `.env.example`.
- Do not put exploit-level security details or infrastructure specifics into public docs.
- Passwords: one-way hash only. Personal data encryption follows the accepted ADR. Never write custom cryptography.
- Never log passwords, tokens, message bodies or unnecessary personal data.
- Authorize at the resource level on every endpoint. Never trust client-supplied owner IDs.

## Context efficiency
- Start from the task brief, this file, the module `CONTEXT.md` and `docs/generated/` maps (when present) before searching code.
- Do not read the whole repository. Use targeted search.
- If you needed context that was not provided, say what was missing in your report so the briefs can improve.
- Keep the "Context usage" section of your report honest (files read, searches, wasted effort).

## Commands
Run from the repository root. Requires the .NET SDK pinned in `global.json` (10.0.402 or a later 10.0 feature band).
- Restore: `dotnet restore TalepPazari.slnx`
- Build: `dotnet build TalepPazari.slnx` (warnings are errors; the build must stay at zero warnings)
- Run Api: `dotnet run --project src/Hosts/TalepPazari.Api` (listens on `http://localhost:5080`, set in `Properties/launchSettings.json`)
- Liveness check: `curl -i http://localhost:5080/health/live` (expects `200 Healthy`; process only, also with the database down)
- Readiness check: `curl -i http://localhost:5080/health/ready` (`200 Healthy`, or `503 Unhealthy` as plain text when a module's critical dependency such as the database is unreachable)
- Telemetry exporter (both hosts): `Telemetry:Exporter` = `Console` (default) or `None`, for example `$env:Telemetry__Exporter = 'None'` (PowerShell) before `dotnet run`. Logs are JSON on the console; the log policy is in code (`docs/architecture/observability.md`)
- OpenAPI document (Development only): `curl http://localhost:5080/openapi/v1.json`
- Run Worker: `dotnet run --project src/Hosts/TalepPazari.Worker` (logs `Worker started`, then runs the outbox dispatcher; settings in the `Outbox` section of its `appsettings.json`; Ctrl+C to stop)
- Test: `dotnet test TalepPazari.slnx` (builds every project under `src/` first; architecture tests need no database, Docker or network)
- One test class: `dotnet test TalepPazari.slnx --filter "FullyQualifiedName~ModuleReferenceTests"`
- Integration tests only: `dotnet test tests/TalepPazari.IntegrationTests`. They need Docker: Testcontainers starts a throwaway `postgis/postgis:18-3.6` container (same image as `compose.yaml`) and gives each test its own database, so local data is never touched. Without Docker they are skipped with a message, and the rest of `dotnet test` still runs.
- Force the integration tests (fail instead of skip when Docker is missing; this is the mode CI uses): set `TALEPPAZARI_REQUIRE_DOCKER=true` first (PowerShell `$env:TALEPPAZARI_REQUIRE_DOCKER = 'true'`, bash `TALEPPAZARI_REQUIRE_DOCKER=true dotnet test ...`)
- Api tests only: `dotnet test tests/TalepPazari.ApiTests`. They start the real Api host in memory (`WebApplicationFactory`). Pagination tests need Docker (per-class PostgreSQL fixture, same skip/`TALEPPAZARI_REQUIRE_DOCKER` rule); every other Api test runs and passes without Docker.
- OpenAPI snapshot, deliberate update (local only, refused when `CI=true`): set `TALEPPAZARI_UPDATE_SNAPSHOTS=true`, run `dotnet test tests/TalepPazari.ApiTests --filter "FullyQualifiedName~OpenApiSnapshotTests"`, unset it, review and commit the diff of `tests/TalepPazari.ApiTests/Snapshots/openapi-v1.json`. On a mismatch the test writes `openapi-v1.received.json` to its output directory.
- Architecture tests (`tests/TalepPazari.ArchitectureTests`) discover modules by the `TalepPazari.Modules.<Name>.<Layer>` convention; a new module needs no test change. A failure message names the rule and the offending project or type.

CI (`.github/workflows/ci.yml`, `.github/workflows/codeql.yml`; Linux runner, Release configuration):
- Environment CI sets: `CI=true` (GitHub sets it), `TALEPPAZARI_REQUIRE_DOCKER=true`, `TESTCONTAINERS_RYUK_DISABLED=true`. CI never sets `TALEPPAZARI_UPDATE_SNAPSHOTS`.
- Run the same steps locally, in this order, with Docker running (bash: `export CI=true TALEPPAZARI_REQUIRE_DOCKER=true TESTCONTAINERS_RYUK_DISABLED=true`; PowerShell: set each with `$env:NAME = 'true'`, and clear them afterwards):
  1. `dotnet restore TalepPazari.slnx`
  2. `dotnet build TalepPazari.slnx --configuration Release --no-restore`
  3. `dotnet format TalepPazari.slnx --verify-no-changes --no-restore`
  4. `docker pull postgis/postgis:18-3.6`
  5. `dotnet test tests/TalepPazari.ArchitectureTests --configuration Release --no-build`
  6. `dotnet test tests/TalepPazari.ApiTests --configuration Release --no-build`
  7. `dotnet test tests/TalepPazari.IntegrationTests --configuration Release --no-build`
- CI also runs dependency review (pull requests), a Gitleaks secret scan (the PR's commits; full history on `main`) and CodeQL. A weekly scheduled run repeats build and test, so a new NuGet advisory shows up without a PR.
- Required checks: `CI result` (aggregate of the CI jobs) and `Analyze (csharp)` (CodeQL). Renaming a job renames its check and breaks branch protection.

Local database (PostgreSQL 18 + PostGIS, Docker Compose; details in README "Local development"):
- First-time setup: `Copy-Item .env.example .env` (PowerShell) or `cp .env.example .env` (bash)
- Start: `docker compose up -d`, then `docker compose ps` until `postgres` shows `healthy`
- Stop / start (data kept): `docker compose stop` / `docker compose start`; `docker compose ps -a` shows stopped containers too (plain `ps` hides them)
- PostGIS check: `"SELECT postgis_version();" | docker compose exec -T postgres sh -c 'psql -U $POSTGRES_USER -d $POSTGRES_DB'`
- Reset (wipes data shared by all checkouts and worktrees on this machine): `docker compose down -v`, then `docker compose up -d`

Connection string and migrations (details in `docs/module-template/how-to-add-a-module.md`):
- Restore local tools (pinned `dotnet-ef`): `dotnet tool restore`
- Connection string, once per machine (both hosts share one `UserSecretsId`; values from your `.env`): `dotnet user-secrets set "ConnectionStrings:Default" "Host=127.0.0.1;Port=<POSTGRES_PORT>;Database=<POSTGRES_DB>;Username=<POSTGRES_USER>;Password=<POSTGRES_PASSWORD>" --project src/Hosts/TalepPazari.Api`
- Apply a module's migrations (explicit only, never at startup): `dotnet ef database update --project src/Modules/<Name>/TalepPazari.Modules.<Name>.Infrastructure --startup-project src/Hosts/TalepPazari.Api --context <Name>DbContext`
- Example module: `<Name>` = `Example`; endpoints `POST http://localhost:5080/api/v1/example/items` with body `{"name":"..."}` and `GET http://localhost:5080/api/v1/example/items?limit=20&cursor=<nextCursor>`

## Map
- `SUMMARY.md`: product and architecture overview
- `docs/roadmap.md`: phases and exit criteria
- `docs/agent-tasks/`: task briefs and status
- `docs/adr/`: architecture decisions
- `docs/templates/`: templates (task, ADR, CONTEXT, report)
- `src/Modules/*/CONTEXT.md`: per-module context
- `docs/module-template/how-to-add-a-module.md`: checklist for adding a module (`src/Modules/Example` is the reference), including the outbox/inbox pattern
- `src/Shared/TalepPazari.SharedKernel/Messaging/`: outbox, inbox and unit-of-work abstractions; `src/Hosts/TalepPazari.Worker/Outbox/`: the dispatcher
- `tests/TalepPazari.IntegrationTests/`: PostgreSQL integration tests (Testcontainers)
- `docs/architecture/api-conventions.md`: routes and versioning, problem details, validation messages, cursor pagination, OpenAPI snapshot
- `docs/architecture/observability.md`: log format and log policy, correlation ID, traces and metrics, health endpoints and module health checks
- `tests/TalepPazari.ApiTests/`: HTTP tests against the in-memory Api host, including the OpenAPI snapshot
