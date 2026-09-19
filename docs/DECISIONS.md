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
