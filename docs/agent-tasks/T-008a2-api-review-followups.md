# T-008a2 — API review follow-ups

- **Phase:** 1
- **Status:** todo
- **Depends on:** T-008a (done)
- **Approval:** required
- **Branch:** `task/T-008a2-api-followups`

## Goal
Fix the findings of the fresh-agent review of T-008a (commit f27dffe) that are worth fixing now. No new features.

## Context to read
1. `AGENTS.md`
2. `docs/agent-tasks/T-008a-*.md`
3. `docs/architecture/api-conventions.md`
4. `src/Hosts/TalepPazari.Api/ErrorHandling/**`
5. `tests/TalepPazari.ApiTests/**`

## Allowed to change
- `src/Hosts/TalepPazari.Api/ErrorHandling/**`
- `tests/TalepPazari.ApiTests/**`
- `docs/architecture/api-conventions.md`
- `src/Modules/Example/**` only if the empty-cursor check (item 5) shows a real defect

## Must NOT change
- ADRs, `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `.github/*`, `docs/agent-tasks/README.md`
- The Worker host, `tests/TalepPazari.ArchitectureTests/**`, the OpenAPI snapshot (if it must change, stop and ask)

## Deliverables
1. **M1:** every problem body has `type`. When the framework's default table has no entry for a status (for example 413, later 429), use `about:blank`. Add a test with at least one status outside the table, reached through a real path or a test-host-only endpoint.
2. **M2:** the test "exception after the response started" must prove the request is aborted, or its name and the docs must stop claiming it. First try to observe the abort (for example the client-side exception kind, or a Kestrel-backed test host if the framework supports it). If that is not practical, rename the test to what it proves (logs type and route only, exception does not reach the log as a message), change the wording in the docs, and list "abort not verified" under Not verified. State the choice in your plan.
3. **L1:** add 23-character invalid cursors (one character replaced by a space or `=`, and an invalid-alphabet character at the last position) so the decoder's own checks run, not only the length check.
4. **L2:** in `api-conventions.md`, limit "every non-success response is application/problem+json" to responses the application pipeline produces (requests rejected by the server before the pipeline, such as an oversized request line, are not covered), and limit the "the framework does not log the exception" claim to the cases covered by `UnhandledExceptionTests`; write failures and client aborts are T-008b.
5. **L4:** check how `?cursor=` (empty value) binds and what the endpoint returns; add a test and one line in the docs describing the actual behavior. Change code only if it produces an exception or a 500.
6. **L5:** run the 500-path tests with the same four Accept values as the 4xx tests.
7. **L6:** dispose the `TestApi` when migration fails in the pagination test setup.

## Acceptance criteria
- [ ] Mutation evidence (temporary, reverted, shown in the report): M1 (remove the `about:blank` fallback: the new test fails), L1 (the new cursor tests fail against a decoder that skips the canonical re-encode check), M2 (if you implement the abort check: removing the abort makes it fail).
- [ ] Architecture tests pass unchanged. `dotnet build TalepPazari.slnx` has zero warnings; `dotnet test TalepPazari.slnx` passes; the database-free Api tests also pass without Docker; `dotnet format TalepPazari.slnx --verify-no-changes` is clean (report the real exit code).
- [ ] `git diff` shows only allowed files.

## Out of scope
- Client-abort logging, swallowed exceptions in metrics (review L3), request-log policy, correlation member: all T-008b.

## Decisions already made (do not reopen)
- Problem details shape and the single host writer (T-008a).
- The started-response middleware aborts and ends the exception; the logging rule (type and route at Error, full exception at Debug) stays.

## Report
Fill `docs/templates/task-report.template.md` in the PR description. No local paths, user names or machine details.
