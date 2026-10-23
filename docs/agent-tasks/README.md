# Agent tasks

One brief per task. One task = one branch = one PR. Only the owner merges.

## Status (the owner updates this table after merges; agents do not edit it)

| ID | Title | Depends on | Approval | Status |
|---|---|---|---|---|
| T-001 | Repository hygiene files | — | not-required | done |
| T-002 | Draft ADR-001..007 | T-001 | required | done |
| T-003 | Solution skeleton and hosts | T-001 | required | done |
| T-004 | Local development environment | T-003 | required | done |
| T-005 | Module template and Example module | T-003, T-004 (ADR-002..004 Accepted) | required | todo |
| T-006 | Architecture tests | T-005 | required | todo |
| T-007 | Outbox/Inbox and event dispatch | T-004, T-005, T-006 | required | todo |
| T-008 | API and observability standards | T-003, T-004, T-005, T-006 | required | todo |
| T-009 | CI pipeline | T-003 (extend after T-006..T-008) | required | todo |
| T-010 | Repo map generator | T-005..T-009 | required | todo |

Suggested order: T-005, then T-006, then T-007 and T-008 in parallel, then T-009, then T-010.
Parallel tasks must not touch the same files. If they do, run them in sequence.
If parallel PRs both edit `Directory.Packages.props` or the `AGENTS.md` Commands section, rebase the later one onto `main`, keep both entries and do not reformat.

## Phase 1 exit review
After T-010 the owner and architect review against the Phase 1 exit criteria in `docs/roadmap.md`. Phase 2 briefs are written only after that review.

## How to start an agent session (paste this, replace the task id)

```
Read AGENTS.md, then docs/agent-tasks/T-XXX-*.md. Do not edit anything yet.
Reply with: your understanding, the files you will change, a step plan, risks, and open questions.
Wait for my approval before making changes.
```

For tasks marked `approval: not-required`, the agent may proceed after posting its plan.

## After the agent finishes
1. Read the PR description report (verification, not-verified, open questions).
2. Run the verification commands yourself at least once for each task.
3. Ask a second, fresh agent session to review the PR against the brief, accepted ADRs and `AGENTS.md`.
4. Merge only when CI is green and you are satisfied.
