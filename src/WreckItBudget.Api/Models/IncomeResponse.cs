namespace WreckItBudget.Api.Models;

/// <summary>
/// What the API sends BACK for an income source.
///
/// This is deliberately not the IncomeSource entity. Three reasons:
///
///   1. The entity has an `Owner` navigation property that is almost always
///      null (we only load it when we ask for it), so it would ship a
///      meaningless "owner": null on every response.
///   2. Callers want the owner's NAME, not their id number. Resolving it here
///      saves the frontend a second request per row.
///   3. If the entity is the API, then renaming a database column breaks every
///      app using the API. This class is a wall between the two: the database
///      can change shape without the public contract changing.
///
/// A `record` is a class whose job is holding data. Writing it like this -
/// a name and a list of properties - generates the whole class for you.
/// </summary>
public record IncomeResponse(
    int Id,
    string Name,

    // Both are here on purpose. OwnerName is what you display;
    // OwnerUserId is what an edit form needs to pre-select the right person.
    int OwnerUserId,
    string OwnerName,

    string PayType,
    decimal AmountPerCheck,

    // Nullable because which of these applies depends on PayType.
    // weekly -> PayWeekday only
    // biweekly -> PayWeekday + FirstPayDate
    // semi-monthly -> PayDay1 + PayDay2
    byte? PayWeekday,
    DateOnly? FirstPayDate,
    byte? PayDay1,
    byte? PayDay2,

    bool IsActive);
