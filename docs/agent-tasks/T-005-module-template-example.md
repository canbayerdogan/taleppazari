# T-005 — Module template and Example module

- **Phase:** 1
- **Status:** todo
- **Depends on:** T-002 (ADR-002..004 drafted), T-003, T-004
- **Approval:** required
- **Branch:** `task/T-005-module-template`

## Goal
Prove the module layout with one tiny `Example` module and document how to add a real module. The Example module is a reference and is removed when the first real module (Identity) exists.

## Context to read
1. `AGENTS.md`
2. `docs/adr/ADR-002-*.md`, `ADR-003-*.md`, `ADR-004-*.md` (Proposed)
3. `docs/templates/CONTEXT.template.md`
4. `src/Hosts/*`, `src/Shared/*`

## Allowed to change
- `src/Modules/Example/**`, `src/Shared/TalepPazari.SharedKernel/**` (minimal abstractions only)
- Host composition code that registers modules
- `docs/module-template/` (how to add a module), `AGENTS.md` Commands section if needed

## Must NOT change
- ADRs, other docs, `.github/*`

## Deliverables
- Projects `Contracts`, `Domain`, `Application`, `Infrastructure`, `Api` for `Example` following the proposed layout.
- Module registration hook used by both hosts.
- Own DbContext, schema `example`, one entity with Fluent configuration, one migration, one endpoint, one integration event contract.
- `src/Modules/Example/CONTEXT.md` filled from the template.
- `docs/module-template/how-to-add-a-module.md`: a checklist someone can follow in under 30 minutes.

## Acceptance criteria
- [ ] Endpoint works against the local PostgreSQL; the table exists in schema `example`.
- [ ] Only `Contracts` is referenced across module boundaries (nothing else exists to reference yet; show the reference graph).
- [ ] No Data Annotations in the module.
- [ ] Following the checklist, a second dummy module can be created without extra decisions (describe your dry run).

## Verification
Build, run migration, call the endpoint, run tests if any exist. Paste results.

## Out of scope
- Outbox/Inbox (T-007), architecture tests (T-006), API/observability conventions (T-008).

## Open questions to raise instead of deciding
- Anything in the Proposed ADRs that blocks implementation.
