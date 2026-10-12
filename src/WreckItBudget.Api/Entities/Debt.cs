namespace WreckItBudget.Api.Entities;

public class Debt
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public required decimal CurrentBalance { get; set; }

    // Whole percent: 22.00 means 22% APR, not 0.22.
    // DECIMAL(5,2) in the schema - precision is configured on the DbContext.
    public required decimal InterestRate { get; set; }

    public required byte DueDay { get; set; }

    // null = not recorded yet. Every real debt has a statement date;
    // Setup should not refuse to save because the paperwork is not to hand.
    public byte? StatementDay { get; set; }

    public required decimal MinPayment { get; set; }

    // Database has DEFAULT 0, and C# bool defaults to false. They agree.
    public bool IsCleared { get; set; }

    // null = not cleared.
    public DateTime? ClearedAt { get; set; }

    // null = not in the payoff waterfall. A cleared debt has no position in the queue.
    public byte? Priority { get; set; }

    // Zero-based index into TOWER_PALETTE. The frontend reads it as
    // TOWER_PALETTE[ColorIndex] with no arithmetic in between.
    public required byte ColorIndex { get; set; }

    public ICollection<SpendingLogEntry> LogEntries { get; set; } = [];
}
