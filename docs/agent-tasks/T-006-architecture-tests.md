# T-006 — Architecture tests

- **Phase:** 1
- **Status:** todo
- **Depends on:** T-005 (done); ADR-005 (Accepted)
- **Approval:** required
- **Branch:** `task/T-006-architecture-tests`

## Goal
Make the module boundary rules executable so a violation fails the build instead of relying on discipline. The tests must keep working, without edits, when new modules are added (Phase 2 and later).

## Context to read
1. `AGENTS.md` ("Hard architecture rules", Commands)
2. `docs/adr/ADR-002-*.md`, `ADR-003-*.md`, `ADR-004-*.md`, `ADR-005-*.md`
3. `docs/adr/ADR-006-*.md` (Proposed; only the Enforcement line about `Api` projects)
4. `src/Modules/Example/CONTEXT.md` and the five `*.csproj` files of `src/Modules/Example/`
5. `src/Hosts/*/*.csproj`, `src/Shared/*/*.csproj`
6. `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `TalepPazari.slnx`

## Facts about the code today (from T-005, verify them, do not assume)
Direct project references:
- `Contracts` → none. `Domain` → none.
- `Application` → `Contracts`, `Domain`.
- `Infrastructure` → `Application`.
- `Api` (module) → `Application`.
- Host `Api` → `Example.Api`, `Example.Infrastructure`. Host `Worker` → `Example.Infrastructure`.
- `SharedKernel` → none (still empty).

Visibility today:
- Infrastructure: everything `internal` except `ExampleModule` (public).
- Api: request/response records and the validator-free endpoint helpers are `internal`; `ExampleEndpoints` is public.
- Application: handler, command, result, registration are `public` on purpose (other projects of the same module use them); validators are `internal`.
- `InternalsVisibleTo` is not used. Boundaries between modules rest on project references, not on visibility.

Other:
- Only the `Example` module exists, so cross-module rules cannot be exercised by real code yet (see Verification).
- Migration files under `Persistence/Migrations/` are tool-generated; exclude them where they would cause noise, and say so.

## Allowed to change
- `tests/TalepPazari.ArchitectureTests/**` (new), `tests/.editorconfig` only if a rule conflicts with plain-language test names (propose it in your plan; no global relaxation)
- `TalepPazari.slnx` (add the test project)
- `Directory.Packages.props` (new test packages; each needs a license and maintenance check in your plan)
- `AGENTS.md` Commands section (test commands)

## Must NOT change
- Production code under `src/` (report violations you find instead of fixing them; the temporary violations used as evidence must be reverted)
- ADRs, `Directory.Build.props`, `global.json`, `.github/*`, `docs/agent-tasks/README.md`
- No `InternalsVisibleTo` in production projects to make tests easier; use reflection or other means that need no production change

## Deliverables
One test project `tests/TalepPazari.ArchitectureTests`. Modules are discovered by the naming convention `TalepPazari.Modules.<Name>.<Layer>`, so a new module is covered automatically. Tests enforce at least:

**Module and layer references**
1. A module references another module only through its `Contracts` project (checked on project references and on type dependencies).
2. `Contracts` depends on no other layer of any module and on no EF Core or ASP.NET Core types.
3. `Domain` depends only on the BCL and itself: not on `Application`, `Infrastructure`, `Api`, EF Core, ASP.NET Core, messaging or provider libraries. Prefer an allowlist over a denylist; list any exception you needed.
4. `Application` depends only on `Domain`, `Contracts` of its own module, the BCL and the approved libraries it uses today (FluentValidation, DI abstractions); not on `Infrastructure` or `Api`.
5. Module `Api` projects depend on `Application` only: not on `Domain` or `Infrastructure` (ADR-002 layout; ADR-006 enforcement line).
6. Hosts reference modules only through `Infrastructure` and `Api` projects, for composition (ADR-002).
7. `SharedKernel` references no module.

**Persistence**
8. Each module has its own DbContext, defined in its `Infrastructure` project, `internal`; no DbContext type exists anywhere else; no DbContext is used by two modules. If the schema-per-module rule can be checked without production changes (for example the default schema equals the module's schema constant), add it; otherwise report why not.

**Code standards**
9. No Data Annotations in module projects: no attribute from `System.ComponentModel.DataAnnotations` (including `.Schema`) and no reference to the annotations assembly. `System.Diagnostics.CodeAnalysis` attributes such as `MemberNotNullWhen` must not be flagged (there is a test for that distinction).

**Visibility**
10. `Infrastructure` exposes exactly one public type, `<Name>Module`. Module `Api` exposes exactly one public type, `<Name>Endpoints`. FluentValidation validators (`AbstractValidator<T>`) and repository implementations are `internal`. Public types in `Application` and `Domain` are allowed.

**Integration events**
11. Integration events live in `Contracts`, namespace `*.Contracts.IntegrationEvents`, are `public sealed record`s and are named `<Name>V<N>` (for example `ExampleItemCreatedV1`). No `IntegrationEvents` namespace exists outside `Contracts`.

**Guards**
12. A guard test fails when module discovery finds no module or when a rule would run against zero types, so no rule passes vacuously.

## Acceptance criteria
- [ ] All tests pass on the current code.
- [ ] For each of rules 1-11 you show evidence in the PR report (not committed) that a deliberate violation fails the tests: temporarily introduce it locally, show the failing test and message, revert. Rule 1 and 6 may need a throwaway second module or a throwaway reference; explain what you used.
- [ ] After the evidence step, `git status` shows no changes under `src/`.
- [ ] Test names state the rule in plain language; failure messages name the offending project or type.
- [ ] Adding a new module that follows `docs/module-template/how-to-add-a-module.md` requires no change to the test project (explain how you checked; a throwaway dummy module is enough, do not commit it).
- [ ] `dotnet build TalepPazari.slnx` has zero warnings and `dotnet test TalepPazari.slnx` passes from a clean clone.
- [ ] Tests do not need a database, Docker or network access.

## Verification
Run `dotnet restore`, `dotnet build TalepPazari.slnx --no-incremental`, `dotnet test TalepPazari.slnx`, `dotnet format TalepPazari.slnx --verify-no-changes`. Paste results, plus the evidence for each deliberate violation. Say in "Not verified" what you did not check.

## Out of scope
- CI wiring (T-009), repo map generator (T-010), integration and contract tests (T-007 and later), any production code change.
- Rules for features that do not exist yet (authorization, outbox usage, facades). Do not invent rules from the ADRs beyond this list; propose extra rules in your report.

## Decisions already made (do not reopen)
- Rules rest on project references and type dependencies, not on `InternalsVisibleTo`.
- Application types used by other projects of the same module stay `public`; the visibility rule applies as in rule 10.
- Validators are `internal` (T-005 follow-up).
- Event naming `<Name>V<N>`, `public sealed record`, namespace `*.Contracts.IntegrationEvents`.
- Architecture tests are not run against the generated migration files.

## Open questions to raise instead of deciding
- **Architecture-test library.** Compare at least two (for example NetArchTest.Rules, ArchUnitNET) and a plain reflection/`System.Reflection.Metadata` approach on maintenance, license and fit. Note that IL-based libraries see type dependencies, not project references; say how you check rule 1 and 6 (parsing `.csproj` `ProjectReference` items, assembly references, or both). Recommend one and wait for approval.
- **Test framework.** ADR-005 does not choose one and T-007 will reuse it. Compare at least two (for example xUnit v3, NUnit, MSTest) on license, maintenance and fit with the pinned SDK. Check how `dotnet test` behaves with the chosen framework on the SDK in `global.json`; if `global.json` or `Directory.Build.props` would need a change, stop and ask.
- Rule 5 (module `Api` depends on `Application` only) rests on ADR-002 and on ADR-006, which is still Proposed. Say if you see a reason it should not be enforced yet.
- Anything in the Accepted ADRs that blocks a rule above, or any rule in this list that the current code already violates.

## Report
Fill `docs/templates/task-report.template.md` in the PR description. Do not include local paths, user names or machine details.
