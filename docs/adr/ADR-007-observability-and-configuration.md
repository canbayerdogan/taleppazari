# ADR-007 — Observability and configuration

- **Status:** Proposed
- **Date:** 2026-10-22
- **Deciders:** project owner

## Context
Two stateless hosts run as multiple instances ([ADR-001](ADR-001-modular-monolith-first.md)), and work flows from Api requests through outbox events into the Worker ([ADR-003](ADR-003-inter-module-communication.md)). Diagnosing problems requires following one operation across hosts and instances. The repository may be public, and the platform handles private messages and personal data, so configuration and logs must not expose secrets or personal data.

## Decision
- **Logging:** structured logging.
- **Correlation IDs:** this ADR owns the correlation ID rule. A correlation ID is taken from the incoming request header (named in [ADR-006](ADR-006-api-conventions.md)) only if it passes validation for length and allowed characters; otherwise, or if missing, a new one is generated. The accepted value is included in every log entry and propagated into integration events and the handlers that process them. Exact limits are set in T-008.
- **Tracing and metrics:** OpenTelemetry for traces and metrics.
- **Health:** the Api exposes liveness and readiness HTTP endpoints. The Worker must also be observable as alive and able to work; the mechanism is not decided here (see Open).
- **Configuration:** via environment variables and a secret store. No secrets in the repository; `.env.example` with placeholders only.
- **Log redaction:** never log passwords, tokens, message bodies or unnecessary personal data.

## Alternatives considered
- **Plain-text logs.** Simple. Rejected: hard to search and correlate across instances.
- **Vendor-specific tracing/metrics SDK.** Richer features for one vendor. Rejected: OpenTelemetry keeps the backend choice open.
- **Configuration files with secrets in the repository** (even encrypted). Convenient. Rejected: the repository may be public, and leaked keys are hard to rotate everywhere.
- **A single health endpoint.** Simpler. Rejected: an orchestrator needs to distinguish "restart me" (liveness) from "do not send traffic yet" (readiness).

## Consequences
- Positive: one operation can be traced from request to background handler.
- Positive: backend for telemetry and secret store can be chosen later without code changes beyond configuration.
- Negative: redaction rules must be maintained as new data types appear.
- Negative: telemetry has storage and processing cost.

## Risks and mitigations
- **Sensitive data in logs.** Mitigated by redaction rules, structured logging (no string-concatenated payloads) and review.
- **Secrets committed by mistake.** Mitigated by `.gitignore` rules, `.env.example` placeholders and review.
- **Readiness checks that depend on too much** (cascading unavailability). Mitigated by keeping readiness limited to the host's own critical dependencies.
- **Untrusted correlation ID header** (log injection, oversized values). Mitigated by validating length and characters and replacing invalid values.
- **A stuck or dead Worker goes unnoticed** (no HTTP surface today). Mitigated once the Worker health mechanism is decided; until then, review of deployment setup.

## Enforcement
- Implementation and tests in T-008: correlation ID middleware, propagation into events, health endpoints.
- Review checklist: no secrets, no sensitive fields in log statements.
- Tests in T-008: an invalid incoming correlation ID is replaced; a valid one is kept.

## Revisit when
- Telemetry cost or volume becomes a measured problem.
- A deployment target is chosen that requires a different health or configuration model.

## Open
- **Secret store** product is not decided.
- **Telemetry backend** is not decided.
- **Worker health mechanism** (for example a small HTTP listener, a heartbeat record, or an orchestrator-level check) is not decided. Decide together with the deployment target.
