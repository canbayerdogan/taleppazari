# T-003 — Solution skeleton and hosts

- **Phase:** 1
- **Status:** todo
- **Depends on:** T-001
- **Approval:** required
- **Branch:** `task/T-003-solution-skeleton`

## Goal
Create the empty .NET 10 solution with the Api and Worker hosts and shared build settings. No business code.

## Context to read
1. `AGENTS.md`
2. `SUMMARY.md` (sections 3 and 4)

## Allowed to change
- `global.json`, `Directory.Build.props`, `Directory.Packages.props`, solution file
- `src/Hosts/TalepPazari.Api/`, `src/Hosts/TalepPazari.Worker/`, `src/Shared/TalepPazari.SharedKernel/`
- `AGENTS.md` — only the "Commands" section

## Must NOT change
- Docs other than the Commands section, `.github/*`

## Deliverables
- `global.json` pinning a .NET 10 SDK with a patch roll-forward policy.
- `Directory.Build.props`: nullable enabled, warnings as errors, analyzers on, deterministic builds.
- Central package management via `Directory.Packages.props`.
- Solution file in the SDK's default format (mention which one in your report).
- Api host: minimal host exposing a liveness endpoint.
- Worker host: background host skeleton that starts and logs.
- SharedKernel: empty project (content arrives in T-005/T-007).
- Commands section in `AGENTS.md` with the exact build/run/test commands.

## Acceptance criteria
- [ ] `dotnet build` succeeds with zero warnings from a clean clone.
- [ ] Api and Worker start; the Api liveness endpoint returns 200.
- [ ] No feature code and no dependencies beyond what the hosts need.

## Verification
Run build, then start each host and call the liveness endpoint. Paste results.

## Out of scope
- Database, Docker, modules, CI, logging setup (T-004, T-005, T-008, T-009).

## Open questions to raise instead of deciding
- SDK version policy if the exact .NET 10 patch is not available locally.
