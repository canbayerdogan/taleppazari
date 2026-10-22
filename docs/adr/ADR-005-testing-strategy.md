# ADR-005 — Testing strategy

- **Status:** Proposed
- **Date:** 2026-10-22
- **Deciders:** project owner

## Context
The architecture rules ([ADR-001](ADR-001-modular-monolith-first.md) to [ADR-004](ADR-004-persistence-and-code-standards.md)) only hold if they are tested. Much of the code will be written by AI agents, so tests are the main evidence that a change is correct. Persistence behavior (constraints, migrations, concurrency) differs between real PostgreSQL and in-memory substitutes.

## Decision
- **Unit tests** for the domain (entities, value objects, invariants).
- **Integration tests** against **real PostgreSQL** running in containers.
- **Architecture tests** for the boundary and layering rules.
- **Contract tests** for integration events and **OpenAPI snapshots**.
- **Security tests** are added as features arrive: authorization, ownership, input handling.
- **CI gates** are described in T-009.

## Alternatives considered
- **In-memory database provider or SQLite for integration tests.** Faster, no containers. Rejected: behavior differs from PostgreSQL (constraints, transactions, types, PostGIS), so tests pass while production fails.
- **Mocking the database in application tests.** Fast. Rejected as the main approach: verifies mocks, not queries and mappings.
- **Mostly end-to-end tests.** Close to real use. Rejected as the main layer: slow, brittle and hard to diagnose; used sparingly later.
- **Boundary rules enforced by review only.** No test code. Rejected: does not scale with AI-written changes.

## Consequences
- Positive: architecture rules are executable; persistence bugs surface before production.
- Positive: event and API contract changes become visible in pull requests.
- Negative: integration tests need a container runtime locally and in CI and are slower.
- Negative: snapshot tests need deliberate updates when contracts change.

## Risks and mitigations
- **Slow test suite.** Mitigated by sharing containers per test run and keeping most logic in fast unit tests.
- **Snapshots updated without review.** Mitigated by review: snapshot changes are called out in the PR.
- **Security tests postponed indefinitely.** Mitigated by adding authorization and ownership tests in the same PR as each new endpoint.

## Enforcement
- CI (T-009) runs all test categories and blocks merge on failure.
- Architecture tests are created in T-006.
- Review: new endpoints include authorization/ownership tests; new events include contract tests.

## Revisit when
- CI duration measurably slows development.
- A test category repeatedly misses defects of a kind it is meant to catch.
