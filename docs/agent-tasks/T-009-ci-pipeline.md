# T-009 — CI pipeline

- **Phase:** 1
- **Status:** todo
- **Depends on:** T-003 (extend after T-006, T-007, T-008)
- **Approval:** required
- **Branch:** `task/T-009-ci`

## Goal
Every pull request is built, tested and scanned automatically. A red check blocks merging.

## Context to read
1. `AGENTS.md`
2. `docs/repo-setup-checklist.md`
3. `.github/*`, the solution and test projects

## Allowed to change
- `.github/workflows/**`, `.github/dependabot.yml` if needed, `AGENTS.md` Commands section

## Must NOT change
- Application code, ADRs

## Deliverables
- Workflow: restore, build, format verification, unit/integration/architecture tests (integration tests use a real PostgreSQL).
- Code scanning (CodeQL for C#), dependency review on PRs, secret scanning step in addition to GitHub's push protection.
- Add the `github-actions` entry (weekly, minor/patch grouped) to `.github/dependabot.yml` (created by T-003).
- Least-privilege `permissions` on every workflow, concurrency cancellation, NuGet caching, third-party actions pinned (by version tag or commit SHA, explain which).
- A short list of the exact check names so the owner can mark them required in branch protection.

## Acceptance criteria
- [ ] A PR with failing tests shows a red check; a clean PR shows green.
- [ ] Workflows request no more permissions than needed.
- [ ] No secrets in workflow files; none required for the basic pipeline.
- [ ] Documented in the report: how long a run takes.

## Verification
Open a throwaway PR for both cases and paste the check results.

## Out of scope
- Deployment/CD, container publishing (Phase 4), repo map check (added by T-010).
