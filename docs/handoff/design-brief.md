# WRECK-IT BUDGET — DESIGN BRIEF
**Status:** App fully built and functional. This brief reverse-engineers the existing codebase into a feature spec so Claude Design can work from structured requirements rather than raw source, ahead of a visual rewrite.

## PROJECT OVERVIEW
A family budgeting app that runs locally — no accounts, no cloud, no logins. Two people (labeled "me" / "spouse") log income, bills, and debts to a single JSON file on the machine. Wrapped in an arcade-cabinet visual theme: pixel-art buildings represent bills and debts, a pixel-art "wrecker" character animates debt payments by swinging at and shrinking debt towers.

## TECH STACK (existing, as-built)
- Backend: Python / FastAPI
- Server: uvicorn
- Validation: Pydantic
- Date math: python-dateutil
- Frontend: vanilla HTML/CSS/JS (single-page app, no framework)
- Storage: single local JSON file (`budget_data.json`) — no database
- Runs via `python app.py`, opens default browser automatically at `localhost:5000`, also reachable on LAN for other devices (phones)

## TARGET STACK FOR REWRITE — LOCKED
Rewrite moves off the Python/vanilla-JS stack entirely, matching Rebound's stack for consistency across both personal projects:
- Backend: C# / ASP.NET Core Web API
- Frontend: Angular / TypeScript
- Reactive layer: RxJS
- Database: SQL (replaces the single local JSON file — real schema needed for income sources, bills, debts, spending log, and monthly snapshots)
- ORM: Entity Framework Core
- Personal/family use — no multi-user auth needed beyond the existing "me"/"spouse" owner labels already in the data model
- LAN reachability (phones on the same WiFi) carries forward as a requirement from the as-built version

## DATA MODEL (as-built)
**Income Source:** name, owner ("me"/"spouse"), pay_type ("twice_monthly"/"weekly"), amount_per_check, pay_dates or pay_weekday, optional start_date (for future-dated income), active flag

**Bill:** name, amount_due, due_day, pay_cycle ("monthly"/"weekly"/"yearly"), category

**Debt:** name, balance, interest_rate (APR), minimum_payment, due_day, optional statement_day, priority (auto-assigned if not given, user-reorderable)

**Spending Log Entry:** date, category ("groceries", "bill", "debt_payment", etc.), optional linked_id (ties entry to a specific bill/debt), planned_amount, actual_amount, logged_by ("me"/"spouse"), notes

**Monthly Snapshot:** month (YYYY-MM), total_debt_balance, total_bills_paid, total_debt_paid, total_other_spending, family_score, captured_at timestamp — one snapshot per month, overwritten (not duplicated) if re-captured mid-month

## SCREENS / STRUCTURE (as-built)

### 1. First-Time Setup Wizard
Three stages, run once:
- **Stage 1 — Power Sources (Income):** add income streams, supports multiple, including future-dated spouse income
- **Stage 2 — Bill Buildings (Recurring):** add recurring bills
- **Stage 3 — Debt Towers (Bosses):** add debts, auto-assigned priority order
- "Finish Setup" marks setup complete and routes to the main dashboard permanently (setup wizard never shown again unless data file is deleted)

### 2. Main Dashboard
- **Status Report panel:** summary numbers — monthly income, monthly bills, monthly minimum debt payments, total debt balance, estimated monthly extra cash, and the headline **Family Score** (income minus bills minus minimum debt payments — positive = winning, negative = losing, styled as an arcade score)
- **Debt Towers:** visual buildings per debt, draggable to reorder priority — priority order directly drives the payoff calculator's snowball-style rollover logic
- **Bill Buildings:** visual buildings per bill, smaller/gentler visual treatment than debt towers (no "boss fight" framing, just a diagonal "wrecked" fill showing percent paid)
- **Month-Over-Month Progress chart:** custom SVG line chart plotting total debt balance and family score across all captured months, plus a written trend summary (debt trend up/down/flat, score trend up/down/flat, current winning streak — consecutive months with positive family score)
- **Log Spending / Payment form:** logs any entry (groceries, bill payment, debt payment, etc.), auto-triggers a snapshot capture on every submission so the trend chart stays current without manual action
- **Recent Activity feed:** running list of logged entries
- **Manual "Capture This Month" button:** forces a snapshot outside the auto-capture-on-log behavior
- **Debt Free Date + Total Interest readout:** pulled from the payoff calculator

### 3. Add Bill / Add Debt forms
Inline forms for adding new bills/debts after setup is complete (not just during the wizard)

## CORE LOGIC (as-built, not visual — for Design context only)
**Payoff Calculator:** month-by-month simulation. Applies interest, then minimum payments to every debt, then any "extra" cash entirely to the #1 priority debt until paid off, then rolls remaining extra to #2, and so on (user-defined order — not a forced snowball-by-balance or avalanche-by-rate). Outputs projected payoff date per debt, total interest paid, and overall debt-free date.

**Extra Cash Estimate:** monthly income minus monthly bills minus monthly minimum debt payments — can be negative, which the app surfaces honestly rather than hiding.

**Trend Analysis:** compares oldest-to-newest snapshot for direction (debt shrinking/growing, score improving/declining), tracks winning streak, feeds the chart's data series.

## VISUAL THEME (as-built, for Design to reinterpret/elevate)
- Arcade cabinet aesthetic — blinking marquee, CRT-style scanline overlay on the main screen
- Pixel-art buildings for bills/debts, sized/filled to represent amount and payoff progress
- "Wrecker" character (chunky pixel figure, red overalls, brown hat) — slides to a debt building on payment, swings, triggers a brick-fall animation, shrinks the building, floats up a green score popup
- Bills get a gentler "wrecked" diagonal-fill treatment on payment — no combat animation, distinct from debt towers intentionally
- Screen shake on payments ≥$100
- "Racer-guide" character (hooded, cyan goggles) — contextual tips
- "Fixer" character (small, blue, hammer) — toast notifications for new bills
- **IP note:** all character art is original/generic pixel art by design — no copyrighted characters (Wreck-It Ralph aesthetic referenced only in spirit, not licensed assets). This constraint should carry forward into any redesign.

## OPEN QUESTIONS FOR DESIGN / NEXT BUILD PASS
1. Does the redesign keep the arcade/wrecker metaphor, or is this the point where visual direction gets revisited entirely?
2. Any new features planned beyond the existing 6 built steps, or is this purely a visual rewrite of existing functionality?
3. Multi-user note: "me"/"spouse" labels are hardcoded — worth deciding if that stays fixed or becomes editable during any rewrite

## WORKFLOW STATUS
- ✅ App fully built and functional (all 6 original planned steps complete)
- ✅ This brief — reverse-engineered spec for Design handoff
- ⬜ Claude Design — visual direction / mockups
- ⬜ Claude Code — rewrite in stack based on brief + mockups

---

# VISUAL & INTERACTION DESIGN — LOCKED
**Worked through piece-by-piece, same process as Rebound. Everything below supersedes the "as-built" visual notes above where they conflict — these are the decisions for the rewrite.**

## Overall Visual Direction — LOCKED
Keep the full arcade/wrecker metaphor. The wrecker physically fighting and breaking down debt towers stays the emotional centerpiece of the app — not a decorative skin. Redesign's job is elevating execution (pixel art fidelity, animation timing, polish), not replacing the concept.

## Tower Down Sequence (Debt Payoff Moment) — LOCKED
Distinct from regular payment animation — a debt hitting $0 gets its own dedicated sequence:
1. Extended wind-up on the final swing — signals "this one's different" before impact
2. Full base-to-top collapse (not brick-chip) — always triggers screen shake regardless of dollar amount (regular payments keep the existing ≥$100 shake threshold)
3. Dust cloud + settle beat before revealing empty ground
4. "DEBT DEFEATED" arcade-style text slam — distinct from the standard green "−$AMOUNT" popup used on regular payments
5. Wrecker victory flourish (arms up, small hop) before walking off
6. Empty lot stays permanently marked as cleared — dashboard becomes a visible trophy case over time, not just a live status board

## Debt Towers — Visual Behavior — LOCKED
- **Size:** tied to current balance, dynamic in both directions
- **On entry:** construction-style build-up animation (floors stacking from ground up) — reads as a boss spawning in
- **On balance increase:** tower extends upward with a brief "extension" animation, paired with a harsher color flash or small "+" indicator — growth should never visually read the same as progress
- **On payment:** ties into wrecking animation — partial brick-fall + shrink for regular payments, full Tower Down sequence for payoff
- **Cumulative damage layer:** separate from height — cracks, boarded windows, tilted signage — only ever increases regardless of balance fluctuation, so a long-fought debt visibly looks fought-over even after a balance uptick
- **Color:** each debt assigned a color from a fixed palette on creation, purely for at-a-glance identity — no encoded meaning (not tied to interest rate, priority, etc.). Every tower takes visible damage the same way regardless of color.

## Priority-Order Visual Treatment — LOCKED
- Wrecker stationed at the #1 priority tower by default between payments — not neutral/wandering
- Spotlight/target marker (glow, reticle, or "CURRENT TARGET" banner) on the #1 tower
- Reordering priority triggers a wrecker relocation animation — he physically walks to the new #1 tower
- Towers #2 and below stay visually neutral on the skyline — exact order lives in the drag-to-reorder list only; the skyline communicates "who's next," nothing more

## Bill Buildings — LOCKED
Deliberately distinct treatment from debt towers — bills are recurring rituals, not enemies to defeat:
- **Light-up windows metaphor** (not "wrecked" fill) — windows light floor by floor as the bill gets paid, dark = unpaid, lit = paid. One window/floor lights per specific bill paid, not the whole building at once unless the bill is fully settled in one payment.
- **Full reset each cycle** — windows go dark again at the start of each new bill cycle. No cumulative damage layer (unlike debts) — bills aren't shrinking enemies, so there's nothing to "defeat" over time.
- **Paid-on-time streak marker** — small visible badge that grows the longer a bill is never missed, giving bills their own version of the long-term consistency signal debts get from cumulative damage.
- **No wrecker involvement** — bills are never touched by the Wrecker. The contrast between the two building types is intentional and reinforces the "boss fight vs. daily ritual" distinction.

## Character Roster — LOCKED (3 characters, 3 distinct non-overlapping jobs)
- **The Wrecker** (red overalls, brown hat) — destroys debt towers exclusively. Stationed at #1 priority target by default.
- **The Fixer** (blue, hammer) — expanded from toast-notification-only duty to also physically walking to a bill building and flipping a switch to trigger its light-up on payment. Exclusively positive-association — only ever turns things on, never undoes progress. Switch-off at the start of a new bill cycle is silent/ambient/automatic — no character involved, never framed as a loss.
- **The Racer-Guide** (hooded, cyan goggles) — formalized as the meta-layer narrative companion, not tied to any single building. Lives near the Status Report / trend charts. Reacts to family-level trend data (winning streaks, bad months delivered honestly not punishingly, sharp score swings). The only character who "talks" via his existing speech-bubble UI — Wrecker and Fixer stay silent physical performers.

## Family Score Panel — LOCKED
- Large chunky pixel-font digits, count-up animation on load (rolls from 0 to actual value)
- State coloring: green/gold + "WINNING" tag for positive, amber-red (not alarm-red — honest, not shaming) for negative, neutral "BREAK EVEN" framing near zero
- Supporting stats (income, bills, minimums, extra cash, total debt) presented as smaller secondary stat-line readouts beneath the main score — not equal-weight peers
- Winning Streak gets its own visual badge — spark icon at 1–2 months, growing to a fuller flame icon by 5–6+ months
- **Live updates:** score animates/updates in real time on every new log entry submission, not just on dashboard refresh/reload

## Log Spending / Payment Form — LOCKED
- Category-driven live preview — selecting a debt or bill shows a small thumbnail of that specific tower/building before submission
- Dynamic submit button text: "WRECK IT" (debt payment) / "PAY BILL" (bill) / "LOG IT" (general spending) — reflects the in-game action being triggered
- Planned-vs-actual variance flag — non-blocking notice if actual differs significantly from planned (e.g. "Ran $50 over plan"), informational only, ties into future Racer-Guide commentary, never blocks submission or shames the entry
- Post-submit: form stays in view while the triggered animation (wrecker walk-over, fixer light-flip) plays in the same viewport — cause and effect stay visually connected, not happening off-screen while the form just clears
- "Logged by" field is sticky — remembers the last person selected rather than resetting blank each time

## Month-Over-Month Trend Chart — LOCKED
- **Two separate mini-charts**, not one overlaid dual-axis chart — debt balance and family score get their own chart each, avoiding scale-confusion between very different dollar magnitudes
- Chart backgrounds echo the skyline metaphor — faint miniature building silhouettes behind the plotted lines
- Debt chart: Wrecker color palette, rubble/brick icon markers at data points instead of plain dots
- Score chart: green/gold winning-state treatment, matching the Family Score panel
- Winning Streak flame/spark badges appear directly on the score chart's positive-streak months — same visual language as the scoreboard, not a separate system
- Racer-Guide positioned next to the charts as his home base — speech bubble can reference specific chart moments directly
- First-month/empty state gets an intentional Racer-Guide message rather than a blank/broken-looking chart

---

## Setup Wizard — LOCKED
- **Real-time skyline build:** as each bill/debt is added in Stage 2/3, its building appears live on a preview skyline in the background — by "Finish Setup," the user has watched their city take shape rather than seeing it appear all at once
- **No characters during setup:** Wrecker, Fixer, and Racer-Guide all stay dashboard-only — nothing to fight or fix yet, so their appearance would be premature. Setup stays a clean, character-free onboarding flow.
- **Stage order confirmed as-is:** Income → Bills → Debts

## Add Bill / Add Debt (Post-Setup) — LOCKED
- Same real-time build-up animation as setup, triggered live on the dashboard skyline when a new item is added after the wizard is done
- New debt auto-assigned to bottom of priority order (matches existing backend behavior), joins skyline at neutral status — no spotlight until reordered to #1
- New bill starts fully dark (unlit windows), consistent with the light-up-on-payment metaphor
- No Fixer/Wrecker appearance on add — their appearances stay reserved exclusively for payments, keeping those moments meaningful rather than constant
- Brief Racer-Guide acknowledgment is appropriate here (unlike setup) — e.g. "New tower on the skyline" — since this happens mid-dashboard-use, not in the character-free onboarding flow

---

# STILL OPEN
- Final call on whether any *new* features get added beyond the existing 6 built steps, or if this is purely a visual rewrite (original open question, still unresolved — only remaining item)

# UPDATED WORKFLOW STATUS
- ✅ App fully built and functional (all 6 original planned steps complete)
- ✅ Visual & interaction design — FULLY LOCKED, every section worked piece-by-piece (Visual Direction, Tower Down Sequence, Debt Towers, Priority Treatment, Bill Buildings, Character Roster, Family Score, Log Form, Trend Chart, Setup Wizard, Add Bill/Debt forms)
- ✅ Claude Design — ready to begin, full brief complete
- ⬜ Claude Code — rewrite in stack based on brief + mockups
