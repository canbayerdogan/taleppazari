# Repository setup checklist (manual, owner only)

These are GitHub settings an agent cannot do for you. Tick them once.

## Account and repository
- [ ] Two-factor authentication enabled on the GitHub account
- [ ] Repository created, default branch `main`
- [ ] **Decision needed:** license (MIT / Apache-2.0 / AGPL / none = all rights reserved). Add `LICENSE` only after deciding
- [ ] Visibility decision recorded (public now; can be switched later)

## Protection (Settings > Rules / Branches)
- [ ] Require a pull request before merging to `main`
- [ ] Require status checks to pass (add the check names from T-009 once CI exists)
- [ ] Block force pushes and branch deletion on `main`
- [ ] Squash merge only; automatically delete head branches after merge

## Security (Settings > Code security)
- [ ] Secret scanning and **push protection** enabled
- [ ] Dependabot alerts and security updates enabled
- [ ] Private vulnerability reporting enabled
- [ ] Code scanning (CodeQL) enabled or added through T-009

## Habits
- [ ] Never paste secrets into issues, PRs, prompts or commits. If one leaks, rotate it immediately: deleting the commit is not enough
- [ ] Use fake data only in tests and fixtures
- [ ] Review every PR yourself before merging; agents never merge their own work
