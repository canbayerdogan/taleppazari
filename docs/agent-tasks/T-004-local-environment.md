# T-004 — Local development environment

- **Phase:** 1
- **Status:** todo
- **Depends on:** T-003 (done)
- **Approval:** required
- **Branch:** `task/T-004-local-environment`

## Goal
One command starts everything the app needs locally (PostgreSQL with PostGIS). A new machine becomes productive quickly.

## Context to read
1. `AGENTS.md`
2. `SUMMARY.md` (section 4)
3. `docs/adr/ADR-004-*.md`, `docs/adr/ADR-007-*.md` (Proposed; configuration rules only)

## Allowed to change
- `compose.yaml`, `.env.example`, `scripts/*`
- `README.md` (development section only)
- `AGENTS.md` Commands section

## Must NOT change
- Anything under `src/` (no host or application code, no connection-string wiring; that arrives with the first DbContext in T-005)
- `Directory.Packages.props`, ADRs, other docs, `.github/*`
- `docs/agent-tasks/README.md` (the owner updates the status table after merges)

## Deliverables
- `compose.yaml` with PostgreSQL + PostGIS using a pinned image tag (explain the PostgreSQL major version and PostGIS version choice).
  - Named volume for data, port published on loopback only, compose-level healthcheck (`pg_isready`).
  - Credentials come from `.env`; no real default passwords inside `compose.yaml`.
- `.env.example` with placeholder values only; real `.env` stays ignored (already in `.gitignore`).
- Helper scripts under `scripts/` only if they add value (for example reset database). Provide a Windows-friendly way (PowerShell or plain `docker compose` commands documented in README). No Linux-only requirement.
- README "Local development" section: prerequisites, first-time setup (copy `.env.example` to `.env`), start/stop commands, reset database, check that PostGIS works, troubleshooting.
- `AGENTS.md` Commands: the exact compose commands.

## Acceptance criteria
- [ ] From a clean clone, the documented steps bring up the database; `docker compose ps` shows it `healthy`.
- [ ] `SELECT postgis_version();` succeeds inside the container (show the exact command).
- [ ] Stopping the container is visible in `docker compose ps`; starting it again restores `healthy` with data kept; the documented reset command wipes it.
- [ ] No real credentials anywhere; placeholders are clearly placeholders. Port is not exposed beyond loopback.
- [ ] Both hosts still build and start exactly as before (nothing in `src/` changed).

## Verification
Run the documented steps from a clean clone, run the PostGIS query, stop and restart the container, run the reset. Paste results.

## Out of scope
- Application-side readiness check and any host configuration (T-005, T-008).
- Production deployment, backups, other infrastructure (message broker, object storage).
- Integration-test container setup (T-007 and T-009 decide how tests start PostgreSQL).

## Open questions to raise instead of deciding
- Whether `compose.yaml` works unchanged with Podman/Rancher Desktop as an alternative to Docker Desktop (state what you verified and what you did not).
- Image choice: official `postgres` plus extension install versus the `postgis/postgis` image; compare maintenance and update cadence, recommend one, wait for approval.
