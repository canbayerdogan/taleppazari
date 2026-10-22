# ADR-006 — API conventions

- **Status:** Proposed
- **Date:** 2026-10-22
- **Deciders:** project owner

## Context
The Api host serves the web client (Phase 4) and later a mobile app through the same REST API. Every module contributes endpoints ([ADR-002](ADR-002-module-boundaries-and-layout.md)). Without shared conventions, versioning, errors, paging and authorization would differ per module.

## Decision
- **Versioning:** URL path versioning, `/api/v1`.
- **OpenAPI:** one OpenAPI document per host.
- **Errors:** RFC 9457 problem details.
- **Correlation:** requests carry a correlation ID header, `X-Correlation-ID`. Generation, propagation to logs and events, and logging rules are defined in [ADR-007](ADR-007-observability-and-configuration.md).
- **Pagination:** cursor pagination for list endpoints. This ADR owns the pagination rule; other ADRs link here.
- **Authorization:** resource-based authorization in the application layer.
- **Endpoints:** no business rules in endpoints. Endpoints map HTTP to application use cases.
- **Idempotency:** idempotency keys for unsafe operations where retries are expected.

## Alternatives considered
- **Header or media-type versioning.** Cleaner URLs. Rejected: harder to see, test and route; path versioning is explicit.
- **Custom error format.** Full control. Rejected: problem details is a standard that clients and tooling understand.
- **Offset pagination.** Simple, supports page numbers. Rejected: slow on large tables and unstable when items are inserted or removed between pages.
- **Authorization only at endpoint level** (roles/policies on routes). Simple. Rejected: cannot check ownership of a specific resource; resource-level checks belong with the use case.
- **GraphQL.** Flexible queries. Rejected: not part of the technology baseline; REST with OpenAPI fits web and mobile clients.

## Consequences
- Positive: consistent API across modules; clients handle errors and paging one way.
- Positive: authorization lives next to the business rule, where it is testable.
- Negative: cursor pagination does not support jumping to an arbitrary page.
- Negative: idempotency keys need storage and expiry handling for the operations that use them.

## Risks and mitigations
- **Breaking API changes without a new version.** Mitigated by OpenAPI snapshot tests ([ADR-005](ADR-005-testing-strategy.md)).
- **Missing resource-level authorization on a new endpoint.** Mitigated by authorization/ownership tests per endpoint and review.
- **Leaking internal details in problem details.** Mitigated by review and by mapping exceptions to safe messages.

## Enforcement
- OpenAPI snapshot tests in CI.
- Architecture tests (T-006): `Api` projects do not contain domain logic and call only the module's application layer.
- Security tests per endpoint: authorization and ownership.
- Standards and helpers are implemented in T-008.

## Revisit when
- A client requirement cannot be met with cursor pagination.
- A second API version is needed.

## Later phase
- **Authentication** is decided in the Phase 2 Identity ADR.
- **Rate limiting** is decided in Phase 4.
