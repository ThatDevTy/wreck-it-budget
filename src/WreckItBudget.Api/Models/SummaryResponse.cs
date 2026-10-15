namespace WreckItBudget.Api.Models;

/// <summary>
/// Everything the Dashboard's top panel needs, in one call.
///
/// Nothing here is read from a stored column. Every figure is calculated from
/// IncomeSources, Bills and Debts each time this endpoint is hit - which is
/// why there is no Summaries table and never will be. If the underlying data
/// changes, the next request reflects it automatically.
///
/// (Snapshots are different, and the difference is worth holding onto. A
/// snapshot deliberately FREEZES these numbers at a point in time, because the
/// inputs will change and the old values become unrecoverable. This endpoint
/// answers "what is true right now"; a snapshot answers "what was true in
/// March". Same numbers, opposite jobs.)
/// </summary>
public record SummaryResponse(

    // income - bills - minimum debt payments.
    // CAN BE NEGATIVE, and when it is, we say so. No clamping, no Math.Abs.
    // A budgeting app that will not tell you that you are underwater is worse
    // than no app at all.
    decimal FamilyScore,

    // The three inputs, returned alongside the result so the Dashboard can
    // show the 2x2 stat grid without asking again - and so a human can check
    // the arithmetic without opening the database.
    decimal MonthlyIncome,
    decimal MonthlyBills,
    decimal MonthlyMinDebtPayments,

    // Not part of the score. It is the fourth stat tile, and the number the
    // whole app is trying to get to zero.
    decimal TotalDebtBalance);
