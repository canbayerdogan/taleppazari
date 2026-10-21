# T-006 — Architecture tests

- **Phase:** 1
- **Status:** todo
- **Depends on:** T-005
- **Approval:** required
- **Branch:** `task/T-006-architecture-tests`

## Goal
Make module boundary rules executable so a violation fails the build instead of relying on discipline.

## Context to read
1. `AGENTS.md` ("Hard architecture rules")
2. `docs/adr/ADR-002-*.md`, `ADR-003-*.md`, `ADR-004-*.md`
3. `src/Modules/Example/**`

## Allowed to change
- `tests/TalepPazari.ArchitectureTests/**`, `Directory.Packages.props` (new test packages), `AGENTS.md` Commands section

## Must NOT change
- Production code (report rule violations you find instead of fixing them silently)

## Deliverables
Tests that enforce at least:
- A module references another module only through its `Contracts` project.
- `Domain` does not depend on `Application`, `Infrastructure`, `Api`, EF Core or ASP.NET Core.
- No Data Annotations attributes are used for validation/mapping in module projects.
- Each module has its own DbContext; no DbContext is shared across modules.
- Module implementation types are not publicly exposed outside `Contracts` where the ADR says so.
- Naming/placement rules for integration events (live in `Contracts`).

## Acceptance criteria
- [ ] Tests pass on the current code.
- [ ] You show evidence (in the PR report, not committed) that a deliberate violation for each rule fails the tests.
- [ ] Test names describe the rule in plain language.

## Verification
Run the test project; for the evidence, temporarily introduce each violation locally, show the failure, revert.

## Out of scope
- CI wiring (T-009).

## Open questions to raise instead of deciding
- Which architecture-test library to use: compare at least two on maintenance, license and fit; recommend one in your plan and wait for approval.
