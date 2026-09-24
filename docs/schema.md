# Database Schema

The tables behind Wreck-It Budget, and the reasoning behind the parts that aren't obvious.

This file is the source of truth for the schema. It is written by hand first, reviewed, and
only then turned into EF Core entities and a migration — so if this file and the database
ever disagree, this file is what gets fixed.

**Status:** WIB-7 — all six tables agreed on and built, nothing open. Next is the first EF Core migration,
which closes Epic 2.

**Tables:** [`Users`](#users) · [`IncomeSources`](#incomesources) · [`Bills`](#bills) ·
[`Debts`](#debts) · [`SpendingLogEntries`](#spendinglogentries) · [`Snapshots`](#snapshots)

Each table is followed by notes explaining anything a reader couldn't infer from the SQL.
Cross-cutting rules are in [Conventions](#conventions); rules the database deliberately
doesn't enforce are in [Rules that live in code](#rules-that-live-in-code).

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
- **Money columns carry a sign check, and the boundary is deliberate.** `> 0` where zero is
  meaningless — a $0 bill, a $0 paycheck, a $0 log entry are all data-entry slips. `>= 0`
  where **zero is the goal**: a paid-off debt has a balance of exactly 0, a deferred loan can
  have a $0 minimum, and a debt-free household has $0 of total debt in a snapshot. Getting
  this backwards on `Debts.CurrentBalance` would make it impossible to pay a debt off, which
  is the entire point of the app. Read each one as "is zero a legitimate value here?"
- **Timestamps** are `DATETIME2`, not `DATETIME`.
- **Nullable means "not recorded" or "not applicable"** — never "zero" and never "unknown
  because we were lazy." If a column is `NULL`, this file says what that null means.

### Where a rule lives

A rule the database can enforce, it should. A rule it can't see, belongs in code.

The real test is whether the row contains everything needed to judge the rule.
`AmountDue > 0` is decidable from the value itself, so it's a `CHECK`. "Is the 31st a real
day" needs to know *which month*, and that isn't in the row — it isn't anywhere in the
database — so it's application logic. It's about available information, not about whether
something is calculated. See [Rules that live in code](#rules-that-live-in-code) at the
bottom.

---

## `Users`

```sql
CREATE TABLE Users (
    Id           INT          IDENTITY(1,1) PRIMARY KEY,
    Username     VARCHAR(20)  NOT NULL UNIQUE,
    PasswordHash VARCHAR(120) NOT NULL,
    CreatedAt    DATETIME2    NOT NULL DEFAULT SYSUTCDATETIME()
);
```

Exactly two rows, ever. Each has its own password; both see all household data. No roles, no
per-user isolation, and **no `household_id` or grouping table** — see D-003. Referenced by
`IncomeSources.OwnerUserId` (D-006) and by the spending log's "logged by" (D-005).

**`Username` doubles as the display name.** There is no separate `DisplayName` column, so
whatever someone picks to log in with is what the Dashboard shows on their income rows. This
only works because of D-003: two people, same house, who can ask each other. If a third
person could ever join, "who is `xXBudgetLordXx`" becomes a real question — but that can't
happen here by design. Revisit only if D-003 ever changes, which it won't.

**`Username` is `UNIQUE` for a structural reason,** unlike `Bills.Name` and `Debts.Name`
where it was a judgment call. A login identifier that isn't unique can't identify anyone.

**`VARCHAR`, not `NVARCHAR`.** Usernames here are typed at a login box and don't need the
full Unicode range, so the narrower type is honest about the content.

**`PasswordHash` never holds a password.** Passwords are not stored — not encrypted, not
obscured, not at all. What's stored is a one-way hash produced by .NET's
`PasswordHasher<T>`; at login the app hashes what was typed and compares hashes. If this
database is ever stolen, nobody's password goes with it, including from us.

**120, when the current hash is 84.** ASP.NET Core's default hash format base64-encodes to
about 84 characters, and the extra room is deliberate. Hashing algorithms get replaced and
the output format changes with them. A too-narrow column truncates silently, and a truncated
hash matches nothing — so every login fails with nothing in the logs pointing at the cause.
Text columns cost nothing until they're used, so there's no reason to be tight.

**There is no `UNIQUE` on `PasswordHash`,** and there shouldn't be. Modern hashers salt, so
two people choosing the identical password still produce different hashes. The constraint
could never fire, which makes it noise for the next reader — the same smell as writing
`CHECK (x >= 0)` on a `TINYINT`, where the type is unsigned and already guarantees it. (The
sign checks on the money columns are a different matter: `DECIMAL` is signed, so those can
genuinely fail.)

**`CreatedAt` defaults to `SYSUTCDATETIME()`** so the database stamps the row and the app
can't forget to. It's UTC rather than local time because daylight-saving jumps make an hour
either repeat or vanish, and local timestamps stop sorting correctly across one. Store UTC,
convert when displaying.

---

## `IncomeSources`

```sql
CREATE TABLE IncomeSources (
    Id             INT           IDENTITY(1,1) PRIMARY KEY,
    Name           NVARCHAR(50)  NOT NULL UNIQUE,
    OwnerUserId    INT           NOT NULL,
    PayType        NVARCHAR(12)  NOT NULL
                                 CHECK (PayType IN ('weekly', 'biweekly', 'semi-monthly')),
    AmountPerCheck DECIMAL(10,2) NOT NULL CHECK (AmountPerCheck > 0),

    -- weekly and biweekly
    PayWeekday     TINYINT       NULL CHECK (PayWeekday BETWEEN 0 AND 6),
    -- biweekly only
    FirstPayDate   DATE          NULL,
    -- semi-monthly only
    PayDay1        TINYINT       NULL CHECK (PayDay1 BETWEEN 1 AND 31),
    PayDay2        TINYINT       NULL CHECK (PayDay2 BETWEEN 1 AND 31),

    IsActive       BIT           NOT NULL DEFAULT 1,

    CONSTRAINT FK_IncomeSources_Users
        FOREIGN KEY (OwnerUserId) REFERENCES Users(Id),

    CONSTRAINT CK_IncomeSources_PayDayOrder CHECK (PayDay2 > PayDay1),

    CONSTRAINT CK_IncomeSources_Schedule CHECK (
           (PayType = 'weekly'       AND PayWeekday IS NOT NULL AND FirstPayDate IS NULL
                                     AND PayDay1    IS NULL     AND PayDay2      IS NULL)
        OR (PayType = 'biweekly'     AND PayWeekday IS NOT NULL AND FirstPayDate IS NOT NULL
                                     AND PayDay1    IS NULL     AND PayDay2      IS NULL)
        OR (PayType = 'semi-monthly' AND PayWeekday IS NULL     AND FirstPayDate IS NULL
                                     AND PayDay1    IS NOT NULL AND PayDay2      IS NOT NULL)
    )
);
```

**`OwnerUserId` is a foreign key to `Users` (D-006).** Without the constraint it would be an
ordinary integer and nothing would stop a row claiming owner `999`. With it, SQL Server
rejects any insert or update naming a user that doesn't exist — the statement fails outright.
It guards the other direction too: deleting a user who still has income rows fails, because
succeeding would leave those rows pointing at nobody. Stranded rows like that are *orphans*,
and preventing them is the point.

The column holds the user's number rather than their name so the username lives in exactly
one place. Rename a user and every income row follows automatically.

**The three pay types are genuinely different, not three words for the same thing.** Weekly
is 52 checks a year, biweekly 26, semi-monthly 24. Semi-monthly always lands on the same two
dates; biweekly drifts against the calendar, which is why two months a year have three
paychecks. For a budgeting app that difference is money, not trivia.

**`NVARCHAR(12)` fits `'semi-monthly'` exactly, and that's fine here** — unlike
`Users.PasswordHash`, where the slack was deliberate. The difference is who controls the
values: a hash format is set by a library that will change, while these three strings are set
by the `CHECK` six inches away and can't change without editing this table anyway.

**`FirstPayDate` is the biweekly anchor, and it replaced the brief's "start date."** "Every
other Friday" doesn't identify a schedule — every other Friday *starting when?* There are two
possible answers and the weekday alone can't distinguish them, so biweekly needs one known
payday to count from.

A start date can't stand in for it. Start a job Monday 1 September with the first check on
Friday the 12th: counting fortnights from the 1st gives the 1st, 15th and 29th, while the
real paydays are the 12th and 26th. Every date wrong, permanently, with the payoff calculator
built on top. They're different facts, so the column is named for the one that does work —
and since "when the job began" drives nothing in v1.0, there's only one column.

**`PayWeekday` is 0–6 with Sunday = 0 through Saturday = 6.** That matches .NET's `DayOfWeek`
enum, so the stored value casts straight to `(DayOfWeek)PayWeekday` with no translation step
to get wrong. Same reasoning as `Debts.ColorIndex` being zero-based: pick the encoding your
consumer already uses.

**`CK_IncomeSources_Schedule` is where the pay types stop overlapping.** Each branch names
**all four** schedule columns — which must be filled *and* which must be empty. The
exclusions are the load-bearing half. Picture someone setting up a weekly income in the UI
and then switching it to semi-monthly: the weekday from the first attempt is still sitting in
the row, and without `PayWeekday IS NULL` on that branch nothing catches it. Later some code
reads a weekday off a semi-monthly income and does something confident and wrong.

It reads as a grid, and it's worth checking against one before trusting it:

| `PayType` | `PayWeekday` | `FirstPayDate` | `PayDay1` | `PayDay2` |
|---|---|---|---|---|
| `weekly` | required | empty | empty | empty |
| `biweekly` | required | required | empty | empty |
| `semi-monthly` | empty | empty | required | required |

A `CHECK` that's hard to line up against a plain-English table is usually wrong.

Note this constraint also rejects any unrecognised `PayType`, since every branch names one —
so the column-level `CHECK` on `PayType` overlaps with it. Harmless, and clearer kept
separate.

**`Name` is `UNIQUE`, with one known wrinkle.** The goal is to force naming the actual
employer rather than two rows both called "Main Job." The wrinkle is that `UNIQUE` has never
heard of `IsActive`, so a retired income source holds its name forever. If that ever
collides, renaming the dead row ("Acme Corp (2024)") is the fix. The precise tool for
"unique only among live rows" is a *filtered unique index*
(`CREATE UNIQUE INDEX ... WHERE IsActive = 1`); it was skipped deliberately as a second
object to maintain for a collision this household will probably never hit.

**`CK_IncomeSources_PayDayOrder` keeps the two pay days in order** so nobody enters the 15th
and the 1st that way round. It's a standalone constraint rather than part of the schedule
`CHECK`, and it works on weekly and biweekly rows too — where both columns are `NULL` —
because of how SQL treats unknowns. `NULL > NULL` isn't false, it's **`UNKNOWN`**, and a
`CHECK` only rejects a row when its condition is definitely `FALSE`. So the constraint stays
silent on rows where it doesn't apply, without being told to.

That three-valued logic (`TRUE`/`FALSE`/`UNKNOWN`) catches people out in `WHERE` clauses,
where `= NULL` matches nothing at all and you need `IS NULL` instead. Here it works in our
favour.

**`PayDay1` and `PayDay2` have the short-month problem** that `Bills.DueDay` has, and the
same answer — see [Rules that live in code](#rules-that-live-in-code).

---

## `Bills`

```sql
CREATE TABLE Bills (
    Id        INT           IDENTITY(1,1) PRIMARY KEY,
    Name      NVARCHAR(50)  NOT NULL UNIQUE,
    AmountDue DECIMAL(10,2) NOT NULL CHECK (AmountDue > 0),
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

**Bills have no `ColorIndex`, unlike `Debts`.** A debt is a tower on the skyline and needs its
own identity, so it owns a color. A bill is a window in the Bill House, and the only thing a
window says is lit or dark. There is nothing for a per-bill color to distinguish, so the
column would be storage with no reader.

**`DueDay` allows 1–31 and is resolved in code.** See
[Rules that live in code](#rules-that-live-in-code).

---

## `Debts`

```sql
CREATE TABLE Debts (
    Id             INT           IDENTITY(1,1) PRIMARY KEY,
    Name           NVARCHAR(50)  NOT NULL UNIQUE,
    CurrentBalance DECIMAL(10,2) NOT NULL CHECK (CurrentBalance >= 0),
    InterestRate   DECIMAL(5,2)  NOT NULL CHECK (InterestRate   >= 0),
    DueDay         TINYINT       NOT NULL CHECK (DueDay BETWEEN 1 AND 31),
    StatementDay   TINYINT       NULL     CHECK (StatementDay BETWEEN 1 AND 31),
    MinPayment     DECIMAL(10,2) NOT NULL CHECK (MinPayment     >= 0),
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

```sql
CREATE TABLE SpendingLogEntries (
    Id               INT           IDENTITY(1,1) PRIMARY KEY,
    Category         NVARCHAR(5)   NOT NULL CHECK (Category IN ('debt', 'bill', 'other')),

    -- the target: a debt, a bill, or a free-text description
    DebtId           INT           NULL,
    BillId           INT           NULL,
    Description      NVARCHAR(50)  NULL,

    -- debt only
    Direction        NVARCHAR(7)   NULL CHECK (Direction IN ('payment', 'charge')),
    -- debt payments only
    MinPaymentAtTime DECIMAL(10,2) NULL     CHECK (MinPaymentAtTime >= 0),

    PlannedAmount    DECIMAL(10,2) NULL     CHECK (PlannedAmount    >  0),
    ActualAmount     DECIMAL(10,2) NOT NULL CHECK (ActualAmount     >  0),
    Notes            NVARCHAR(500) NULL,

    LoggedByUserId   INT           NOT NULL,
    LoggedAt         DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_SpendingLogEntries_Debts FOREIGN KEY (DebtId)         REFERENCES Debts(Id),
    CONSTRAINT FK_SpendingLogEntries_Bills FOREIGN KEY (BillId)         REFERENCES Bills(Id),
    CONSTRAINT FK_SpendingLogEntries_Users FOREIGN KEY (LoggedByUserId) REFERENCES Users(Id),

    CONSTRAINT CK_SpendingLogEntries_Target CHECK (
           (Category = 'debt'  AND Direction = 'payment'
                AND DebtId IS NOT NULL AND BillId IS NULL
                AND Description IS NULL AND MinPaymentAtTime IS NOT NULL)
        OR (Category = 'debt'  AND Direction = 'charge'
                AND DebtId IS NOT NULL AND BillId IS NULL
                AND Description IS NULL AND MinPaymentAtTime IS NULL)
        OR (Category = 'bill'  AND Direction IS NULL
                AND DebtId IS NULL     AND BillId IS NOT NULL
                AND Description IS NULL AND MinPaymentAtTime IS NULL)
        OR (Category = 'other' AND Direction IS NULL
                AND DebtId IS NULL     AND BillId IS NULL
                AND Description IS NOT NULL AND MinPaymentAtTime IS NULL)
    )
);
```

**This is the table everything else reads from.** The payment streak (D-007), the Recent
Activity feed, and which windows are lit on the Bill House are all *computed from these rows*
rather than stored anywhere. That's the same "don't store what you can calculate" rule as
`Debts.CurrentBalance` versus tower height, applied at the scale of whole features.

**The target is the same shape of problem as `IncomeSources`' pay schedule,** and it gets the
same solution. An entry points at a debt, or a bill, or neither — and each case needs
different columns. Four nullable columns and one `CHECK` whose every branch names all of
them:

| `Category` | `Direction` | `DebtId` | `BillId` | `Description` | `MinPaymentAtTime` |
|---|---|---|---|---|---|
| `debt` | `payment` | required | empty | empty | required |
| `debt` | `charge` | required | empty | empty | empty |
| `bill` | empty | empty | required | empty | empty |
| `other` | empty | empty | empty | required | empty |

The alternative — one `TargetId` column plus a `TargetType` — is a pattern called a
*polymorphic association*, and it's tempting because it's fewer columns. It's avoided here
because a plain integer that sometimes means a debt and sometimes a bill **cannot have a
foreign key**, so the database loses the ability to reject a target that doesn't exist.
Trading two nullable columns for referential integrity is a bad trade.

**`Direction` is debt-only** because a bill is always a payment and "other" is always
spending. Only debts can move in both directions: a payment shrinks the tower, a new charge
grows it. The design brief is explicit that a balance increase must never read as progress,
so this column is what the animation branches on.

**`Description` is required for `'other'` and forbidden otherwise.** For debt and bill
entries the Recent Activity feed gets its label from the target's name, so a second copy here
would be a fact stored twice. An `'other'` entry has no target, so without this it would have
nothing to display.

**`MinPaymentAtTime` freezes the minimum that a payment was judged against (D-017).** It is
not a copy of `Debts.MinPayment` — it's a different fact. `Debts.MinPayment` answers "what is
the minimum?", present tense, always. This answers "what was the minimum *when this payment
was made?*", and once the debt row is edited that is gone forever.

Without it the streak rewrites its own history. Pay $75, $80 and $60 against a $50 minimum
and you've earned three ⚡. Let the balance grow, update the minimum to $100 as you should,
and the Dashboard recomputes all three as below-minimum — three months of real effort deleted
silently by an edit that had nothing to do with them. It inflates the other way too: pay the
card down until the minimum drops to $25, and payments that genuinely missed the old bar
retroactively count.

This is the same principle as `Snapshots`, applied to one column. The test:

> If the source changes, can I still get this value back? **No** → store it. **Yes** → derive
> it.

Balances in March: unrecoverable, so `Snapshots` exists. The Family Score: its three inputs
sit in the same row, so it stays derived. The minimum that applied to a payment:
unrecoverable, so it's stamped here.

It doesn't contradict D-007 either — the streak is still computed from the log, never stored.
This only makes the input it computes from honest.

**Only debt *payments* carry it**, which is why the `CHECK` has four branches rather than
three. A new charge isn't measured against a minimum, and bills and "other" don't have one.

**`PlannedAmount` is nullable, `ActualAmount` is not.** You always spent something; you didn't
necessarily plan it first. The variance notice on the Log Entry screen ("ran over/under plan"
past 15%) only appears when both are present, which falls out of the nullability rather than
needing a flag.

**`LoggedByUserId` is who pressed the button (D-005),** defaulting to the session user with
the Me/Spouse toggle able to override it. It's `NOT NULL` — an entry nobody logged is not a
thing that can happen.

**`LoggedAt` is UTC and database-stamped**, same reasoning as `Users.CreatedAt`.

**The foreign keys make log entries protective.** A debt or bill with history can't be
deleted, because that would orphan its entries. That suits the design: `Debts` already keeps
cleared rows permanently as skyline trophies rather than deleting them.

---

## `Snapshots`

```sql
CREATE TABLE Snapshots (
    Id                     INT           IDENTITY(1,1) PRIMARY KEY,
    CapturedAt             DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),

    TotalDebtBalance       DECIMAL(10,2) NOT NULL CHECK (TotalDebtBalance       >= 0),
    MonthlyIncome          DECIMAL(10,2) NOT NULL CHECK (MonthlyIncome          >= 0),
    MonthlyBills           DECIMAL(10,2) NOT NULL CHECK (MonthlyBills           >= 0),
    MonthlyMinDebtPayments DECIMAL(10,2) NOT NULL CHECK (MonthlyMinDebtPayments >= 0)
);
```

**This table breaks "don't store what you can calculate," on purpose, and it's worth being
precise about why.** Every number here is derivable — from `Debts`, `Bills` and
`IncomeSources` — but only derivable *from those tables as they are right now*. Pay off a
debt and its balance is gone; raise a bill and the old amount is gone. The trend report needs
to know what was true in March, and by June there is nothing left to compute it from.

So the rule is really: don't store what you can *re-*calculate. Derived values that become
unrecoverable are exactly what a snapshot is for.

**The Family Score is deliberately not a column.** It's `MonthlyIncome − MonthlyBills −
MonthlyMinDebtPayments`, and all three are right there in the same row, so it stays
recoverable forever and adding it would be storing the same fact twice. Sharp line worth
holding: the snapshot stores the *inputs*, because those are what vanish. The score is
arithmetic on a row you already have.

**Rows are append-only, and there are many per month.** Every log submission silently writes
one, and the Capture button writes one on demand. The trend report groups them by month and
takes the latest in each. It was called `MonthlySnapshots` until D-018 — "monthly" described
how the trend report *reads* the table rather than what it holds.

**`CapturedAt` is UTC and database-stamped,** same as everywhere else.

---

## Rules that live in code

Things the database deliberately does not enforce, recorded here so they don't get lost or
re-litigated.

### Due days in short months

`DueDay` accepts 1–31 for both bills and debts, as do `IncomeSources.PayDay1` and `PayDay2`.
A `CHECK` can't do better: it sees one row, and `31` is a valid number on its own. Whether
it's a *real* day depends on the month.

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

Computed from `SpendingLogEntries`, never stored (D-007). Each payment is compared against
`SpendingLogEntries.MinPaymentAtTime` — the minimum as it stood that day — not against
`Debts.MinPayment`, which is only ever today's figure (D-017).

### Family score

`monthly income − monthly bills − monthly minimum debt payments`. Calculated, not stored.
Can be negative, and is shown honestly.

---

## Open questions

_None open._
