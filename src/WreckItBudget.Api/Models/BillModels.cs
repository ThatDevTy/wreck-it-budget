namespace WreckItBudget.Api.Models;

/// <summary>
/// What a caller may send when CREATING a bill.
///
/// Category is nullable because it is optional - useful, but nothing in v1.0
/// breaks without it. Everything else is required.
/// </summary>
public record CreateBillRequest(
    string Name,
    decimal AmountDue,
    string PayCycle,
    byte DueDay,
    string? Category);

/// <summary>
/// What a caller may send when EDITING a bill.
///
/// Identical to the create request today. They are still two types, for the
/// same reason the income ones are: the moment one of them needs a field the
/// other must not have, a shared class forces a bad compromise. Splitting
/// later means touching every caller; splitting now costs five lines.
/// </summary>
public record UpdateBillRequest(
    string Name,
    decimal AmountDue,
    string PayCycle,
    byte DueDay,
    string? Category);

/// <summary>
/// What the API sends back for a bill.
///
/// Flat and boring, which is correct - a bill has no relationships to resolve.
/// Compare IncomeResponse, which has to turn an OwnerUserId into a name.
/// </summary>
public record BillResponse(
    int Id,
    string Name,
    decimal AmountDue,
    string PayCycle,
    byte DueDay,
    string? Category);
