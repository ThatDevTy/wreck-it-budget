# Progress

**The board lives in Jira:**
https://thehyberbolictimechamber.atlassian.net/jira/software/projects/WIB/boards/2

Status, workflow and the backlog are tracked there (D-019). This file is a pointer and a
short summary, not a second board — if the two disagree, Jira is right about *status* and
the repo is right about *reasoning*.

---

## Where things stand

**Epic 1 · Environment Setup — complete (2026-09-19).** Git, .NET 10 SDK 10.0.401, Node 24 +
Angular CLI 22, SQL Server 2025 Express (`localhost\SQLEXPRESS`) + SSMS 22, VS Code
extensions, repo published at github.com/ThatDevTy/wreck-it-budget.

**Epic 2 · SQL Schema — complete (2026-09-29).** All six tables are agreed and documented in
[`schema.md`](schema.md) with a written rationale for every non-obvious column, and the DDL
has been run against a scratch database. Eleven tests passed: nine constraint violations
rejected with zero rows written, and both positive cases accepted — a debt paid down to
exactly 0 and cleared, and a debt carrying a $0 minimum payment.

**Epic 3 · Backend API — next (WIB-8).** Scaffold the project, write the six entity classes
from `schema.md`, generate the first EF Core migration. The migration was originally planned
under Epic 2; it moved here so the scaffold, the entities and the migration stay one
connected piece of work.

---

## How the work runs

- **The user writes the code.** Claude designs, explains, reviews and unblocks.
- One exception, agreed 2026-09-25: Claude writes the **styling and animation layer** — the
  CSS tokens, keyframes and SVG transcribed from the design reference. It is very high
  volume and close to zero learning, and offloading it is the cheapest way to buy calendar
  back without hollowing out the git history.
- **Definition of Done:** every acceptance criterion checked · the test plan run, with output
  shared · the diff reviewed · committed with a message the user wrote · the Jira issue moved.

---

## Documentation in this repo

- [`schema.md`](schema.md) — the six tables and why each non-obvious column is the way it is
- [`DECISIONS.md`](DECISIONS.md) — D-001 onward, the decisions that shape the project
- [`handoff/`](handoff/) — the original design brief and visual spec
