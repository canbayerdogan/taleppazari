# T-009b — CI review follow-ups

- **Phase:** 1
- **Status:** todo
- **Depends on:** T-009 (done); start after T-008b is merged (both touch `AGENTS.md`)
- **Approval:** required
- **Branch:** `task/T-009b-ci-followups`

## Goal
Make the CI pipeline more robust: no runner drift, a secret scan that cannot lock `main` because of old history, no red noise from cancelled runs. Fix the findings of the fresh-agent review of T-009 that are worth fixing now. No new CI features.

## Context to read
1. `AGENTS.md` (Commands, CI paragraph)
2. `docs/agent-tasks/T-009-ci-pipeline.md`
3. `.github/workflows/ci.yml`, `.github/workflows/codeql.yml`, `.github/dependabot.yml`
4. `docs/repo-setup-checklist.md` (read only; required check names)

## Facts about the code today (verify, do not assume)
- Both workflows use `runs-on: ubuntu-latest`. The CodeQL run showed an Ubuntu 26 migration notice. When the label moves, SDK, Docker and tool versions shift without any change in the repository, and Dependabot does not update runner labels.
- The first runs installed .NET SDK 10.0.403 while `global.json` says 10.0.402 with `rollForward: latestFeature`. This was accepted for now; the weekly schedule is the safety net.
- On push to `main` the secret scan covers the full history, with no baseline or ignore file. One false positive in an old commit would turn every later push red.
- The aggregate job `CI result` runs with `if: always()`, so a run cancelled by a newer push shows a red `CI result`.
- Required checks on `main` are `CI result` and `Analyze (csharp)`. Their names must not change.
- `.github/dependabot.yml` has a comment saying `github-actions` supports only `default-days` for cooldown. It was not verified against the current docs.
- Scheduled workflows in a public repository are disabled by GitHub after 60 days without repository activity.

## Allowed to change
- `.github/workflows/ci.yml`, `.github/workflows/codeql.yml` (the runner label and any change a deliverable below requires), `.github/dependabot.yml`
- `AGENTS.md` (Commands section, CI paragraph)
- `.github/gitleaks.toml` or a baseline file, only if you propose it in your plan and I approve it

## Must NOT change
- Anything under `src/` or `tests/`, ADRs, `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `docs/agent-tasks/README.md`, `docs/repo-setup-checklist.md`
- Check names: `CI result`, `Analyze (csharp)`, and the names of the jobs behind them
- Do not weaken or remove a check, and do not change which jobs the aggregate job requires

## Deliverables
1. **Runner pinning.** Replace `ubuntu-latest` with `ubuntu-24.04` in every job of both workflows, with a short comment: moving to a newer image is a deliberate task, not a silent change. Do not change any action version.
2. **Secret scan range (review M1).** On push to `main`, scan only the pushed range (`github.event.before..github.sha`). Handle the all-zero `before` value (first push or a new branch) and a `before` commit that no longer exists (force push): in both cases fall back to a defined, documented range. Keep `--redact`, the pinned Gitleaks version and the hard-coded SHA-256 unchanged.
3. **Full-history scan.** Propose where the full-history scan still happens (for example the weekly schedule run, a baseline file, or both) without making the required `CI result` depend on old history in a way that can block unrelated pushes. Wait for my approval of the proposal before editing the workflow for this part.
4. **Cancelled runs (review L2).** The aggregate job must not show red for a run cancelled by a newer push (for example `if: ${{ !cancelled() }}`), while it must still fail on `failure` of any upstream job and on `cancelled` or `skipped` where the current rules say so. Show with the `needs` and `result` logic (a short truth table in your plan) that a failing upstream job still makes it red in a non-cancelled run, and that nothing but cancellation can skip it.
5. **SDK visibility.** Add a step that prints the installed SDK version (for example `dotnet --version`) to the build job log, so drift against `global.json` is visible in every run. Do not change `global.json` and do not try to pin the SDK exactly.
6. **Small fixes (review L5, L6).** One sentence in `AGENTS.md` about the weekly CodeQL run and the scheduled run (including the 60-day inactivity rule above, if you can confirm it in the current GitHub docs). Check the Dependabot cooldown comment against the current docs and fix or remove it.
7. Keep every `uses:` pinned by full commit SHA with a version comment; do not change versions. Keep permissions, triggers and the `paths`-filter rule (no `paths` filters) as they are.

## Acceptance criteria
- [ ] actionlint (pinned, outside the repository) reports no errors; a strict YAML parse passes for all workflow files.
- [ ] Every job in both workflows uses `ubuntu-24.04`; no `ubuntu-latest` remains (search result shown).
- [ ] The push-range logic is shown with the three cases (normal push, all-zero `before`, missing `before` commit), for example by running the range computation locally against a throwaway repository.
- [ ] The workflow's commands still run locally in the order from `AGENTS.md` with `CI=true`, `TALEPPAZARI_REQUIRE_DOCKER=true` and `TESTCONTAINERS_RYUK_DISABLED=true`.
- [ ] Required check names are unchanged (`CI result`, `Analyze (csharp)`).
- [ ] `git diff` shows only allowed files. State plainly that the changes have not run on GitHub; the pull request is the first run, and workflow changes in a pull request run with the pull request's version of the file.

## Verification
Local: YAML validity, actionlint, the range logic, the CI-equivalent commands. After the PR is opened the owner reports the check results; fixes go in follow-up commits on this branch until green. After merge the owner confirms that `CI result` and `Analyze (csharp)` still appear as required checks and still gate a throwaway pull request.

## Out of scope
- Cache cleanup (review L1), artifact content (L3), PR base SHA (L4), exact SDK pinning (owner decision, `global.json`), Windows job, the CodeQL overlay-database notice, moving to Ubuntu 26, new jobs or scanners
- Whether the dependency review check reads NuGet versions under central package management: that is observed on a pull request that changes `Directory.Packages.props` (T-008b)

## Decisions already made (do not reopen)
- Linux runner, no secrets, no `pull_request_target`, no `paths` filters, SHA pinning, Gitleaks CLI with a hard-coded SHA-256, `CI result` and `Analyze (csharp)` as the required checks.
- Moving to Ubuntu 26 is a separate, deliberate task.

## Open questions to raise instead of deciding
- Where the full-history secret scan lives (deliverable 3) and whether it needs a baseline file.
- Whether `!cancelled()` leaves any case where the aggregate job is skipped and a required check shows as passing when it should not.
- Whether the scheduled run should also execute the secret scan (it is skipped on schedule today) and what that does to the aggregate job's accepted `skipped` cases.

## Report
Fill `docs/templates/task-report.template.md` in the PR description. No local paths, user names or machine details.
