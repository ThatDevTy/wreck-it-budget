namespace WreckItBudget.Api.Entities;

public class User
{
    public int Id { get; set; }

    public required string Username { get; set; }

    // Never a password. A one-way hash produced by PasswordHasher<T>.
    public required string PasswordHash { get; set; }

    // Database has DEFAULT SYSUTCDATETIME(), so this does not need `required`.
    public DateTime CreatedAt { get; set; }

    public ICollection<IncomeSource> IncomeSources { get; set; } = [];
    public ICollection<SpendingLogEntry> LoggedEntries { get; set; } = [];
}
