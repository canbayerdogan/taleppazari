# T-010 — Repo map generator

- **Phase:** 1
- **Status:** todo
- **Depends on:** T-005, T-006, T-007, T-008, T-009
- **Approval:** required
- **Branch:** `task/T-010-repo-map`

## Goal
Generate small, deterministic maps of the codebase so agents can orient themselves without exploring. A CI check keeps them honest.

## Context to read
1. `AGENTS.md`
2. `docs/adr/ADR-002-*.md`, `ADR-003-*.md`
3. `src/Modules/**` (structure, `CONTEXT.md`), `.github/workflows/**`

## Allowed to change
- `tools/RepoMap/**`, `docs/generated/**`, `.github/workflows/**` (add the check), `AGENTS.md` (Map and Commands sections)

## Must NOT change
- Module code, ADRs

## Deliverables
A tool that writes to `docs/generated/`:
- `modules.md`: modules, their public `Contracts`, and inter-module references.
- `events.md`: integration events, publisher module and consumers.
- `endpoints.md`: HTTP routes per module.
- `schemas.md`: schema and tables per module (from the EF model).
Outputs are sorted and contain no timestamps or machine-specific paths, so identical code yields identical files. Each file states that it is generated and how to regenerate it.
A CI job regenerates the maps and fails the PR if they differ from the committed files.

## Acceptance criteria
- [ ] Running the tool twice produces no diff.
- [ ] Adding a new endpoint or event without regenerating makes CI fail.
- [ ] Each file stays small (target: a few hundred lines at most for the current codebase).
- [ ] Output marks anything inferred rather than certain (for example reflection-based or dynamic registrations).

## Verification
Run the tool, commit the output, then make a deliberate change and show CI catching the stale map.

## Out of scope
- Graph databases, semantic search, embeddings, MCP servers. Decide those later from measured need, using the "Context usage" sections of the task reports.

## Open questions to raise instead of deciding
- Reflection-based versus Roslyn-based analysis: compare effort, accuracy and maintenance; recommend one and wait for approval.
