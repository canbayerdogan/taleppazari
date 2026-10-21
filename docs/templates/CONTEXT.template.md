# <ModuleName> — module context

> Short on purpose (about half a page). Update it in every PR that changes the module's public surface, invariants or dependencies.

## Purpose
One or two sentences: what this module is responsible for.

## Owns
- Concepts, data and rules this module is the authority for.

## Does NOT own
- Things people might wrongly expect here, and which module owns them.

## Public surface (the only thing other modules may use)
- Contracts project: `TalepPazari.Modules.<Name>.Contracts`
- Query/command facades:
- Integration events published (name, version):
- Integration events consumed (name, from module):
- HTTP endpoints (route prefix):

## Allowed dependencies
- Modules (Contracts only):
- External:

## Data
- Schema name:
- Key tables/entities:

## Invariants and rules
- Rules that must always hold, and where they are enforced (domain, DB constraint, application).

## Key flows
- Short bullet list of the main use cases.

## Related ADRs and tasks
- ADR-XXX, T-XXX

## Gotchas for agents
- Non-obvious things that would waste time or break the module.

## Last verified
- Commit: <hash> — Date: <yyyy-mm-dd>
