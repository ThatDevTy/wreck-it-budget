namespace WreckItBudget.Api.Models;

public record CreateIncomeRequest(
    string Name,
    int OwnerUserId,
    string PayType,
    decimal AmountPerCheck,
    byte? PayWeekday,
    DateOnly? FirstPayDate,
    byte? PayDay1,
    byte? PayDay2);

