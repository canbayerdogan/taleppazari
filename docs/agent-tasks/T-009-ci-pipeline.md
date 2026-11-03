# T-009 — CI pipeline

- **Phase:** 1
- **Status:** todo
- **Depends on:** T-003, T-006, T-007, T-008a, T-008a1 (extend after T-008b, T-010)
- **Approval:** required
- **Branch:** `task/T-009-ci`

## Goal
Every pull request is built, tested and scanned automatically, and a red check blocks merging. This exists because a bad merge once left `main` unbuildable and nothing caught it.

## Context to read
1. `AGENTS.md` (Commands)
2. `docs/repo-setup-checklist.md`
3. `.github/*`, `TalepPazari.slnx`, `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `compose.yaml`
4. `tests/**/*.csproj`, `tests/TalepPazari.IntegrationTests/Support/PostgresFixture.cs`, `tests/TalepPazari.ApiTests/Snapshots/OpenApiSnapshotTests.cs`
5. `docs/architecture/api-conventions.md` (snapshot update rule)

## Facts about the code today (verify, do not assume)
- Tests so far ran only on Windows. Architecture tests locate the repo root by `TalepPazari.slnx`, read compiled assemblies from `bin/<Configuration>/<TargetFramework>/` and use path separators; the integration and Api tests use Testcontainers. None has run on Linux.
- `TALEPPAZARI_REQUIRE_DOCKER=true` turns "Docker missing" into a failure instead of a skip. CI must set it.
- The OpenAPI snapshot update variable (`TALEPPAZARI_UPDATE_SNAPSHOTS`) is refused when `CI` is `true` or `1`. CI must never set it.
- `Directory.Build.props` sets `ContinuousIntegrationBuild` when `CI` or `GITHUB_ACTIONS` is true; warnings are errors, including NuGet audit advisories (NU1901-NU1904).
- `global.json` pins SDK 10.0.402 with `rollForward: latestFeature`.
- The integration and Api pagination tests pull `postgis/postgis:18-3.6` and the Testcontainers helper image (`testcontainers/ryuk`) from Docker Hub.
- `.github/dependabot.yml` has the `nuget` entry only.
- The agent cannot push, open a PR or see GitHub Actions results.

## Allowed to change
- `.github/workflows/**`, `.github/dependabot.yml`, `AGENTS.md` (Commands section)
- `tests/**` only for minimal fixes of Linux-specific problems (paths, line endings, case sensitivity) found while preparing the workflow. Propose each in your plan; every such change must be listed in the report.

## Must NOT change
- Anything under `src/`, ADRs, `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `docs/agent-tasks/README.md`
- Do not weaken or skip any test or architecture rule to make CI pass.

## Deliverables
1. **Build and test workflow:** restore, build (zero warnings), `dotnet format --verify-no-changes`, then architecture, API and integration tests. Integration and Api tests run on real PostgreSQL through Testcontainers with `TALEPPAZARI_REQUIRE_DOCKER=true`. Use the SDK from `global.json`. State which build configuration you use and why.
2. **Code scanning:** CodeQL for C#. State your build mode choice (for example autobuild, manual build, no build) with the trade-off.
3. **Dependency review** on pull requests, and a **secret scanning step** in addition to GitHub's push protection (compare options, see open questions).
4. **Dependabot:** add the `github-actions` entry (weekly, minor and patch grouped) to `.github/dependabot.yml`.
5. **Workflow hygiene:** least-privilege `permissions` on every workflow and job (default `contents: read`; elevate per job only where needed, with a comment saying why), `concurrency` cancellation for pull requests (not for `main`), NuGet package caching, `timeout-minutes` on every job, triggers `pull_request` (never `pull_request_target`) and `push` to `main`, no secrets required.
6. **Action pinning:** every third-party action is pinned by full commit SHA with a version comment (Dependabot keeps SHAs updated). For first-party `actions/*` and `github/*` actions propose tag or SHA and explain.
7. **Stable check names:** a short list of the exact check names so the owner can mark them required. Names must not change casually (renaming breaks required checks). Propose one aggregate job if it makes the required list stable.
8. `AGENTS.md` Commands: the CI-equivalent commands to run locally, and which environment variables CI sets.
9. A written first-run checklist for the owner (see Verification).

## Acceptance criteria
- [ ] Workflow files parse as valid YAML, and an action linter is run if a maintained, permissively licensed one is available (report which).
- [ ] The commands the workflow runs were executed locally in the same order with `CI=true` and `TALEPPAZARI_REQUIRE_DOCKER=true`; results pasted.
- [ ] Workflows request no more permissions than needed; no secrets in workflow files.
- [ ] Third-party actions are SHA-pinned; first-party pinning is explained.
- [ ] `TALEPPAZARI_UPDATE_SNAPSHOTS` is not set anywhere.
- [ ] Any change under `tests/**` is listed with its Linux-specific reason.
- [ ] Not verified, stated plainly: the workflow has not run on GitHub (the first run is the PR itself).

## Verification
You cannot run GitHub Actions. Do these locally and report: YAML validity, linter result, and the CI-equivalent commands. In the report, give the owner a first-run checklist. After the PR is opened, the owner pastes the check results and logs back, and fixes happen in follow-up commits on this branch until the first run is green. After merge the owner opens throwaway branches and shows: (a) a failing test turns the check red, (b) a duplicated `PackageVersion` entry fails at restore, (c) an architecture rule violation fails, (d) a clean PR is green. Also record how long a green run takes.

## Out of scope
- Deployment/CD, container publishing (Phase 4), repo map check (T-010), observability checks (T-008b), branch protection settings (owner, manual; see `docs/repo-setup-checklist.md`)
- A Windows or macOS job unless you propose one with its cost (see open questions)

## Decisions already made (do not reopen)
- GitHub-hosted Linux runner (`ubuntu-latest`) as the baseline.
- No secrets and no write permissions beyond `security-events: write` for CodeQL where needed.
- `pull_request`, never `pull_request_target`.
- Third-party actions are pinned by commit SHA.
- Integration tests are required, never skipped, in CI.

## Open questions to raise instead of deciding
- **Secret scanning tool.** Compare at least two on license (including any key or paid tier for personal or organization accounts) and maintenance; scan the PR diff or the full history? Recommend one and wait.
- **CodeQL build mode** and whether the repository's default CodeQL setup must be disabled first (owner setting) to avoid a duplicate.
- **Docker Hub pull limits** on shared runners for `postgis/postgis` and `testcontainers/ryuk`: how likely is throttling, and what mitigation works without secrets (cache, retry, a different registry)? Recommend one.
- **Dependabot cooldown:** Dependabot raised package versions within days of release, against the project's two-week rule. Check the current Dependabot docs for a `cooldown` option and propose values for `nuget` and `github-actions`.
- **A Windows job.** Developers use Windows; CI is Linux. Is a second job worth its minutes? Give cost and what it would catch.
- **Required-check naming and an aggregate job** (see deliverable 7).
- Anything in the Accepted ADRs or architecture tests that blocks a deliverable.

## Report
Fill `docs/templates/task-report.template.md` in the PR description. Do not include local paths, user names or machine details.
