namespace WreckItBudget.Api.Entities;

public class IncomeSource
{
    public int Id { get; set; }

    public required string Name { get; set; }

    // --- foreign key to Users (D-006) ---
    // Two properties for one relationship:
    //   OwnerUserId is the column that actually exists in the database.
    //   Owner is a "navigation property" - no column of its own. It lets you
    //   write incomeSource.Owner.Username instead of a manual join.
    public int OwnerUserId { get; set; }
    public User? Owner { get; set; }

    // 'weekly' | 'biweekly' | 'semi-monthly'
    public required string PayType { get; set; }

    public required decimal AmountPerCheck { get; set; }

    // --- schedule: which of these apply depends on PayType ---
    // The rule lives in CK_IncomeSources_Schedule, configured on the DbContext.

    // weekly and biweekly. Sunday = 0 .. Saturday = 6, matching System.DayOfWeek.
    public byte? PayWeekday { get; set; }

    // biweekly only. The anchor - "every other Friday" needs one known payday
    // to count from, and a job's start date is not the same fact.
    public DateOnly? FirstPayDate { get; set; }

    // semi-monthly only.
    public byte? PayDay1 { get; set; }
    public byte? PayDay2 { get; set; }

    // Database has DEFAULT 1, but EF sends every property on insert, so the
    // C# initializer is what actually decides this. They must agree.
    public bool IsActive { get; set; } = true;
}
