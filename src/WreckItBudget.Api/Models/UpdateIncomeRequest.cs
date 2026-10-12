namespace WreckItBudget.Api.Models;

/// <summary>
/// What a caller may send when EDITING an income source (PUT).
///
/// Almost the same as CreateIncomeRequest, with one real difference:
/// this one has IsActive and the create one does not.
///
/// That difference is the reason they are two classes rather than one.
/// A brand new income source is always active - there is no sensible way to
/// create one that is already switched off - so letting a caller set IsActive
/// on create would be offering a choice that makes no sense. Editing is
/// different: switching an old job off is exactly what you'd want to do.
///
/// Note there is still no Id here. The id comes from the URL
/// (PUT /api/income/12), not the body. If it were in both places they could
/// disagree, and then you have to decide which one wins - a problem worth
/// not having.
/// </summary>
public record UpdateIncomeRequest(
    string Name,
    int OwnerUserId,
    string PayType,
    decimal AmountPerCheck,
    byte? PayWeekday,
    DateOnly? FirstPayDate,
    byte? PayDay1,
    byte? PayDay2,
    bool IsActive);
