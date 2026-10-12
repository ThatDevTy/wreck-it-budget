using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WreckItBudget.Api.Data;
using WreckItBudget.Api.Entities;
using WreckItBudget.Api.Models;

namespace WreckItBudget.Api.Controllers;

/// <summary>
/// WIB-25. The third CRUD controller, and the one with the most behaviour
/// hiding inside it.
///
/// Income and Bills were mostly "take the fields, check them, save them".
/// Debts has three things the server decides for itself:
///
///   1. ColorIndex  - picked server-side so towers never share a colour.
///   2. Priority    - a new debt joins the bottom of the payoff queue.
///   3. IsCleared   - DERIVED from the balance, never sent by a caller.
///
/// That third one is the pattern worth taking away. A caller could lie about
/// whether a debt is paid off; a caller cannot lie about its balance being
/// zero, because the balance is the thing being checked. Derive state from
/// facts rather than accepting claims about state.
/// </summary>
[ApiController]
[Route("api/debts")]
public class DebtsController : ControllerBase
{
    private readonly WreckItBudgetContext _db;

    public DebtsController(WreckItBudgetContext db)
    {
        _db = db;
    }

    // The tower palette has 25 colours, indexed 0-24 (CK_Debts_ColorIndex).
    private const byte PaletteSize = 25;

    // Priority runs 1-25 (CK_Debts_Priority). Not a real limit anyone should
    // hit - it is a tripwire. Twenty-six debts means something has gone wrong,
    // and finding out from a clear error beats a skyline that will not render.
    private const byte MaxPriority = 25;

    // ------------------------------------------------------------------
    // GET /api/debts
    // ------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> GetDebts()
    {
        // Ordered by Priority, which is the order the skyline draws them in.
        //
        // The null-handling matters: cleared debts have Priority = null, and in
        // SQL Server NULL sorts FIRST by default - so paid-off debts would
        // appear at the front of your skyline, which is backwards. Ordering by
        // "is it null" first pushes them to the end where they belong, as
        // rubble behind the active towers.
        var debts = await _db.Debts
            .OrderBy(d => d.Priority == null)
            .ThenBy(d => d.Priority)
            .ThenBy(d => d.Name)
            .Select(d => new DebtResponse(
                d.Id, d.Name, d.CurrentBalance, d.InterestRate, d.DueDay,
                d.StatementDay, d.MinPayment, d.IsCleared, d.ClearedAt,
                d.Priority, d.ColorIndex))
            .ToListAsync();

        return Ok(debts);
    }

    // ------------------------------------------------------------------
    // GET /api/debts/5
    // ------------------------------------------------------------------
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDebt(int id)
    {
        var debt = await _db.Debts
            .Where(d => d.Id == id)
            .Select(d => new DebtResponse(
                d.Id, d.Name, d.CurrentBalance, d.InterestRate, d.DueDay,
                d.StatementDay, d.MinPayment, d.IsCleared, d.ClearedAt,
                d.Priority, d.ColorIndex))
            .FirstOrDefaultAsync();

        if (debt is null)
        {
            return NotFound($"No debt with ID {id}.");
        }

        return Ok(debt);
    }

    // ------------------------------------------------------------------
    // POST /api/debts
    // ------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> CreateDebt(CreateDebtRequest request)
    {
        if (await _db.Debts.AnyAsync(d => d.Name == request.Name))
        {
            return Conflict($"A debt named '{request.Name}' already exists.");
        }

        var error = Validate(
            request.CurrentBalance, request.InterestRate,
            request.DueDay, request.StatementDay, request.MinPayment);

        if (error is not null)
        {
            return BadRequest(error);
        }

        // --- the server-owned fields ---

        // Priority: the new debt goes to the bottom of the queue. Only ACTIVE
        // debts count, because cleared ones have no position (Priority = null).
        var activeCount = await _db.Debts.CountAsync(d => d.Priority != null);

        if (activeCount >= MaxPriority)
        {
            return Conflict(
                $"You already have {MaxPriority} active debts, which is the limit. " +
                "Clear one before adding another.");
        }

        var priority = (byte)(activeCount + 1);

        // ColorIndex: take the lowest colour nobody is using, so clearing a
        // debt frees its colour for the next one. Falls back to wrapping around
        // if somehow all 25 are taken (it cannot be, given the priority cap,
        // but code that assumes "cannot happen" is how bugs get in).
        var colorIndex = await NextFreeColorIndex();

        var debt = new Debt
        {
            Name = request.Name,
            CurrentBalance = request.CurrentBalance,
            InterestRate = request.InterestRate,
            DueDay = request.DueDay,
            StatementDay = request.StatementDay,
            MinPayment = request.MinPayment,
            Priority = priority,
            ColorIndex = colorIndex,
            // IsCleared defaults to false; ClearedAt stays null.
        };

        _db.Debts.Add(debt);
        await _db.SaveChangesAsync();

        var created = new DebtResponse(
            debt.Id, debt.Name, debt.CurrentBalance, debt.InterestRate, debt.DueDay,
            debt.StatementDay, debt.MinPayment, debt.IsCleared, debt.ClearedAt,
            debt.Priority, debt.ColorIndex);

        return CreatedAtAction(nameof(GetDebt), new { id = debt.Id }, created);
    }

    // ------------------------------------------------------------------
    // PUT /api/debts/5
    // ------------------------------------------------------------------
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateDebt(int id, UpdateDebtRequest request)
    {
        var debt = await _db.Debts.FindAsync(id);

        if (debt is null)
        {
            return NotFound($"No debt with ID {id}.");
        }

        if (await _db.Debts.AnyAsync(d => d.Name == request.Name && d.Id != id))
        {
            return Conflict($"A debt named '{request.Name}' already exists.");
        }

        var error = Validate(
            request.CurrentBalance, request.InterestRate,
            request.DueDay, request.StatementDay, request.MinPayment);

        if (error is not null)
        {
            return BadRequest(error);
        }

        if (request.Priority is not null && (request.Priority < 1 || request.Priority > MaxPriority))
        {
            return BadRequest($"Priority must be between 1 and {MaxPriority}, or null.");
        }

        debt.Name = request.Name;
        debt.CurrentBalance = request.CurrentBalance;
        debt.InterestRate = request.InterestRate;
        debt.DueDay = request.DueDay;
        debt.StatementDay = request.StatementDay;
        debt.MinPayment = request.MinPayment;
        debt.Priority = request.Priority;

        // --- the derived bit ---
        //
        // A balance of exactly zero means the debt is paid off. The caller does
        // not get to assert that; we work it out from the balance, which is the
        // one fact we can trust.
        //
        // Note >= 0 is allowed by the database on purpose. Zero is the GOAL
        // here, not an error - the whole app exists to get balances to zero,
        // and a CHECK of "> 0" would have made that impossible.
        if (debt.CurrentBalance == 0 && !debt.IsCleared)
        {
            debt.IsCleared = true;
            debt.ClearedAt = DateTime.UtcNow;
            debt.Priority = null;   // a cleared debt has no place in the queue
        }

        // Deliberately NOT doing the reverse. If a cleared debt's balance goes
        // back above zero (a new charge on a paid-off card), we do not silently
        // un-clear it here - the design treats the cleared lot as a permanent
        // trophy and the "balance went up" case has its own animation and its
        // own decision to make. Left for WIB-34 rather than guessed at now.

        await _db.SaveChangesAsync();

        return NoContent();
    }

    // ------------------------------------------------------------------
    // DELETE /api/debts/5
    // ------------------------------------------------------------------
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteDebt(int id)
    {
        var debt = await _db.Debts.FindAsync(id);

        if (debt is null)
        {
            return NotFound($"No debt with ID {id}.");
        }

        // Same protection as bills. SpendingLogEntries has a foreign key
        // pointing at Debts set to DeleteBehavior.Restrict, so SQL Server would
        // refuse this and EF would throw - giving the caller a useless 500.
        // Ask first, answer properly.
        //
        // This suits the design rather than fighting it: a paid-off debt is
        // meant to stay on the skyline permanently as a cleared lot. Deleting
        // one is close to always the wrong thing to want.
        if (await _db.SpendingLogEntries.AnyAsync(s => s.DebtId == id))
        {
            return Conflict(
                $"'{debt.Name}' has payments logged against it and cannot be deleted. " +
                "Cleared debts stay on the skyline on purpose.");
        }

        _db.Debts.Remove(debt);
        await _db.SaveChangesAsync();

        // Deleting a debt leaves a gap in the priority sequence (say 1, 3, 4).
        // Harmless for now - the payoff calculator walks them in order rather
        // than by exact number - and WIB-33 rewrites the whole sequence anyway.
        // Noted rather than silently left for someone to wonder about.

        return NoContent();
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Finds the lowest palette index not currently in use, so that clearing or
    /// deleting a debt frees its colour for the next one.
    /// </summary>
    private async Task<byte> NextFreeColorIndex()
    {
        var used = await _db.Debts.Select(d => d.ColorIndex).ToListAsync();

        for (byte c = 0; c < PaletteSize; c++)
        {
            if (!used.Contains(c))
            {
                return c;
            }
        }

        // Every colour taken. Wrap around - duplicates are ugly, not broken,
        // and ColorIndex is not unique in the schema.
        return (byte)(used.Count % PaletteSize);
    }

    /// <summary>
    /// Returns null when the debt is valid, or a message explaining what is wrong.
    /// Each rule mirrors a CHECK constraint, so the database stays the net and
    /// this stays the useful error message.
    /// </summary>
    private static string? Validate(
        decimal currentBalance, decimal interestRate,
        byte dueDay, byte? statementDay, decimal minPayment)
    {
        // >= 0, NOT > 0. This is the single most important boundary in the
        // whole schema: a paid-off debt has a balance of exactly zero, so
        // "> 0" would make it impossible to ever clear one.
        if (currentBalance < 0)
        {
            return "Current balance cannot be negative.";
        }

        // >= 0 as well - 0% promotional APR cards are real.
        if (interestRate < 0)
        {
            return "Interest rate cannot be negative.";
        }

        // And again - deferred student loans genuinely have a $0 minimum.
        if (minPayment < 0)
        {
            return "Minimum payment cannot be negative.";
        }

        // 1-31, with 29/30/31 deliberately ALLOWED. Whether the 31st exists
        // depends on the month, which this row cannot know, so resolving it is
        // application logic at display time. See docs/schema.md,
        // "Rules that live in code".
        if (dueDay is < 1 or > 31)
        {
            return "Due day must be between 1 and 31.";
        }

        // Nullable, so only check it when it was actually sent. `is not null
        // and (...)` reads as "if it has a value, and that value is out of range".
        if (statementDay is not null and (< 1 or > 31))
        {
            return "Statement day must be between 1 and 31, or omitted.";
        }

        return null;
    }
}
