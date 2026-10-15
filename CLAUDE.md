# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Wreck-It Budget — a household budgeting app for two people, styled as an 80s arcade cabinet.
Debts are pixel-art towers that shrink as they're paid down; bills are windows in a house that
light up when paid. ASP.NET Core (.NET 10) + EF Core + SQL Server, with an Angular frontend
not yet started. LAN-only, for one household.

It is a rewrite of a working Python/FastAPI + JSON-file app, so the domain is proven and this
pass is about doing it properly.

## How the work runs

**The repository owner writes the implementation code.** Claude designs, explains, reviews
and unblocks — it does not write application code by default. This is a learning project
whose point is the owner's own experience, so handing over the code defeats it.

Two standing exceptions:

- **The CSS token file and animation keyframes** (D-021). High volume, transcribed from a
  design spec that already states the values, close to zero learning.
- **When the owner explicitly asks.** They have, twice, to catch up on time. Both times they
  asked for heavy commenting so the result could be studied. Treat that as a request to
  honour, not a new default — the next ticket goes back to them writing it.

**Explain at roughly an eighth-grade level.** Define every term the first time it appears,
walk code top to bottom, and give the reasoning behind a choice rather than just the choice.
The owner is learning C#, TypeScript and architecture from zero, and has twice had to ask for
the level to come down.

**Claude does not run `git commit` or `git push` here**, and never adds Claude attribution to
a commit message. The owner makes every commit. Say when something is ready and stop.

**Jira ticket status is moved in a batch at end of day**, not during the work.

## Sources of truth

Four, and they do not overlap:

| | |
|---|---|
| [`docs/schema.md`](docs/schema.md) | The database. Every table, every column, and why each non-obvious one is the way it is. If this and the database disagree, **this file is what gets fixed.** |
| [`docs/DECISIONS.md`](docs/DECISIONS.md) | Numbered decisions D-001 onward. Superseded entries are marked, never deleted. Cite the number when code depends on one. |
| [`docs/handoff/`](docs/handoff/) | The original design brief and visual spec. `design-ref/Wreck-It Budget.dc.html` is a working prototype — high fidelity, exact values are final, but it is a reference to recreate, not code to port. |
| Jira | Status, sprints, the backlog. Project key `WIB` at thehyberbolictimechamber.atlassian.net. Issue descriptions carry the acceptance criteria and test plan. |

Decision records stay in the repo rather than Jira (D-019): `schema.md` cites D-numbers
inline, the repo is public and doubles as a portfolio, and a free Atlassian site can lapse.

## Commands

Run from the repository root:

```bash
dotnet run --project src/WreckItBudget.Api    # starts on http://localhost:5120
dotnet build
```

EF Core, from `src/WreckItBudget.Api`:

```bash
dotnet ef migrations add <Name>
dotnet ef database update
```

The connection string is in user secrets, never in a committed file. Database is
`WreckItBudget` on `localhost\SQLEXPRESS`.

**The app locks its own .exe while running.** A build that fails with "the process cannot
access the file ... because it is being used by another process" means the app is still up —
stop it, don't debug it. `dotnet build -t:CoreCompile` compiles without the copy step and is
useful for checking C# while the app stays running.

## Tests

No automated test project yet (WIB-50 onward). Verification is PowerShell and SQL scripts in
[`tools/`](tools/), run against a live local instance:

```powershell
.\tools\api-test.ps1       # ~72 checks across income, bills and debts - behaviour
.\tools\summary-test.ps1   # Family Score arithmetic, to the penny
```

`api-test.ps1` checks *behaviour* — did the right status code come back, with a useful
message. `summary-test.ps1` checks *arithmetic* against figures a human can verify by hand.
Both clean up after themselves.

**`summary-test.ps1` wipes income, bills and debts before seeding.** Safe until WIB-29, when
real household data gets entered. Not after.

`tools/schema-validation.sql` and `tools/wib8-verify.sql` prove the schema and the
EF-generated database enforce what `schema.md` says. Worth re-running after any migration.

## Architecture

Request → Controller → `WreckItBudgetContext` → SQL Server. No service layer; the controllers
are thin enough not to need one yet.

**Entities are never exposed over HTTP.** Every endpoint takes a request record and returns a
response record from `Models/`. Create and update requests are separate types even when
identical today — they diverge the moment one needs a field the other must not have, and
`UpdateIncomeRequest` already has `IsActive` where the create one doesn't. The response
shapes also resolve relationships: `IncomeResponse` carries `OwnerName`, not a bare id plus
an always-null navigation property.

**Validation runs in the handler, and the database is the net underneath.** Every `CHECK`
constraint in `schema.md` has a matching check in a controller. The constraint exists to
catch Claude's and the owner's bugs; the handler check exists so a user gets an actionable
400 instead of a 500 and a stack trace. A constraint violation reaching the client is a bug.

Status codes carry meaning and are used consistently:

- **400** — the request is nonsense and always would be
- **404** — no such id
- **409** — the request is fine but clashes with current state (duplicate name; deleting
  something that has history)
- **204** — done, nothing to say (PUT, DELETE)

**Reads project, writes find.** `.Select(...)` before `.ToListAsync()` lets EF build the SQL,
add any join, and fetch only the named columns — but the result is *not tracked*. `PUT` uses
`FindAsync` instead, because assigning to a tracked entity is what produces an `UPDATE`.
Where C# logic has to run (the monthly normalisation in `SummaryController`), `.ToListAsync()`
comes first, because EF cannot translate a C# method into SQL.

**Derive state from facts, don't accept claims about state.** A caller never sends
`IsCleared` on a debt — it sets a balance, and the server works out what that means. Server-
owned fields (`ColorIndex`, `Priority`, `IsCleared`, `ClearedAt`) are simply absent from the
request records, which is how nobody can set them wrong.

## Things that bite

**Zero means opposite things in different places.** `Bills.AmountDue` must be **> 0** — a
zero-value bill is a typo. `Debts.CurrentBalance` must be **>= 0** — zero is the goal, and a
`> 0` rule there would make it impossible to ever pay a debt off. Also legitimate: 0% APR
cards, $0 minimum payments on deferred loans, $0 total debt.

**Day-of-month columns accept 1–31, deliberately including 29, 30 and 31.** Whether the 31st
exists depends on the month, which a row cannot know, so it is resolved at display time by
clamping to the last day. Do not add validation that rejects them.

**Money is `decimal`, never `double` or `float`**, and precision is configured on the
`DbContext` — a C# `decimal` carries no precision, so EF defaults everything to
`decimal(18,2)` unless told otherwise.

**Pay frequency is not intuition.** Biweekly is 26 cheques a year, not 24. Monthly figures
are `annual / 12`, never "amount × 4". And `amount * 52 / 12` is correct where
`amount * (52 / 12)` is whole-number division that silently loses 8%.

**Some values must be captured when they happen, because their source changes.**
`SpendingLogEntries.MinPaymentAtTime` (D-017) freezes the minimum a payment was judged
against; without it, editing a debt's minimum silently rewrites streak history. `Snapshots`
exists for the same reason at table scale. The test: *if the source changes, can I still get
this value back?* No means store it. Yes means derive it — which is why the Family Score is
computed and never stored.

## Design rules that are not negotiable

These come from the design brief and are what make the app feel like itself. They are
invisible in a screenshot, so they only survive if they're written down:

- The Family Score **can be negative, and is shown that way.** No clamping, no absolute value.
- A debt balance going **up** must never read as progress — it gets its own distinct animation.
- Mary (the Fixer) only ever turns bill lights **on**. The cycle reset is silent and automatic,
  never framed as a loss.
- A cleared debt stays on the skyline permanently as a trophy. It is not removed.
- Cumulative tower damage only ever increases, independent of balance.
- The app never punishes a bad month.
