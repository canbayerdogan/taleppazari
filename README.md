# TalepPazarı

A peer-to-peer marketplace for listings and a demand market where providers send offers. The platform connects people; payments and delivery happen privately between users.

Status: early foundation phase (no product features yet).

- Product and architecture overview: [SUMMARY.md](SUMMARY.md)
- Roadmap: [docs/roadmap.md](docs/roadmap.md)
- Contributor and AI-agent rules: [AGENTS.md](AGENTS.md)
- Architecture decisions: [docs/adr/](docs/adr/)

## Local development

The local database is PostgreSQL 18 with PostGIS 3.6 (`postgis/postgis:18-3.6`), run with Docker Compose from `compose.yaml`. It is for local development only.

### Prerequisites
- Docker Desktop (or another engine with Docker Compose v2). Verified with Docker Desktop; other engines are untested.
- .NET SDK as pinned in `global.json` (see [AGENTS.md](AGENTS.md#commands) for build and run commands).

### First-time setup
Run from the repository root:

```powershell
Copy-Item .env.example .env    # PowerShell
# cp .env.example .env         # bash
docker compose up -d
docker compose ps              # wait until STATUS shows "(healthy)"
```

`.env` is ignored by Git. The values in `.env.example` are obviously fake placeholders (`change-me-local-only`). You may change them in your own `.env`, but never reuse this password anywhere real. The database port is published on `127.0.0.1` only.

### Daily commands
| Task | Command |
|---|---|
| Start | `docker compose up -d` |
| Status | `docker compose ps -a` (`-a` also lists stopped containers) |
| Stop (keeps data) | `docker compose stop` |
| Start again | `docker compose start` |
| Remove container (keeps data) | `docker compose down` |
| Logs | `docker compose logs postgres` |
| **Reset database (wipes all data)** | `docker compose down -v`, then `docker compose up -d` |

### Check that PostGIS works
The command reads the user and database name from `.env` inside the container. It works unchanged in PowerShell and bash; the SQL is piped in because Windows PowerShell 5.1 strips nested double quotes from arguments to native commands.

```powershell
"SELECT postgis_version();" | docker compose exec -T postgres sh -c 'psql -U $POSTGRES_USER -d $POSTGRES_DB'
```

Expected output contains a line like `3.6 USE_GEOS=1 USE_PROJ=1 USE_STATS=1`.

### Point the hosts at the local database
The Api and Worker hosts read one connection string, `ConnectionStrings:Default`. In development it comes from `dotnet user-secrets`; both hosts share one `UserSecretsId`, so one command configures both. Use the values from your own `.env`:

```powershell
dotnet user-secrets set "ConnectionStrings:Default" "Host=127.0.0.1;Port=<POSTGRES_PORT>;Database=<POSTGRES_DB>;Username=<POSTGRES_USER>;Password=<POSTGRES_PASSWORD>" --project src/Hosts/TalepPazari.Api
```

Use `127.0.0.1`, not `localhost`: the port is published on IPv4 loopback only, and `localhost` may resolve to IPv6 first and time out. The environment variable `ConnectionStrings__Default` works as an alternative. Never put the connection string in `appsettings*.json`. Then apply migrations as described in [AGENTS.md](AGENTS.md#commands); they are never applied at startup.

### Shared database across checkouts
The compose project name is fixed to `taleppazari`. All checkouts and Git worktrees of this repository on one machine therefore share the same compose project, the same container and the same data volume. `docker compose down -v` run from any of them wipes that shared database.

### Troubleshooting
- **Port 5432 is already in use** (for example a locally installed PostgreSQL): set another port in `.env`, such as `POSTGRES_PORT=5433`, then run `docker compose up -d` again.
- **`required variable POSTGRES_... is missing a value`**: `.env` is missing or incomplete. Copy `.env.example` to `.env`.
- **Changed credentials in `.env` have no effect**: PostgreSQL only reads them when the data volume is first created. Reset the database (`docker compose down -v`, then `docker compose up -d`).
- **Container never becomes healthy**: check `docker compose logs postgres`.
- **Slow on ARM machines (for example Apple Silicon)**: the `postgis/postgis` image is published for amd64 only and runs under emulation there.

License: to be decided by the repository owner.
