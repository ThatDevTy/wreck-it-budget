namespace WreckItBudget.Api.Entities;

public class Snapshot
{
    public int Id { get; set; }

    // Database has DEFAULT SYSUTCDATETIME(), so this does not need `required`.
    public DateTime CapturedAt { get; set; }

    public required decimal TotalDebtBalance { get; set; }
    public required decimal MonthlyIncome { get; set; }
    public required decimal MonthlyBills { get; set; }
    public required decimal MonthlyMinDebtPayments { get; set; }

    // Family Score is deliberately NOT stored - it is
    // MonthlyIncome - MonthlyBills - MonthlyMinDebtPayments,
    // and all three inputs are in this row. See schema.md.
}
