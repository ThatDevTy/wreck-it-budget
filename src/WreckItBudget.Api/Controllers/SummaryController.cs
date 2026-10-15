using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WreckItBudget.Api.Data;
using WreckItBudget.Api.Models;

namespace WreckItBudget.Api.Controllers;

/// <summary>
/// WIB-26. The first endpoint that computes rather than stores.
///
/// No table behind it, no id in the route, no POST/PUT/DELETE. There is only
/// one summary, so there is only one way to ask for it.
/// </summary>
[ApiController]
[Route("api/summary")]
public class SummaryController : ControllerBase
{
    private readonly WreckItBudgetContext _db;

    public SummaryController(WreckItBudgetContext db)
    {
        _db = db;
    }

    // ------------------------------------------------------------------
    // GET /api/summary
    // ------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> GetSummary()
    {
        // ============================================================
        // INCOME
        // ============================================================
        //
        // Note where .ToListAsync() sits, because it contradicts what the other
        // controllers do and the reason matters.
        //
        // In IncomeController the .Select(...) comes BEFORE .ToListAsync(), so
        // EF translates the whole thing into SQL. That is impossible here: the
        // next line calls MonthlyFromIncome, and EF has no idea how to turn a
        // C# method into SQL.
        //
        //   Reshaping into columns?  let EF do it in SQL  (.Select then .ToList)
        //   Running your own logic?  get the rows first   (.ToList then compute)
        //
        // The .Select still earns its place - it fetches two columns instead of
        // the whole table. After .ToListAsync() we are in plain C# and can do
        // anything.
        var incomes = await _db.IncomeSources
            .Where(i => i.IsActive)              // an ended job is not income
            .Select(i => new { i.PayType, i.AmountPerCheck })
            .ToListAsync();

        var monthlyIncome = incomes.Sum(i => MonthlyFromIncome(i.PayType, i.AmountPerCheck));

        // ============================================================
        // BILLS
        // ============================================================
        //
        // Same shape as income. No IsActive filter - bills do not have one,
        // every bill counts.
        var bills = await _db.Bills
            .Select(b => new { b.PayCycle, b.AmountDue })
            .ToListAsync();

        var monthlyBills = bills.Sum(b => MonthlyFromBill(b.PayCycle, b.AmountDue));

        // ============================================================
        // DEBTS
        // ============================================================
        //
        // Easier, for two reasons. Minimum payments are ALREADY monthly - that
        // is how card statements work - so there is nothing to normalise. And
        // because there is no C# logic involved, these CAN run entirely in SQL:
        // SumAsync sends a SELECT SUM(...) and brings back one number instead
        // of every row.
        //
        // A cleared debt costs nothing and owes nothing, so it is excluded from
        // both figures.
        //
        // SumAsync returns 0 on an empty set rather than throwing, which is
        // what we want - a household with no debts has a total of zero, not an
        // error.
        var monthlyMinDebtPayments = await _db.Debts
            .Where(d => !d.IsCleared)
            .SumAsync(d => d.MinPayment);

        var totalDebtBalance = await _db.Debts
            .Where(d => !d.IsCleared)
            .SumAsync(d => d.CurrentBalance);

        // ============================================================
        // THE SCORE
        // ============================================================
        //
        // Returned exactly as it comes out. If bills and minimum payments
        // exceed income this is negative, and we say so - no clamping, no
        // Math.Abs. A budgeting app that will not tell you that you are
        // underwater is worse than no app.
        var familyScore = monthlyIncome - monthlyBills - monthlyMinDebtPayments;

        // Rounded to pennies on the way out. The arithmetic above runs at full
        // decimal precision; rounding once at the edge avoids compounding
        // rounding errors through three separate sums.
        return Ok(new SummaryResponse(
            decimal.Round(familyScore, 2),
            decimal.Round(monthlyIncome, 2),
            decimal.Round(monthlyBills, 2),
            decimal.Round(monthlyMinDebtPayments, 2),
            decimal.Round(totalDebtBalance, 2)));
    }

    // ------------------------------------------------------------------
    // Normalisation
    // ------------------------------------------------------------------

    /// <summary>
    /// Turns one pay cheque into what it is worth per month.
    ///
    /// This method exists because the obvious answer is wrong. Biweekly does
    /// NOT mean twice a month. It means every two weeks, which is 26 cheques a
    /// year, not 24 - two months a year you get three.
    ///
    ///     on $1,842 biweekly
    ///       wrong:  1842 * 2        = $3,684
    ///       right:  1842 * 26 / 12  = $3,991
    ///
    /// $307 a month missing from a figure whose entire job is telling someone
    /// the truth about their money.
    ///
    /// The rule: annualise first, THEN divide by twelve. Never multiply by a
    /// "roughly how many per month" number.
    /// </summary>
    private static decimal MonthlyFromIncome(string payType, decimal amountPerCheck) => payType switch
    {
        "weekly"       => amountPerCheck * 52 / 12,   // 52 cheques a year
        "biweekly"     => amountPerCheck * 26 / 12,   // 26 - every two weeks
        "semi-monthly" => amountPerCheck * 24 / 12,   // 24 - twice a month exactly

        // The catch-all. Validation in IncomeController should make this
        // unreachable, but "should be" is not "is", and returning 0 beats
        // taking the whole Dashboard down over one bad row.
        _ => 0m,
    };

    /// <summary>
    /// Turns one bill into what it costs per month.
    ///
    /// Note the cycles are NOT the same three as income - bills are monthly,
    /// weekly or yearly (CK_Bills_PayCycle). Easy to assume otherwise, which is
    /// exactly the kind of assumption that produces a plausible wrong number.
    /// </summary>
    private static decimal MonthlyFromBill(string payCycle, decimal amountDue) => payCycle switch
    {
        "monthly" => amountDue,              // already monthly
        "weekly"  => amountDue * 52 / 12,    // 52 a year
        "yearly"  => amountDue / 12,         // one a year, spread across twelve
        _         => 0m,
    };

    // Both of the above are SWITCH EXPRESSIONS - a good fit when every branch
    // produces a VALUE rather than doing something. `_` is the catch-all, the
    // same way `else` is. The `m` on `0m` means decimal, not double.
    //
    // One trap worth naming, because it compiles and runs and is quietly wrong:
    //
    //     amountDue * 52 / 12      correct
    //     amountDue * (52 / 12)    WRONG
    //
    // In the second, 52 and 12 are both whole numbers, so C# does whole-number
    // division first: 52 / 12 = 4, remainder discarded. You would multiply by 4
    // instead of 4.333 and lose about 8% of every weekly bill. Keeping the
    // amount at the front forces the multiplication to happen in decimal.
}
