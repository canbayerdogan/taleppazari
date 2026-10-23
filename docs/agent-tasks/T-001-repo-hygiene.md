# T-001 — Repository hygiene files

- **Phase:** 1
- **Status:** done
- **Depends on:** — (bootstrap pack already committed by the owner)
- **Approval:** not-required
- **Branch:** `task/T-001-repo-hygiene`

## Goal
Add the standard repository files so every later task starts from a safe, consistent base. No source code, no solution files.

## Context to read
1. `AGENTS.md`
2. `SUMMARY.md` (sections 4 and 5)
3. `docs/repo-setup-checklist.md` (manual settings; do not duplicate them)

## Allowed to change
- `.gitignore`, `.gitattributes`, `.editorconfig`
- `.github/pull_request_template.md`, `.github/ISSUE_TEMPLATE/*`, `.github/dependabot.yml`, `.github/CODEOWNERS`
- `SECURITY.md`

## Must NOT change
- `SUMMARY.md`, `AGENTS.md`, `CLAUDE.md`, `docs/adr/*`
- Any `.cs`, `.csproj`, `.sln*` file (do not create them)

## Deliverables
- `.gitignore` for .NET, Node/TypeScript, IDEs and OS files. It must also ignore `.env`, `.env.*` (except `.env.example`), key/certificate files (`*.pfx`, `*.pem`, `*.key`), and local settings files such as `appsettings.*.local.json`.
- `.gitattributes` normalizing line endings to LF for text files.
- `.editorconfig` with a minimal C# baseline (file-scoped namespaces, nullable awareness, 4-space indent, final newline). Keep it small; do not encode debatable style opinions.
- `.github/pull_request_template.md` based on `docs/templates/task-report.template.md`.
- An issue template for agent tasks pointing to `docs/templates/agent-task.template.md`.
- `.github/dependabot.yml` for `nuget`, `github-actions` and `npm` (weekly, grouped, minor/patch).
- `.github/CODEOWNERS` with a placeholder for the owner's GitHub handle (ask the owner for the real handle).
- `SECURITY.md` explaining how to report vulnerabilities privately. No internal architecture or infrastructure details.

## Acceptance criteria
- [ ] `git check-ignore -v .env secrets.pem appsettings.Development.local.json` shows each path as ignored; `.env.example` is not ignored.
- [ ] PR template contains the Verification, Not verified and Context usage sections.
- [ ] `dependabot.yml` is valid YAML and lists the three ecosystems.
- [ ] No file contains secrets, real names or real e-mail addresses (except the placeholder handle).

## Verification
Run the `git check-ignore` command above and a YAML parse of `dependabot.yml`. Paste results in the report.

## Out of scope
- Branch protection and security settings (manual, see checklist), LICENSE, CI workflows (T-009).

## Open questions to raise instead of deciding
- Owner's GitHub handle for CODEOWNERS.
