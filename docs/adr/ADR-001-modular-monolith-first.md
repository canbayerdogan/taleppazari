# ADR-001 — Modular monolith first

- **Status:** Proposed
- **Date:** 2026-10-22
- **Deciders:** project owner

## Context
TalepPazarı starts with a small team and a product whose domain boundaries (listings, demands, offers, communication, reputation, moderation, search) are not yet validated by real usage. We need strong boundaries so the codebase stays maintainable, without paying the operational cost of a distributed system before there is a measured need for one.

## Decision
- The system is one codebase and one solution, organized into **modules** with enforced boundaries (layout and rules: [ADR-002](ADR-002-module-boundaries-and-layout.md)).
- Two runtime hosts are built from the same codebase:
  - **Api**: serves user-facing requests.
  - **Worker**: runs background jobs, outbox dispatch, notifications and media processing.
- Both hosts are stateless and can run as multiple instances.
- A module is extracted into a separate service only through a **new ADR** backed by measured need: scaling, team structure or release cadence.

## Alternatives considered
- **Microservices from day one.** Independent deployment and scaling per service. Rejected: high operational cost (deployment, networking, distributed tracing, distributed data consistency) for a small team, and the service boundaries are not yet validated. A wrong boundary is much more expensive to fix across services than across modules.
- **Layered monolith without module boundaries** (one Domain/Application/Infrastructure stack for everything). Cheapest to start. Rejected: dependencies between features grow unchecked, data ownership becomes unclear, and later extraction becomes practically impossible.

## Consequences
- Positive: one build, one deployment unit per host, simple local development, refactoring across modules is still cheap while boundaries settle.
- Positive: module boundaries keep a later extraction possible, without committing to it.
- Negative: all modules share one process per host, so they share a failure domain and resource pool.
- Negative: boundaries are only as strong as their enforcement; without architecture tests they will erode.
- Neutral: scaling is per host (Api vs. Worker), not per module.

## Risks and mitigations
- **Single process failure domain** (one faulty module can degrade the whole host). Mitigations:
  - multiple instances of each host;
  - Api/Worker separation, so background load does not starve user requests;
  - health checks (liveness/readiness, see [ADR-007](ADR-007-observability-and-configuration.md));
  - timeouts on outbound calls and limits on resource usage;
  - safe rollout (gradual deployment with rollback).
- **Boundary erosion.** Mitigated by architecture tests ([ADR-005](ADR-005-testing-strategy.md)) and review.
- **Pressure to extract a module early.** Mitigated by requiring an ADR with measurements.

## Enforcement
- Architecture tests (T-006) check module references and layering as defined in ADR-002.
- CI (T-009) runs the architecture tests on every pull request.
- Review: any proposal to add a separate deployable service must reference an accepted ADR.

## Revisit when
- A module has a measured scaling need that cannot be met by scaling the host (for example, sustained resource saturation caused by one module).
- Team structure requires independent ownership and release cadence for a module.
- Release cadence of one module is measurably blocked by the shared deployment.

## Open
- **Deployment target** (hosting platform for the hosts) is not decided by this ADR.
