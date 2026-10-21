# T-008 — API and observability standards

- **Phase:** 1
- **Status:** todo
- **Depends on:** T-003, T-005
- **Approval:** required
- **Branch:** `task/T-008-api-observability`

## Goal
Make every endpoint behave the same way: errors, correlation, versioning, health, logging and tracing.

## Context to read
1. `AGENTS.md`
2. `docs/adr/ADR-006-*.md`, `ADR-007-*.md`
3. `src/Hosts/TalepPazari.Api/**`, `src/Modules/Example/**`

## Allowed to change
- `src/Hosts/**`, `src/Shared/**` (cross-cutting only), Example module endpoint, `tests/**`
- `docs/architecture/api-conventions.md` (new)

## Must NOT change
- ADRs, domain logic

## Deliverables
- Problem-details error responses (RFC 9457) with a global exception handler that never leaks internals.
- Correlation ID middleware: accept or generate, return in the response, include in logs and in outgoing events.
- `/api/v1` route convention and OpenAPI generation for the Api host.
- Liveness and readiness endpoints (readiness aware of the database).
- Structured logging with redaction rules (no passwords, tokens, message bodies, unnecessary personal data).
- OpenTelemetry traces and metrics with a configurable exporter (console by default).
- Cursor pagination convention documented with a small reference implementation in the Example module.
- `docs/architecture/api-conventions.md` describing all of the above in about one page.

## Acceptance criteria
- [ ] A forced exception returns a problem-details body without stack trace or internal names.
- [ ] The same correlation ID appears in the response header, the log line and the emitted event.
- [ ] OpenAPI document is generated and a snapshot test guards it.
- [ ] Redaction test proves a sample password/token/message body is not logged.

## Verification
Run tests and show a sample request/response and the matching log line.

## Out of scope
- Authentication/authorization (Phase 2), rate limiting (Phase 4), dashboards.

## Open questions to raise instead of deciding
- Logging library choice: compare options on license, maintenance and fit; recommend one and wait for approval.
