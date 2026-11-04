# T-009b — CI review follow-ups

- **Phase:** 1
- **Status:** todo
- **Depends on:** T-009 (done)
- **Approval:** required
- **Branch:** `task/T-009b-ci-followups`

## Goal
Fix the findings of the fresh-agent review of T-009 that are worth fixing now. No new CI features.

## Context to read
1. `AGENTS.md` (Commands)
2. `docs/agent-tasks/T-009-ci-pipeline.md`
3. `.github/workflows/ci.yml`, `.github/dependabot.yml`

## Allowed to change
- `.github/workflows/ci.yml`, `.github/dependabot.yml`, `AGENTS.md` (Commands section)
- `.github/gitleaks.toml` or a baseline file, only if you propose it in your plan

## Must NOT change
- Anything under `src/` or `tests/`, ADRs, `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `docs/agent-tasks/README.md`
- `.github/workflows/codeql.yml` (unless a finding requires it; ask first)
- Do not weaken a check, remove a required job or change a check name

## Deliverables
1. **M1:** on push to `main`, the secret scan covers only the pushed range (`github.event.before..github.sha`, handling the all-zero `before` of a first push). Propose how the full-history scan still happens (a weekly scheduled run, a baseline file, or both) without making the required `CI result` check depend on old history. Wait for approval of the proposal.
2. **L2:** make the aggregate job not show red for runs cancelled by a newer push, while it still can never be skipped by anything but cancellation; prove with the `needs` logic that `failure` and `cancelled` upstream results still fail it in non-cancelled runs.
3. **L5/L6:** one sentence in AGENTS.md on the weekly CodeQL run; check the Dependabot comment about `github-actions` cooldown keys against the current docs and fix or remove it.
4. Keep every `uses:` pinned by full commit SHA with a version comment; do not change versions.

## Acceptance criteria
- [ ] actionlint (pinned, outside the repo) reports no errors; strict YAML parse passes.
- [ ] The workflow's commands still run locally in order with `CI=true` and `TALEPPAZARI_REQUIRE_DOCKER=true`.
- [ ] Required check names `CI result` and `Analyze (csharp)` are unchanged.
- [ ] `git diff` shows only allowed files. State plainly that the change has not run on GitHub; the PR is the first run.

## Out of scope
- Cache cleanup (L1), artifact content (L3), PR base SHA (L4), SDK drift (M2: the owner checks the run log), new jobs, Windows job.

## Decisions already made (do not reopen)
- Linux runner, no secrets, no `pull_request_target`, no `paths` filters, SHA pinning, Gitleaks CLI with hard-coded SHA-256.

## Report
Fill `docs/templates/task-report.template.md` in the PR description. No local paths, user names or machine details.
