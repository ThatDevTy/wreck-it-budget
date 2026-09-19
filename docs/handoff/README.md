# Handoff: Wreck-It Budget — Visual Redesign

## Overview
Family budgeting app, arcade-cabinet themed. Two people ("me"/"spouse") log income, bills, and debts. Pixel-art buildings represent bills and debts; a "wrecker" character animates debt payments by swinging at and shrinking debt towers. This handoff covers the **visual/interaction redesign** — full functional spec is `design-brief.md` in this folder (reverse-engineered from the as-built app, plus the LOCKED design decisions worked through with Claude Design).

## About the Design Files
`Wreck-It Budget.dc.html` is a **design reference built in HTML** — a working prototype demonstrating exact visual treatment, layout, colors, typography, copy, and interaction/animation behavior. **It is not production code to copy directly.** Your job is to recreate this design in the target stack below, using that stack's own patterns (components, styling system, state management) — not to port the HTML/CSS/JS as-is.

The prototype uses inline styles and a custom templating runtime (`support.js`) specific to the design tool it was built in — ignore that plumbing; treat the rendered visual result (see screenshots) and the markup's structure/values as the spec.

## Fidelity
**High-fidelity.** Colors, typography, spacing, copy, and animation timings below are final — implement pixel-close, not "inspired by."

## Known deviation from `design-brief.md`
The brief's "Setup Wizard" section (LOCKED) specifies a 3-stage wizard (Income → Bills → Debts) with a live-building skyline preview. **That was superseded during design iteration** — the user disliked the staged-wizard flow and the skyline metaphor in setup specifically. The current build (and the spec below) replaces it with a **single flat page** with all three sections visible at once, each independently add/removable, no skyline preview, no stage progression. Everything else in the brief still stands. Build the flat version, not the wizard described in the brief.

## Tech Stack (target)
- **Backend:** C# / ASP.NET Core Web API
- **ORM:** Entity Framework Core
- **Database:** SQL (replaces the as-built single local JSON file — needs a real schema for income sources, bills, debts, spending log entries, and monthly snapshots — see Data Model in `design-brief.md`)
- **Frontend:** Angular / TypeScript
- **Reactive layer:** RxJS
- Personal/family use — no multi-user auth beyond existing "me"/"spouse" owner labels
- LAN reachability (phones on same WiFi) carries forward as a requirement

## Global Visual Language
- **Background:** `#0a0710` (app shell) / `#100a16` (screen background)
- **Card/panel surface:** `#150c1c` or `#160f1e`, border `1-2px solid #2a1c36`, border-radius `9-10px`
- **CRT scanline overlay:** repeating linear gradient, 1px lines every 4px, `rgba(255,255,255,.05)`, `mix-blend-mode:overlay`, animated scroll (0.6s linear loop) — applied over the entire screen content, z-index above content
- **Header bar:** gradient `#1c1024` → `#100a16`, `2px solid #2f1f3a` bottom border, title in Press Start 2P `12px`, color `#ffd36e`, `text-shadow:0 0 8px rgba(255,211,110,.6)`, plus 3 small blinking marquee dots (red `#ff5a4d`, gold `#ffd36e`, green `#6ee0a8`, each 6px circle, staggered 1.1s opacity blink 0.2s apart)
- **Fonts:** "Press Start 2P" (Google Fonts) for headlines/scores/CTAs/retro labels; system-ui for body copy, inputs, secondary text
- **Body text color:** `#e8ddf0` primary, `rgba(255,255,255,.4-.6)` secondary/muted
- **Primary color palette by role:**
  - Gold/amber `#ffd36e` — brand/header accent, winning streak
  - Green `#6ee0a8` / `#8ef0c0` — positive/winning states, income
  - Red `#e2543a` / `#ff9a7a` / `#c0392b` — debt, danger, primary destructive-feeling CTA ("WRECK IT", "LOG SPENDING")
  - Blue `#3a8fe2` / `#8ec0f0` — bills
  - Purple `#c9b8ff` / `#3a2a54` — nav/secondary UI accents
- **Debt tower color palette** (assigned round-robin per debt, purely for identity, no encoded meaning): `#e2543a, #3a8fe2, #e2b23a, #8f5ae2, #3ae2b2, #e23a8f`
- **Bill accent palette:** `#ffd36e, #8fe2c7, #e28f8f, #8fb2e2, #c9a8f0`
- **Buttons:** primary CTA = Press Start 2P, `11-12px`, white text on `#c0392b`, often with a hard drop-shadow (`0 3-4px 0 #7a2419`) for a chunky arcade-button feel; secondary = system-ui `10-11px` on dark panel with 1-2px border
- **Screen shake:** on payments ≥$100, and always on debt payoff — `shakeScreen` keyframe, translate ±4px, 0.4s ease

## Screens

### 1. Dashboard (home)
- Header bar (see Global) with "WRECK-IT BUDGET" title
- **Family Score panel:** rounded card, border/bg color state-driven (green tones when winning, amber-red when losing, neutral when break-even) with a "WINNING"/"LOSING"/"BREAK EVEN" tag top-right. Score digits: Press Start 2P `34px`, glowing text-shadow matching state color, **count-up animation from 0 to actual value on load**. Formula caption directly under the digits: "Income − Bills − Min. Debt Payments" (`8.5px` system-ui, muted). Below that: streak icon (pulsing, `flamePulse` 1.4s) + streak label. Below a divider: 2x2 grid of secondary stats (Income, Bills, Min. Debt, Total Debt) each as a small label + `13px` white value. Footer line: "Debt-free by **{date}** · ~{amount} interest"
  - **Family Score formula:** `monthly income − monthly bills − monthly minimum debt payments`. Can be negative — shown honestly, not hidden.
  - **Streak definition:** consecutive debt payments logged **above the minimum due** (not calendar months of positive score). Increments by 1 each qualifying payment, resets to 0 on any payment at/below minimum. Icon: `·` at 0, `⚡` at 1-5, `👑` at 6+.
- **Racer-Guide tip strip:** small avatar (34x34, rounded, pixelated) + speech-bubble-style panel (`#1b2530` bg) with contextual message reacting to trend data (streaks, rough months, score swings) — never punishing tone on bad months
- **Debt Towers section:** horizontally scrollable skyline. Wrecker character sprite (48x48, pixelated) sticky-positioned at the left, standing at the **#1 priority tower** by default. Each tower: width 72px, height mapped from current balance (dynamic both directions — grows on new charges, shrinks on payment), colored per its assigned palette color, dark cap strip at top, 2x5 grid of "window" cells inside (lit/dark per an internal damage/paid pattern), name + balance below. The current #1 priority tower gets a "TARGET" pixel-badge above it and a pulsing radial glow behind it. A cleared (fully paid) debt renders as a small rubble/tile stack instead of a building — **permanent visual trophy**, not removed from the layout.
  - Entry: construction-style build-up (floors stacking bottom-up), reads as "boss spawning in"
  - Balance increase: brief upward "extension" animation + harsher color flash/"+" indicator — must never look like progress
  - Regular payment: partial brick-fall + shrink, ties to the wrecker's swing animation, screen shake if ≥$100
  - Full payoff: separate "Tower Down" sequence (see below) — always shakes regardless of amount
  - Cumulative damage layer (cracks/boarded windows/tilted sign) only ever increases, independent of balance fluctuation
- **Priority Order list:** draggable rows, each with color dot, priority number (Press Start 2P `8px`), name, and up/down arrow buttons as a non-drag-and-drop fallback. Reordering triggers the wrecker relocating to the new #1 tower on the skyline.
- **Bill House:** a single house illustration (roof + wall panel) with a 5-column grid of small square "windows," one per bill, lighting up (glow + color shift, `lightUp` keyframe) as that specific bill is paid; unpaid = dark. Fixer character sprite walks in from the side and "flips a switch" on payment (never removes/undoes anything). Windows reset to dark at the start of each new bill cycle — silent/automatic, no character, never framed as loss. Below the house: a wrapped row of bill chips (color dot + name + amount + streak badge if any).
- **Log Spending** button (primary red CTA) + **Capture** button (manual snapshot, secondary) side by side
- **View Month-Over-Month Trend →** link-style row button
- **Recent Activity feed:** reverse-chron list of logged entries (icon, label, who + relative time, red "-$amount")
- Bottom tab bar (Dashboard / Log / Trend / Add), 4 equal-width icon+label buttons, active tab tinted

### 2. Setup (flat page, no wizard stages)
Single scrolling page — build this, **not** the staged wizard described in `design-brief.md` (see deviation note above).
- Header: same header-bar pattern as dashboard. Title "PLAYER SETUP" in Press Start 2P with a blinking pixel-cursor block (`8x12px` `#ffd36e` rectangle, steps(2) 1s blink) immediately after the text. Subtitle: "Add your income, bills, and debts. Edit anytime later." (`10px` system-ui, muted)
- Three stacked cards, `#150c1c` bg, `2px solid #2a1c36` border, `10px` radius, `12px` padding, in this order: **Income → Bills → Debts**
- Each card header: small glowing pixel square (9x9, color-coded — green `#8ef0c0` income, blue `#8ec0f0` bills, red/orange `#f0a08e` debts, `box-shadow:0 0 6px` matching color) + Press Start 2P `9px` label, same color, matching text-shadow glow
- Each card body: list of already-added items as rows (`#160f1e` bg, `6px` radius, `8x10px` padding) — small color-dot (8x8, rounded 2px; debts use their real assigned tower color, income/bills use the section's accent) + name + secondary details (owner/pay type for income, amount/cycle for bills, balance/min for debts) in muted text + a `✕` remove button (transparent, muted, `13px`)
- Below the list: the add-item form (inputs matching global input style: `#160f1e` bg, `1px solid #2a1c36`, `7px` radius, `10px` padding, `12px` system-ui, full-width, `8px` gap between stacked/paired fields) — Income needs name, owner (Me/Spouse select), pay type (Twice monthly/Weekly select), amount per check; Bills needs name, amount due, due day, cycle (Monthly/Weekly/Yearly select); Debts needs name, balance, min. payment, APR%
- Add button per card: full-width, Press Start 2P `10px`, letter-spacing `.5px`, section-colored text on a darker tinted bg with matching border (e.g. income: `#8ef0c0` on `#2a4a3a`/`#3a6a52` border) — copy: "ADD INCOME" / "ADD BILL" / "ADD DEBT" (no "+", no emoji)
- Bottom: full-width "FINISH SETUP" button — Press Start 2P `12px`, white on `#c0392b`, `8px` radius, glow shadow `0 0 14px rgba(192,57,43,.55)` — routes to Dashboard
- **No characters appear on this screen** (Wrecker/Fixer/Racer-Guide are dashboard-only — nothing to fight/fix yet)
- Items added here write directly into the same income/bills/debts lists the Dashboard reads — this is not a separate draft state

### 3. Log Entry
- Back arrow + "LOG ENTRY" header (no marquee dots on sub-screens, just back + title)
- Live preview stage (dynamic height) showing a small thumbnail of the selected debt/bill building, animating in place when the entry is submitted (wrecker swings in for debt payments, fixer walks in and flips the switch for bills, a stamp+speech-bubble for "other") — **animation plays in the same viewport as the form**, form stays visible, nothing happens off-screen
- Category selector: 3 equal buttons (DEBT / BILL / OTHER), each with a small pixel glyph icon (building silhouette / house-with-flag / notepad), active state tinted to that category's color, inactive dark
- Debt-only: Payment(−) / New Charge(+) mode toggle
- Target select dropdown (debt/bill being logged against)
- Planned $ / Actual $ paired number inputs, labeled in Press Start 2P `8px` muted labels
- Quick-fill chips: "Min payment · {amt}" and "Last paid · {amt}" (pill buttons, `#221530` bg, `20px` radius) when applicable
- Variance notice (non-blocking, informational): "ℹ Ran {amount} over/under plan" when actual differs >15% from planned — amber `#e8b86e` on `#2a2312`
- Notes input (optional)
- "Logged by: Me" / "Logged by: Spouse" toggle pair — **sticky, remembers last selection**
- Submit button: dynamic label — "WRECK IT" (debt payment) / "PAY BILL" (bill) / "LOG IT" (other) — Press Start 2P `12px`, same chunky red CTA + drop-shadow style as dashboard's primary button
- Post-submit: inline confirmation "✓ Logged! Log another · Dashboard" links, plus every submission silently triggers a snapshot capture

### 4. Trend Report
- Back arrow + "TREND REPORT" header
- Racer-Guide message strip (same pattern as dashboard)
- 2-column stat row: "DEBT-FREE BY" / "ON-TIME BILLS"
- **Two separate mini line charts** (not combined) — each 320x128 SVG:
  - Debt Balance chart: reddish theme (`#241318`→`#150c1c` bg gradient, `#6a3226` border), header "🏦 DEBT BALANCE" + big digit readout, faint building-silhouette rects behind the line (opacity .22, built from the user's **actual current debts+bills, colored to match, height ~ log-scaled balance/amount** — not decorative/random), line stroke `#e2543a` 3px, rubble/brick-square markers (7x7, rotated 45°) at data points, month labels along the bottom, trend summary sentence below
  - Family Score chart: green theme (`#15281f`→`#150c1c` bg, `#2e6a4a` border), header "🏆 FAMILY SCORE" + digit (color flips red/green by sign), formula caption under the header, same silhouette background, a dashed zero-line, circular markers colored by sign, streak badges (⚡ icons) inline, trend summary sentence
- Bill Payment Streaks list: per-bill row with streak badge if active
- Empty-state toggle + Racer-Guide first-month message

## Data Model
See `design-brief.md` → **DATA MODEL (as-built)** for full field-level spec: Income Source, Bill, Debt, Spending Log Entry, Monthly Snapshot. Needs a real SQL schema in the rewrite (was a flat JSON file).

## Core Logic
See `design-brief.md` → **CORE LOGIC**: Payoff Calculator (month-by-month simulation, user-ordered priority waterfall — not forced snowball/avalanche), Extra Cash / Family Score formula, Trend Analysis (oldest-to-newest snapshot comparison).

## Interactions & Behavior Summary
- Debt payoff triggers a dedicated full-screen "Tower Down" sequence: extended wind-up swing → full collapse (not brick-chip) → always screen-shake → dust cloud + settle → "DEBT DEFEATED" Press Start 2P text slam (`22px`, red, glowing) → wrecker victory hop → walks off → empty lot stays permanently as a cleared marker
- Reordering debt priority animates the Wrecker walking to the new #1 tower
- New bill/debt added post-setup gets the same live build-up animation on the dashboard skyline, joins at neutral status (bottom of priority for debts, dark windows for bills), brief Racer-Guide toast acknowledgment (e.g. "New tower on the skyline")
- Toast notifications: bottom-anchored, auto-dismiss ~3s

## Characters (3, non-overlapping jobs)
- **Wrecker** (red overalls, brown hat) — debt towers exclusively, stationed at #1 priority
- **Fixer** (blue, hammer) — bill windows exclusively, only ever positive (turns on, never off)
- **Racer-Guide** (hooded, cyan goggles) — meta-narrative only, the only character with a speech bubble, lives near Family Score / trend charts
- All character art is original pixel art — no licensed/copyrighted characters. Sprite sheets included in `assets/` (idle, walk, swing frames for Wrecker/Fixer; single sprite for Racer-Guide).

## Assets
- `assets/char-wrecker*.png`, `assets/char-fixer*.png`, `assets/char-racer.png` — pixel-art character sprites, various animation frames
- `assets/tile_004*.png` — rubble/cleared-lot tile graphics used in the payoff sequence and permanent cleared-debt markers
- Google Font: Press Start 2P (`https://fonts.googleapis.com/css2?family=Press+Start+2P&display=swap`)

## Files in this bundle
- `Wreck-It Budget.dc.html` — full interactive design reference (open in a browser)
- `design-brief.md` — original reverse-engineered functional spec + LOCKED design decisions (see deviation note above re: Setup)
- `assets/` — character sprites + tile graphics referenced by the prototype
