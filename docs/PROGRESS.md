# Progress

**Current issue:** WIB-7 · Spike: design the database schema (Epic 2, starts next session)

_Initial board drafted by Claude from the approved project plan, 2026-09-19._

---

## How this board works

- **Keys:** `WIB-n`, numbered sequentially across the whole project.
- **Epics:** one per stage. An epic's issues are created when the epic starts, not in advance.
- **Types:** Story (a user-facing feature) · Task (technical work) · Bug (a defect found after
  Done) · Spike (time-boxed research whose output is a decision).
- **Workflow:** To Do → In Progress → In Review → Done, plus Blocked.
- **Points:** Fibonacci 1 / 2 / 3 / 5 / 8. Anything bigger gets split.

### Definition of Done
An issue is Done when:
- [ ] every acceptance criterion is checked
- [ ] the test plan was run by me, with the output shared
- [ ] the diff was reviewed
- [ ] it's committed with a message I wrote
- [ ] this board is updated

---

## Epic 1 · Environment Setup

| Key | Summary | Type | Status |
|---|---|---|---|
| WIB-1 | Install Git and configure identity | Task | Done |
| WIB-2 | Install .NET 10 SDK (10.0.401) | Task | Done |
| WIB-3 | Install Node 24 LTS and Angular CLI 22 | Task | Done |
| WIB-4 | Install SQL Server 2025 Express and SSMS 22 | Task | Done |
| WIB-5 | Install VS Code extensions (C# Dev Kit, Angular Language Service) | Task | Done |
| WIB-6 | Create the repo, make the first commit, publish to GitHub (public) | Task | Done |

**Epic 1 complete — 2026-09-19.**

## Epic 2 · SQL Schema — _next_

| Key | Summary | Type | Status |
|---|---|---|---|
| WIB-7 | Design the database schema (tables, columns, keys, relationships) | Spike | To Do |
Users, IncomeSources, Bills, Debts, SpendingLogEntries, MonthlySnapshots. First EF Core
migration.

## Epic 3 · Backend API — _not started_
Scaffold, auth, CRUD for income/bills/debts, priority reorder, spending log with
auto-snapshot, summary, payoff calculator, trend.

## Epic 4 · Frontend — _not started_
Scaffold and global styles, login and auth, Setup, Dashboard (score, towers, Bill House),
Log Entry, Trend Report, damage layer Spike, Tower Down sequence.

## Epic 5 · Testing — _not started_

## Epic 6 · QA — _not started_

## Epic 7 · LAN Deployment — _not started_

## Epic 8 · Maintenance — _not started_

---

## Blocked
_Nothing._
