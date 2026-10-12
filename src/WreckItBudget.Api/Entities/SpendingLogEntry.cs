namespace WreckItBudget.Api.Entities;

public class SpendingLogEntry
{
    public int Id { get; set; }

    // 'debt' | 'bill' | 'other'
    public required string Category { get; set; }

    // --- the target: a debt, a bill, or a free-text description ---
    // Two nullable foreign keys rather than one polymorphic TargetId, because
    // a column that sometimes means a debt and sometimes a bill cannot carry
    // a foreign key - and referential integrity is worth more than tidiness.
    public int? DebtId { get; set; }
    public Debt? Debt { get; set; }

    public int? BillId { get; set; }
    public Bill? Bill { get; set; }

    // Required for 'other', forbidden otherwise. Debt and bill entries get
    // their label from the target's name.
    public string? Description { get; set; }

    // 'payment' | 'charge'. Debt entries only - a bill is always a payment.
    public string? Direction { get; set; }

    // D-017: the minimum as it stood the day this payment was made. NOT a copy
    // of Debts.MinPayment, which is only ever today's figure. Without this,
    // raising a minimum silently erases streaks that were genuinely earned.
    public decimal? MinPaymentAtTime { get; set; }

    // You always spent something; you did not necessarily plan it first.
    public decimal? PlannedAmount { get; set; }
    public required decimal ActualAmount { get; set; }

    public string? Notes { get; set; }

    // --- who pressed the button (D-005) ---
    public int LoggedByUserId { get; set; }
    public User? LoggedBy { get; set; }

    // Database has DEFAULT SYSUTCDATETIME().
    public DateTime LoggedAt { get; set; }
}
