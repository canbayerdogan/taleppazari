# AGENTS.md — Instructions for AI coding agents

Read this file fully, then `SUMMARY.md`. Keep this file short; details live in `docs/`.

## Language and conventions
- Code, comments, docs, commits and PRs are in English.
- Conventional Commits. Branch name: `task/T-XXX-short-slug`. One task = one branch = one PR.
- Root namespace: `TalepPazari`.

## Source of truth (highest first)
1. Code and passing tests
2. Accepted ADRs (`docs/adr/`)
3. Contracts (module `*.Contracts` projects, `contracts/`)
4. Module `CONTEXT.md` files
5. `SUMMARY.md`, `docs/`
6. Generated maps (`docs/generated/`): indexes only; verify against code when in doubt

If sources conflict, stop and report the conflict. Do not pick one silently.

## How to work
1. Read the assigned brief in `docs/agent-tasks/`. Read only the files it lists plus the `CONTEXT.md` of any module you touch.
2. Before editing, reply with: understanding, files you intend to change, plan, risks, open questions. Wait for approval unless the brief says `approval: not-required`.
3. Stay in scope. Respect "Allowed to change" and "Must NOT change". No drive-by refactors, no speculative abstractions. New dependency = justify need, license, maintenance.
4. If a decision is missing, list options with trade-offs and ask. Never invent permanent architecture decisions.
5. Run the verification commands. Report exact commands and results, and say plainly what you did NOT verify.
6. Update docs, `CONTEXT.md` and ADR drafts that your change affects.
7. Finish with the report from `docs/templates/task-report.template.md` in the PR description.

## Hard architecture rules (enforced by architecture tests once they exist)
- Modular monolith. Modules live in `src/Modules/<Name>/`.
- A module may reference another module only through that module's `Contracts` project.
- Each module owns its schema, DbContext and migrations. No cross-module SQL joins, foreign keys or shared DbContext.
- Cross-module state changes use integration events (outbox/inbox), never a shared transaction.
- The Domain layer does not depend on EF Core, HTTP, messaging or external providers.
- No Data Annotations for validation or mapping. Use EF Fluent API, boundary validation (FluentValidation or an approved alternative), domain invariants in entities, and DB constraints.
- Base types only where useful (`IEntity` for identity, audit fields only where needed).
- Business rules never live only in endpoints or in the frontend.

## Security and privacy (this repository may be public)
- Never commit secrets, keys, tokens, real credentials, real user data or personal data. Use placeholders and `.env.example`.
- Do not put exploit-level security details or infrastructure specifics into public docs.
- Passwords: one-way hash only. Personal data encryption follows the accepted ADR. Never write custom cryptography.
- Never log passwords, tokens, message bodies or unnecessary personal data.
- Authorize at the resource level on every endpoint. Never trust client-supplied owner IDs.

## Context efficiency
- Start from the task brief, this file, the module `CONTEXT.md` and `docs/generated/` maps (when present) before searching code.
- Do not read the whole repository. Use targeted search.
- If you needed context that was not provided, say what was missing in your report so the briefs can improve.
- Keep the "Context usage" section of your report honest (files read, searches, wasted effort).

## Commands
> Filled in by task T-003. Until then no build commands exist.

## Map
- `SUMMARY.md`: product and architecture overview
- `docs/roadmap.md`: phases and exit criteria
- `docs/agent-tasks/`: task briefs and status
- `docs/adr/`: architecture decisions
- `docs/templates/`: templates (task, ADR, CONTEXT, report)
- `src/Modules/*/CONTEXT.md`: per-module context
