namespace WreckItBudget.Api.Models;

/// <summary>
/// What a caller may send when CREATING a debt.
///
/// Look at what is NOT here, because that is the interesting part:
///
///   ColorIndex   - the server assigns it. The client has no idea which
///                  palette colours are already taken, so letting it choose
///                  would guarantee duplicates.
///   Priority     - the server assigns it too. A new debt joins at the bottom
///                  of the payoff queue; reordering is a separate action (WIB-33).
///   IsCleared    - a brand new debt is not already paid off. Offering the
///                  choice would be offering nonsense.
///   ClearedAt    - follows from IsCleared, so it is never sent either.
///
/// Every one of those is a field the SERVER owns. Leaving them out of the
/// request is how you stop a caller writing to them.
/// </summary>
public record CreateDebtRequest(
    string Name,
    decimal CurrentBalance,

    // Whole percent: 22.00 means 22% APR, not 0.22. Matching what someone
    // types into a form means no silent conversion between UI and database.
    decimal InterestRate,

    byte DueDay,

    // Nullable - "not recorded yet". Every real debt has a statement date, but
    // Setup should not refuse to save a car loan because the paperwork is not
    // to hand. People faced with a required field they cannot fill will invent
    // a number, and invented data is worse than a blank.
    byte? StatementDay,

    decimal MinPayment);

/// <summary>
/// What a caller may send when EDITING a debt.
///
/// Adds Priority, which create does not have. Editing is where you'd move a
/// debt up or down the queue, so it belongs here and not there.
///
/// Still no IsCleared or ClearedAt. Clearing is not something you type - it is
/// something that HAPPENS when the balance reaches zero. The controller works
/// it out, so the two can never disagree.
/// </summary>
public record UpdateDebtRequest(
    string Name,
    decimal CurrentBalance,
    decimal InterestRate,
    byte DueDay,
    byte? StatementDay,
    decimal MinPayment,
    byte? Priority);

/// <summary>
/// What the API sends back for a debt.
///
/// Everything the Dashboard skyline needs for one tower. Note there is no
/// TowerHeight: height is a function of CurrentBalance, so storing or sending
/// both would mean two copies of one fact that can disagree. The frontend
/// works out the height. "Don't store what you can calculate."
/// </summary>
public record DebtResponse(
    int Id,
    string Name,
    decimal CurrentBalance,
    decimal InterestRate,
    byte DueDay,
    byte? StatementDay,
    decimal MinPayment,
    bool IsCleared,
    DateTime? ClearedAt,
    byte? Priority,

    // Zero-based index into TOWER_PALETTE. The frontend reads it as
    // TOWER_PALETTE[colorIndex] with no arithmetic in between - which is
    // exactly why the range is 0-24 and not 1-25.
    byte ColorIndex);
