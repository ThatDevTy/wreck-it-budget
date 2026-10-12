using Microsoft.EntityFrameworkCore;
using WreckItBudget.Api.Entities;

namespace WreckItBudget.Api.Data;

/// <summary>
/// The EF Core model. Everything here that is not a DbSet exists because the
/// entity classes cannot express it: column types and precision, UNIQUE indexes,
/// CHECK constraints, defaults, and delete behaviour.
///
/// docs/schema.md is the source of truth. If this file and that file disagree,
/// that file is what gets fixed.
/// </summary>
public class WreckItBudgetContext(DbContextOptions<WreckItBudgetContext> options)
    : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<IncomeSource> IncomeSources => Set<IncomeSource>();
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<Debt> Debts => Set<Debt>();
    public DbSet<SpendingLogEntry> SpendingLogEntries => Set<SpendingLogEntry>();
    public DbSet<Snapshot> Snapshots => Set<Snapshot>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        // ------------------------------------------------------------ Users
        model.Entity<User>(e =>
        {
            e.ToTable("Users");

            // VARCHAR, not NVARCHAR - a login box does not need the full Unicode range.
            e.Property(u => u.Username).IsUnicode(false).HasMaxLength(20).IsRequired();
            e.Property(u => u.PasswordHash).IsUnicode(false).HasMaxLength(120).IsRequired();

            e.Property(u => u.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

            // Structural, not a judgment call: a login identifier that is not
            // unique cannot identify anyone.
            e.HasIndex(u => u.Username).IsUnique();

            // Deliberately NO unique index on PasswordHash - hashes are salted,
            // so it could never fire. See schema.md.
        });

        // ---------------------------------------------------- IncomeSources
        model.Entity<IncomeSource>(e =>
        {
            e.ToTable("IncomeSources", t =>
            {
                t.HasCheckConstraint("CK_IncomeSources_PayType",
                    "[PayType] IN ('weekly', 'biweekly', 'semi-monthly')");
                t.HasCheckConstraint("CK_IncomeSources_AmountPerCheck",
                    "[AmountPerCheck] > 0");
                t.HasCheckConstraint("CK_IncomeSources_PayWeekday",
                    "[PayWeekday] BETWEEN 0 AND 6");
                t.HasCheckConstraint("CK_IncomeSources_PayDay1",
                    "[PayDay1] BETWEEN 1 AND 31");
                t.HasCheckConstraint("CK_IncomeSources_PayDay2",
                    "[PayDay2] BETWEEN 1 AND 31");

                // Works on weekly and biweekly rows too, where both are NULL:
                // NULL > NULL is UNKNOWN, and a CHECK only rejects a definite FALSE.
                t.HasCheckConstraint("CK_IncomeSources_PayDayOrder",
                    "[PayDay2] > [PayDay1]");

                // Each branch names ALL FOUR schedule columns - which must be
                // filled AND which must be empty. The exclusions are the
                // load-bearing half: without them, a stale weekday left over
                // from an edit survives on a semi-monthly row.
                t.HasCheckConstraint("CK_IncomeSources_Schedule", """
                       ([PayType] = 'weekly'       AND [PayWeekday] IS NOT NULL AND [FirstPayDate] IS NULL
                                                   AND [PayDay1]    IS NULL     AND [PayDay2]      IS NULL)
                    OR ([PayType] = 'biweekly'     AND [PayWeekday] IS NOT NULL AND [FirstPayDate] IS NOT NULL
                                                   AND [PayDay1]    IS NULL     AND [PayDay2]      IS NULL)
                    OR ([PayType] = 'semi-monthly' AND [PayWeekday] IS NULL     AND [FirstPayDate] IS NULL
                                                   AND [PayDay1]    IS NOT NULL AND [PayDay2]      IS NOT NULL)
                    """);
            });

            e.Property(i => i.Name).HasMaxLength(50).IsRequired();
            e.Property(i => i.PayType).HasMaxLength(12).IsRequired();
            e.Property(i => i.AmountPerCheck).HasPrecision(10, 2);
            e.Property(i => i.IsActive).HasDefaultValue(true);

            e.HasIndex(i => i.Name).IsUnique();

            // Restrict, not Cascade: deleting a user who still owns income rows
            // must FAIL rather than quietly deleting their income too.
            e.HasOne(i => i.Owner)
             .WithMany(u => u.IncomeSources)
             .HasForeignKey(i => i.OwnerUserId)
             .OnDelete(DeleteBehavior.Restrict)
             .HasConstraintName("FK_IncomeSources_Users");
        });

        // ------------------------------------------------------------ Bills
        model.Entity<Bill>(e =>
        {
            e.ToTable("Bills", t =>
            {
                t.HasCheckConstraint("CK_Bills_AmountDue", "[AmountDue] > 0");
                t.HasCheckConstraint("CK_Bills_PayCycle",
                    "[PayCycle] IN ('monthly', 'weekly', 'yearly')");
                t.HasCheckConstraint("CK_Bills_DueDay", "[DueDay] BETWEEN 1 AND 31");
            });

            e.Property(b => b.Name).HasMaxLength(50).IsRequired();
            e.Property(b => b.PayCycle).HasMaxLength(10).IsRequired();
            e.Property(b => b.Category).HasMaxLength(50);
            e.Property(b => b.AmountDue).HasPrecision(10, 2);

            e.HasIndex(b => b.Name).IsUnique();
        });

        // ------------------------------------------------------------ Debts
        model.Entity<Debt>(e =>
        {
            e.ToTable("Debts", t =>
            {
                // >= 0, not > 0. Zero is the GOAL - a paid-off debt has a balance
                // of exactly 0, and > 0 would make it impossible to clear one.
                t.HasCheckConstraint("CK_Debts_CurrentBalance", "[CurrentBalance] >= 0");
                t.HasCheckConstraint("CK_Debts_InterestRate", "[InterestRate] >= 0");
                t.HasCheckConstraint("CK_Debts_MinPayment", "[MinPayment] >= 0");
                t.HasCheckConstraint("CK_Debts_DueDay", "[DueDay] BETWEEN 1 AND 31");
                t.HasCheckConstraint("CK_Debts_StatementDay", "[StatementDay] BETWEEN 1 AND 31");
                t.HasCheckConstraint("CK_Debts_Priority", "[Priority] BETWEEN 1 AND 25");

                // 0-24, not 1-25: ColorIndex is a true zero-based array index.
                t.HasCheckConstraint("CK_Debts_ColorIndex", "[ColorIndex] BETWEEN 0 AND 24");
            });

            e.Property(d => d.Name).HasMaxLength(50).IsRequired();
            e.Property(d => d.CurrentBalance).HasPrecision(10, 2);
            e.Property(d => d.InterestRate).HasPrecision(5, 2);
            e.Property(d => d.MinPayment).HasPrecision(10, 2);
            e.Property(d => d.IsCleared).HasDefaultValue(false);

            e.HasIndex(d => d.Name).IsUnique();
        });

        // ----------------------------------------------- SpendingLogEntries
        model.Entity<SpendingLogEntry>(e =>
        {
            e.ToTable("SpendingLogEntries", t =>
            {
                t.HasCheckConstraint("CK_SpendingLogEntries_Category",
                    "[Category] IN ('debt', 'bill', 'other')");
                t.HasCheckConstraint("CK_SpendingLogEntries_Direction",
                    "[Direction] IN ('payment', 'charge')");
                t.HasCheckConstraint("CK_SpendingLogEntries_MinPaymentAtTime",
                    "[MinPaymentAtTime] >= 0");
                t.HasCheckConstraint("CK_SpendingLogEntries_PlannedAmount",
                    "[PlannedAmount] > 0");
                t.HasCheckConstraint("CK_SpendingLogEntries_ActualAmount",
                    "[ActualAmount] > 0");

                // Four branches, because MinPaymentAtTime applies to debt
                // PAYMENTS only - a new charge is not measured against a minimum.
                t.HasCheckConstraint("CK_SpendingLogEntries_Target", """
                       ([Category] = 'debt'  AND [Direction] = 'payment'
                            AND [DebtId] IS NOT NULL AND [BillId] IS NULL
                            AND [Description] IS NULL AND [MinPaymentAtTime] IS NOT NULL)
                    OR ([Category] = 'debt'  AND [Direction] = 'charge'
                            AND [DebtId] IS NOT NULL AND [BillId] IS NULL
                            AND [Description] IS NULL AND [MinPaymentAtTime] IS NULL)
                    OR ([Category] = 'bill'  AND [Direction] IS NULL
                            AND [DebtId] IS NULL     AND [BillId] IS NOT NULL
                            AND [Description] IS NULL AND [MinPaymentAtTime] IS NULL)
                    OR ([Category] = 'other' AND [Direction] IS NULL
                            AND [DebtId] IS NULL     AND [BillId] IS NULL
                            AND [Description] IS NOT NULL AND [MinPaymentAtTime] IS NULL)
                    """);
            });

            e.Property(s => s.Category).HasMaxLength(5).IsRequired();
            e.Property(s => s.Direction).HasMaxLength(7);
            e.Property(s => s.Description).HasMaxLength(50);
            e.Property(s => s.Notes).HasMaxLength(500);
            e.Property(s => s.MinPaymentAtTime).HasPrecision(10, 2);
            e.Property(s => s.PlannedAmount).HasPrecision(10, 2);
            e.Property(s => s.ActualAmount).HasPrecision(10, 2);
            e.Property(s => s.LoggedAt).HasDefaultValueSql("SYSUTCDATETIME()");

            // All three Restrict: a debt or bill with history cannot be deleted,
            // which suits the design - cleared debts stay as skyline trophies.
            e.HasOne(s => s.Debt)
             .WithMany(d => d.LogEntries)
             .HasForeignKey(s => s.DebtId)
             .OnDelete(DeleteBehavior.Restrict)
             .HasConstraintName("FK_SpendingLogEntries_Debts");

            e.HasOne(s => s.Bill)
             .WithMany()
             .HasForeignKey(s => s.BillId)
             .OnDelete(DeleteBehavior.Restrict)
             .HasConstraintName("FK_SpendingLogEntries_Bills");

            e.HasOne(s => s.LoggedBy)
             .WithMany(u => u.LoggedEntries)
             .HasForeignKey(s => s.LoggedByUserId)
             .OnDelete(DeleteBehavior.Restrict)
             .HasConstraintName("FK_SpendingLogEntries_Users");
        });

        // -------------------------------------------------------- Snapshots
        model.Entity<Snapshot>(e =>
        {
            e.ToTable("Snapshots", t =>
            {
                // All >= 0: a debt-free household has $0 of total debt, and a
                // household that has entered nothing yet has $0 of everything.
                t.HasCheckConstraint("CK_Snapshots_TotalDebtBalance", "[TotalDebtBalance] >= 0");
                t.HasCheckConstraint("CK_Snapshots_MonthlyIncome", "[MonthlyIncome] >= 0");
                t.HasCheckConstraint("CK_Snapshots_MonthlyBills", "[MonthlyBills] >= 0");
                t.HasCheckConstraint("CK_Snapshots_MonthlyMinDebtPayments",
                    "[MonthlyMinDebtPayments] >= 0");
            });

            e.Property(s => s.CapturedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.Property(s => s.TotalDebtBalance).HasPrecision(10, 2);
            e.Property(s => s.MonthlyIncome).HasPrecision(10, 2);
            e.Property(s => s.MonthlyBills).HasPrecision(10, 2);
            e.Property(s => s.MonthlyMinDebtPayments).HasPrecision(10, 2);
        });
    }
}
