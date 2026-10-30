# Decisions

A running log of decisions that shape this project: what was decided, when, and why.
New decisions are appended; superseded ones are marked, not deleted.

_Initial entries drafted by Claude from the approved project plan, 2026-09-19._

---

## D-001 · Stack
**2026-09-19** — C# / ASP.NET Core Web API (.NET 10 LTS), Entity Framework Core, SQL Server,
Angular / TypeScript, RxJS. Replaces the as-built Python/FastAPI + vanilla JS + JSON file.
**Why:** real, defensible experience in a mainstream stack for a job search. This project is
also the reference for Rebound, which doesn't exist yet and will inherit its conventions.

## D-002 · Setup is a flat page, not a wizard
**2026-09-19** — Setup is a single scrolling page with Accounts → Income → Bills → Debts cards,
all visible at once. No stages, no skyline preview, no characters.
**Why:** the brief's 3-stage wizard was superseded during design iteration (see
`handoff/README.md`, "Known deviation").

## D-003 · Authentication shape
**2026-09-19** — Exactly two accounts, each with its own password, sharing all household data.
No roles, no permissions, no per-user data isolation, no password reset or email flow.
**No `household_id` or any grouping table, ever.** This is a single-household app. The
invariant is documented here instead of modeled in the schema.
**Why:** real identity replaces the hardcoded "me"/"spouse" labels, and nothing more.

## D-004 · Accounts are created on the Setup page
**2026-09-19** — The Setup page gets an Accounts card at the top. Both logins are created there
on first run. The login screen is designed in the existing arcade visual language.
**Why:** keeps first run to one screen, consistent with D-002.

## D-005 · "Logged by" defaults from the session, and can be overridden
**2026-09-19** — Log entries default to the logged-in user. The designed Me/Spouse toggle stays
so either person can log on the other's behalf.

## D-006 · Income owner is a foreign key to Users
**2026-09-19** — `IncomeSources.OwnerUserId` references `Users`. The UI shows real account
names instead of "Me"/"Spouse".

## D-007 · Streak = consecutive payments above the minimum
**2026-09-19** — The payment streak counts consecutive debt payments logged *above* the minimum
due, and resets to 0 on any payment at or below the minimum. Icons: `·` (0), `⚡` (1–5),
`👑` (6+). It is computed from the spending log, not stored.
**Superseded:** the brief's "consecutive months with a positive family score" definition.
Do not build it.

## D-008 · Single Bill House
**2026-09-19** — Bills are shown as one house with one window per bill, lit when that bill is
paid for the current cycle.
**Superseded:** the brief's one-building-per-bill, floor-by-floor treatment.

## D-009 · Cumulative damage layer ships in v1.0
**2026-09-19** — Debt towers get a damage layer (cracks, boarded windows, tilted signage) that
only ever increases. There is no reference art for it, so it gets its own design Spike first.

## D-010 · Fresh start, no data migration
**2026-09-19** — The existing `budget_data.json` is not imported. Data is re-entered through
Setup, which doubles as the first end-to-end test.

## D-011 · One SQL Server Express instance
**2026-09-19** — SQL Server 2025 Express, instance `localhost\SQLEXPRESS`, used for both
development and the LAN deployment.
**Supersedes:** the original "LocalDB for dev, Express for deploy" split. The deploy box is this
machine, and winget has no standalone LocalDB package.

## D-012 · `TrustServerCertificate=True` is localhost-only
**2026-09-19** — Local connections (SSMS and the API's connection string) skip certificate
validation, because Express only has a self-signed certificate. Traffic is still encrypted,
just not authenticated.
**Why it's acceptable:** every database connection is to localhost and never crosses a
network. Only the API is exposed on the LAN.
**Revisit if:** the database moves to another machine, or hosting ever goes beyond the LAN.
At that point, install a trusted certificate and remove this flag.

## D-013 · LAN only. Hosting is a separate decision
**2026-09-19** — v1.0 runs on the home network only. Adding logins does **not** mean deploying
publicly. HTTPS, secrets management, backups, and uptime belong to a separate, future
conversation.

## D-014 · Out of scope, permanently
**2026-09-19** — PWA installability and Tailscale/remote access are off the table for this
project. Don't add them to the backlog.

## D-015 · Theming: tokens file only
**2026-09-19** — Colors live as CSS custom properties in a single tokens file, because we need
a palette file regardless and it costs nothing. No theme service, no runtime theme switching,
no persisted user themes.

## D-016 · Function first, then polish, per screen
**2026-09-19** — Each screen gets a function issue (layout, data, typography), then its own
animation/polish issue, before work moves to the next screen.

## D-017 · The minimum payment is stamped onto each debt payment
**2026-09-24** — `SpendingLogEntries.MinPaymentAtTime` records the debt's minimum at the
moment a payment is logged. The streak (D-007) compares against that value, never against
`Debts.MinPayment`.
**Why:** `Debts.MinPayment` is always *today's* figure. Raise it — as you should when a
balance grows — and every past payment is re-judged against a bar that didn't exist when it
was made, so earned streaks vanish silently. Lower it and unearned ones appear. The streak is
the app's game layer; a number that rewrites its own history is worse than no number.
This is not duplicated data: "the minimum now" and "the minimum then" are different facts,
and the second becomes unrecoverable the moment the debt row is edited. Same reasoning as
`Snapshots`. D-007 still holds — the streak is computed, never stored; only its input is
frozen.

## D-018 · `Snapshots`, not `MonthlySnapshots`
**2026-09-24** — The table is append-only, with a row written on every log submission plus
any manual Capture, so there are many rows per month. "Monthly" described how the trend
report reads them, not what the table holds.

## D-019 · Jira owns status. The repo owns reasoning.
**2026-09-24** — Issue tracking moves to a Jira project (key `WIB`, so the existing WIB-n
references stay correct). `PROGRESS.md` shrinks to a pointer and `BACKLOG.md` folds into the
Jira backlog. **`DECISIONS.md` and `schema.md` stay in the repo.**
**Why:** decision records belong next to the code they constrain — `schema.md` cites D-003,
D-006, D-007 and D-017 inline, and those links only work if both files live together. They're
also versioned and diffable, so a superseded decision shows its history. And this repo is
public and doubles as a portfolio: reasoning visible in the repo is reasoning a reader
actually finds, where a ticket tracker behind a login is not. Keeping architecture decision
records in-repo is the common convention, not a workaround.
**Also:** a free Atlassian site can be deactivated after prolonged inactivity. The repo is on
disk and on GitHub.

## D-020 · Two releases: `0.5 Tracker` and `1.0`. The 10/1 target is dropped.
**2026-09-25** — v1.0 was going to be "in production by 1 October." That is not achievable and
has been replaced with two dated releases in Jira:

- **`0.5 Tracker` — due 8 November.** A usable budget tracker: enter income, bills and debts,
  log spending, see a true Family Score. No auth, no characters, no animation, no charts.
  ~57 hours of work.
- **`1.0` — due 31 January 2027.** The full arcade experience, LAN-deployed. ~183 hours total.

**Why:** the remaining work is roughly 183 hours at a learning pace, against a stated capacity
of one to two hours nightly — about ten hours a week. Six days was never going to be 10% of
it. The estimate assumes no prior C#, TypeScript or Angular, and that the user writes the
code (see D-021 for the one exception). It should compress as fluency arrives, so mid-January
is a realistic stretch on 1.0, but the committed date is the honest one.

**The two things most likely to move these dates**, flagged so they are not a surprise: auth
(WIB-31, WIB-32), which is conceptually heavy and where beginners lose a week without
noticing; and the payoff calculator (WIB-36), the only genuinely algorithmic piece.

**Revisit** after the `0.5 Tracker` release. Actual velocity over six weeks beats any estimate
made before a line of C# exists.

## D-021 · Claude writes the styling and animation layer. The user writes everything else.
**2026-09-25** — The CSS token file, keyframe animations, sprite-sheet handling and inline SVG
transcribed from the design reference are written by Claude. All C#, all Angular component
logic, all API code and all schema work stay with the user.

**Why:** the arcade layer is hundreds of lines of exact hex values and timing functions
transcribed from a spec that already states them. It is enormous in volume and close to zero
in learning — hours spent copying `#ffd36e` teach nothing about either stack. Offloading it
buys back most of the calendar without hollowing out the git history, which is the thing this
project exists to produce. Handing over domain logic would do the opposite.

**Boundary:** Claude writes the animation *definitions*; the user wires them to the events
that trigger them. That wiring is real Angular work and stays theirs.

## D-022 · Jira board conventions
**2026-09-25** — How the board is arranged, recorded so it stays consistent:

- **Issues are created before epics.** Epics consume keys from the same sequence, so creating
  them first would have pushed WIB-1 to WIB-9. Work items are WIB-1 to WIB-8 and WIB-23
  onward; the eight epics occupy WIB-9 to WIB-16 purely because they were created second.
  **Jira keys cannot be renumbered or reused** — they are assigned on creation and permanent.
- **Board order comes from rank, not key.** Every issue is ranked in execution order, so the
  board reads top to bottom as the plan regardless of its number.
- **Dependencies are `blocks` links**, not conventions in prose. 52 of them describe the real
  critical path.
- **Story points are Fibonacci and sized for a learner**, not for an experienced developer.
  Hour estimates appear at the bottom of each description, because hours are what actually
  plan an evening.
- **Every card carries the same four sections:** context, acceptance criteria, test plan, and
  what is explicitly *not* in the issue.

## D-023 · Controllers on the backend, standalone components on the frontend
**2026-10-15 / 2026-10-30** — ASP.NET Core endpoints are written as **controller classes**,
not minimal APIs. Angular components are **standalone**, not declared in NgModules.

**Why controllers:** the owner's workplace uses them, so every hour spent in that style
compounds into something read every day. Minimal APIs are less ceremony and would have been
the lighter choice for a project this size, but matching the real target won.

**Why standalone:** it is what Angular CLI 22 produces by default, so NgModules would mean
opting out of the tooling's own direction. It is also one fewer concept to learn — an
NgModule exists purely as bookkeeping, registering components somewhere other than the file
they live in. And it fails better: a missing standalone import points at the component's own
file, where a missing NgModule declaration surfaces as an unrecognised element in a template.

**Note these two answers point opposite ways on purpose.** Controllers and minimal APIs are
both live, supported, equal choices in ASP.NET Core, so matching the workplace is free.
NgModules are a legacy path Angular is actively migrating away from, so learning them first
would mean learning the thing you later convert *away* from. Standalone to NgModules is also
the easier direction to travel, because you already understand what the module was doing.

**Revisit if:** the owner lands somewhere whose Angular codebase is NgModule-based and
wants the practice. The conversion is mechanical.

## D-024 · Angular project conventions
**2026-10-30** — Three decisions taken before the first service was written:

**The API base URL lives in Angular `environment` files**, not hardcoded and not in a shared
constant. `ng generate environments` creates `environment.ts` and
`environment.production.ts`; the CLI swaps the file at production build time. **Why:** WIB-55
deploys to the LAN on a different address, and a committed constant would have to be edited
to deploy and remembered back afterwards — exactly the mistake environments exist to prevent.

**Interfaces live in one shared `src/app/models/` folder**, mirroring the API's C# `Models/`
folder rather than sitting beside each service. **Why:** these types exist only to mirror the
API contract, so when the C# changes the TypeScript change should be findable in one step.
Matching names on both sides (`IncomeResponse` ↔ `IncomeResponse`) makes drift obvious.

**No component calls `HttpClient` directly.** One service per resource, and every screen goes
through it. **Why:** otherwise the URL for fetching debts ends up written in four components
and changing it means finding all four.
