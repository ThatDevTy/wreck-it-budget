# Database Schema

The tables behind Wreck-It Budget, and the reasoning behind the parts that aren't obvious.

This file is the source of truth for the schema. It is written by hand first, reviewed, and
only then turned into EF Core entities and a migration — so if this file and the database
ever disagree, this file is what gets fixed.

**Status:** WIB-7 in progress. `Bills` and `Debts` are agreed. The rest are placeholders.

---

## Conventions

These hold for every table unless a note says otherwise.

- **Names** are `PascalCase`, tables plural (`Bills`), columns singular (`AmountDue`).
- **Primary keys** are `Id INT IDENTITY(1,1)`.
- **Money** is `DECIMAL(10,2)` — never `FLOAT`. Floating point can't represent `0.10`
  exactly, so sums drift by fractions of a cent and a balance that should hit zero doesn't.
  `DECIMAL` stores the digits you meant. `(10,2)` is ten digits total with two after the
  point, so it tops out just under $100,000,000.
- **Percentages** are stored as **whole percent**: `22.00` means 22% APR, not 0.22. That's
  what someone types into the form, so nothing is silently converted between the UI and the
  database. The division happens once, in the payoff calculator, as `apr / 100 / 12`.
- **Timestamps** are `DATETIME2`, not `DATETIME`.
- **Nullable means "not recorded" or "not applicable"** — never "zero" and never "unknown
  because we were lazy." If a column is `NULL`, this file says what that null means.

### Where a rule lives

A rule the database can enforce, it should. A rule it can't see, belongs in code.

The test is whether the rule is true looking at **one row on its own**. `AmountDue` can never
be negative no matter what else is in the database — that's a `CHECK`. "Is the 31st a real
day" depends on which month you're asking about, and the row has no idea — that's
application logic. See [Rules that live in code](#rules-that-live-in-code) at the bottom.

---

## `Users`

_Not drafted yet._ Exactly two rows, ever. Each has its own password; both see all household
data. No roles, no per-user isolation, and **no `household_id` or grouping table** — see
D-003. Referenced by `IncomeSources.OwnerUserId` (D-006) and by the spending log's
"logged by" (D-005).

---

## `IncomeSources`

_Not drafted yet. This is the next table._ From the brief: name, owner, pay type (twice
monthly or weekly), amount per check, pay dates or pay weekday, optional start date, active
flag. The owner is a foreign key to `Users` (D-006), which is the new idea in this table.

---

## `Bills`

```sql
CREATE TABLE Bills (
    Id        INT           IDENTITY(1,1) PRIMARY KEY,
    Name      NVARCHAR(50)  NOT NULL UNIQUE,
    AmountDue DECIMAL(10,2) NOT NULL,
    PayCycle  NVARCHAR(10)  NOT NULL CHECK (PayCycle IN ('monthly', 'weekly', 'yearly')),
    DueDay    TINYINT       NOT NULL CHECK (DueDay BETWEEN 1 AND 31),
    Category  NVARCHAR(50)  NULL
);
```

**`Name` is `UNIQUE`,** matching `Debts`. The trade-off is real: it blocks a household that
genuinely has two bills they'd both call "Insurance." It's worth it because every bill is a
window on the Bill House and every debt is a tower on the skyline, and two identically
labelled ones are indistinguishable there. The constraint forces slightly better names —
"Car Insurance" and "Renters Insurance" — which is what you'd want anyway. With exactly two
users who can talk to each other, renaming is a conversation, not an obstacle.

**`PayCycle` is a `CHECK` list rather than a lookup table.** Three values that only change if
the app itself changes. A `BillCycles` table would be a join for no benefit.

**`Category` is nullable** because it's useful but not required — nothing in v1.0 breaks if
it's empty.

**`DueDay` allows 1–31 and is resolved in code.** See
[Rules that live in code](#rules-that-live-in-code).

---

## `Debts`

```sql
CREATE TABLE Debts (
    Id             INT           IDENTITY(1,1) PRIMARY KEY,
    Name           NVARCHAR(50)  NOT NULL UNIQUE,
    CurrentBalance DECIMAL(10,2) NOT NULL,
    InterestRate   DECIMAL(5,2)  NOT NULL,
    DueDay         TINYINT       NOT NULL CHECK (DueDay BETWEEN 1 AND 31),
    StatementDay   TINYINT       NULL     CHECK (StatementDay BETWEEN 1 AND 31),
    MinPayment     DECIMAL(10,2) NOT NULL,
    IsCleared      BIT           NOT NULL DEFAULT 0,
    ClearedAt      DATETIME2     NULL,
    Priority       TINYINT       NULL     CHECK (Priority BETWEEN 1 AND 25),
    ColorIndex     TINYINT       NOT NULL CHECK (ColorIndex BETWEEN 0 AND 24)
);
```

**`InterestRate` is whole percent** — `22.00` is 22% APR. See Conventions.

**`StatementDay` is nullable, meaning "not recorded yet."** Every real debt has a statement
date, but Setup shouldn't refuse to save a car loan because the paperwork isn't in front of
you — people faced with a required field they can't fill will type a plausible number, and
invented data is worse than a blank. It also drives no behavior in v1.0; it's reference
information, not a calculator input.

**`Priority` is nullable and capped at 25.** Null means the debt isn't in the priority
waterfall — a cleared debt has no position in the queue. The 25 is a generous ceiling: not a
real limit anyone should hit, but a tripwire. Twenty-six debts means something has gone
wrong, and it's better to find out from a constraint than from a skyline that won't render.

**`ColorIndex` is a true zero-based array index**, which is why the range is `0–24` and not
`1–25`. The frontend reads it as `TOWER_PALETTE[colorIndex]` with no arithmetic in between.
An off-by-one here would be invisible: every debt would just get the wrong color, and nobody
would ever get the first one.

**`IsCleared` + `ClearedAt` are kept, not derived.** A cleared debt stays in the table
permanently — the cleared lot is a trophy on the skyline (D-009 territory), not a deleted
row.

**`CurrentBalance` is stored; tower height is not.** Height is a function of the balance, so
storing both means two copies of the same fact that can disagree. Don't store what you can
calculate.

---

## `SpendingLogEntries`

_Not drafted yet._ Every logged payment or charge. Drives the payment streak (D-007), the
Recent Activity feed, and the bill-paid state of the Bill House windows. Records who logged
it (D-005). Debt entries carry a payment/charge direction.

---

## `MonthlySnapshots`

_Not drafted yet._ Point-in-time captures of totals, written automatically on every log
submission and manually via the Capture button. The trend report compares
oldest-to-newest across these.

---

## Rules that live in code

Things the database deliberately does not enforce, recorded here so they don't get lost or
re-litigated.

### Due days in short months

`DueDay` accepts 1–31 for both bills and debts. A `CHECK` can't do better: it sees one row,
and `31` is a valid number on its own. Whether it's a *real* day depends on the month.

So the database stores what the user meant — "the 31st" — and the app resolves it to a real
date by **clamping to the last day of that month**. A bill due on the 31st falls on Feb 28,
Apr 30, and Jul 31. This matches what actual billers do, and it means the stored value never
has to be edited when the calendar changes.

```csharp
// example, not the answer
var day  = Math.Min(bill.DueDay, DateTime.DaysInMonth(year, month));
var date = new DateTime(year, month, day);
```

The UI should also **flag it at entry** rather than silently accepting it: when someone picks
29, 30, or 31, show a note that this bill will land on the last day in shorter months. The
clamp is the behavior; the note is so it isn't a surprise.

### Payment streaks

Computed from `SpendingLogEntries`, never stored (D-007).

### Family score

`monthly income − monthly bills − monthly minimum debt payments`. Calculated, not stored.
Can be negative, and is shown honestly.

---

## Open questions

- **Should money columns get `CHECK (x >= 0)`?** `AmountDue`, `MinPayment` and
  `CurrentBalance` can't meaningfully be negative, and that rule is true of a row on its own,
  so the database could enforce it. Not added yet.
- **Do bills need a `ColorIndex` too?** The design has a five-color bill accent palette, but
  the Bill House windows may not need a persisted color per bill.
