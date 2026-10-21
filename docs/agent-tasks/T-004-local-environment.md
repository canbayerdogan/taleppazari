# T-004 — Local development environment

- **Phase:** 1
- **Status:** todo
- **Depends on:** T-003
- **Approval:** required
- **Branch:** `task/T-004-local-environment`

## Goal
One command starts everything the app needs locally (PostgreSQL with PostGIS). A new machine becomes productive quickly.

## Context to read
1. `AGENTS.md`
2. `SUMMARY.md` (section 4)
3. `src/Hosts/*` (to wire configuration)

## Allowed to change
- `compose.yaml`, `.env.example`, `scripts/*`, `README.md` (development section), `AGENTS.md` Commands section
- Host configuration files needed to read a connection string

## Must NOT change
- Application architecture, other docs, `.github/*`

## Deliverables
- `compose.yaml` with PostgreSQL + PostGIS using a pinned image tag (explain the version choice).
- `.env.example` with placeholder values only; real `.env` stays ignored.
- Readiness check in the Api that verifies database connectivity.
- README "Local development" section: prerequisites, start/stop commands, reset database, troubleshooting.

## Acceptance criteria
- [ ] From a clean clone, documented steps bring up the database and both hosts.
- [ ] Readiness endpoint is healthy with the database up and unhealthy with it down.
- [ ] No real credentials anywhere; default dev passwords are clearly placeholders.

## Verification
Run the documented steps; stop the database and confirm readiness fails. Paste results.

## Out of scope
- Production deployment, backups, other infrastructure (broker, object storage).
