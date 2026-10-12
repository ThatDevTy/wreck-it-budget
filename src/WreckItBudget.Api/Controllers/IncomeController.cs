using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WreckItBudget.Api.Data;
using WreckItBudget.Api.Entities;
using WreckItBudget.Api.Models;

namespace WreckItBudget.Api.Controllers;

[ApiController]
[Route("api/income")]
public class IncomeController : ControllerBase
{
    private readonly WreckItBudgetContext _db;

    public IncomeController(WreckItBudgetContext db)
    {
        _db = db;
    }

    // ------------------------------------------------------------------
    // GET /api/income
    // ------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> GetIncomes()
    {
        // .Select() reshapes each database row into an IncomeResponse.
        //
        // The important part: EF reads this BEFORE going to the database.
        // It sees we reached for i.Owner.Username and writes a SQL JOIN to
        // fetch it, and it only selects the columns named here rather than
        // every column in the table. One query, minimum data.
        //
        // This is why there is no .Include(i => i.Owner) - Include loads the
        // whole related row, which we would then throw most of away.
        var sources = await _db.IncomeSources
            .OrderBy(i => i.Name)
            .Select(i => new IncomeResponse(
                i.Id, i.Name, i.OwnerUserId, i.Owner!.Username, i.PayType,
                i.AmountPerCheck, i.PayWeekday, i.FirstPayDate,
                i.PayDay1, i.PayDay2, i.IsActive))
            .ToListAsync();

        return Ok(sources);
    }

    // ------------------------------------------------------------------
    // GET /api/income/5
    // ------------------------------------------------------------------
    // "{id:int}" is a route parameter with a type constraint. The :int part
    // means this only matches when the segment is a whole number, so
    // /api/income/abc never reaches your code - it 404s before that.
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetIncome(int id)
    {
        var source = await _db.IncomeSources
            .Where(i => i.Id == id)
            .Select(i => new IncomeResponse(
                i.Id, i.Name, i.OwnerUserId, i.Owner!.Username, i.PayType,
                i.AmountPerCheck, i.PayWeekday, i.FirstPayDate,
                i.PayDay1, i.PayDay2, i.IsActive))
            .FirstOrDefaultAsync();

        // FirstOrDefaultAsync returns null when nothing matched, rather than
        // throwing. So "not found" is a normal outcome we handle, not a crash.
        if (source is null)
        {
            return NotFound($"No income source with ID {id}.");
        }

        return Ok(source);
    }

    // ------------------------------------------------------------------
    // POST /api/income
    // ------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> CreateIncome(CreateIncomeRequest request)
    {
        // --- gate 1: does the owner exist? ---
        // AnyAsync asks "is there at least one match?" and returns true/false.
        // Cheaper than fetching the row: SQL Server stops looking at the first hit.
        if (!await _db.Users.AnyAsync(u => u.Id == request.OwnerUserId))
        {
            return BadRequest($"No user with ID {request.OwnerUserId}.");
        }

        // --- gate 2: is the name free? ---
        // 409 Conflict, not 400. The name is perfectly valid - it is just taken
        // right now. Delete the other row and this same request would succeed.
        if (await _db.IncomeSources.AnyAsync(i => i.Name == request.Name))
        {
            return Conflict($"An income source named '{request.Name}' already exists.");
        }

        // --- gate 3: does the pay schedule make sense? ---
        var scheduleError = ValidateSchedule(
            request.PayType, request.PayWeekday, request.FirstPayDate,
            request.PayDay1, request.PayDay2);

        if (scheduleError is not null)
        {
            return BadRequest(scheduleError);
        }

        // Every question is answered. Now we can just do the work.
        var source = new IncomeSource
        {
            Name = request.Name,
            OwnerUserId = request.OwnerUserId,
            PayType = request.PayType,
            AmountPerCheck = request.AmountPerCheck,
            PayWeekday = request.PayWeekday,
            FirstPayDate = request.FirstPayDate,
            PayDay1 = request.PayDay1,
            PayDay2 = request.PayDay2,
            // IsActive is left alone - the entity already defaults it to true.
        };

        // Add() only stages the change in memory. Nothing reaches SQL Server
        // until SaveChangesAsync(), which sends everything staged in one
        // transaction. That is what will make multi-step writes safe at WIB-34.
        _db.IncomeSources.Add(source);
        await _db.SaveChangesAsync();

        // source.Id is filled in by now - EF reads back the identity value
        // SQL Server generated during the insert. We re-query so the response
        // carries the owner's name, which the entity we just built doesn't have.
        var created = await _db.IncomeSources
            .Where(i => i.Id == source.Id)
            .Select(i => new IncomeResponse(
                i.Id, i.Name, i.OwnerUserId, i.Owner!.Username, i.PayType,
                i.AmountPerCheck, i.PayWeekday, i.FirstPayDate,
                i.PayDay1, i.PayDay2, i.IsActive))
            .FirstAsync();

        // 201 Created, plus a Location header pointing at the new resource.
        // nameof(GetIncome) lets the compiler check the method name for us -
        // rename GetIncome later and this breaks at build time rather than
        // silently producing a wrong URL.
        return CreatedAtAction(nameof(GetIncome), new { id = source.Id }, created);
    }

    // ------------------------------------------------------------------
    // PUT /api/income/5
    // ------------------------------------------------------------------
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateIncome(int id, UpdateIncomeRequest request)
    {
        // FindAsync looks up by primary key and returns a TRACKED entity -
        // EF remembers it and watches it for changes. That is what makes the
        // plain property assignments further down turn into an UPDATE.
        var source = await _db.IncomeSources.FindAsync(id);

        if (source is null)
        {
            return NotFound($"No income source with ID {id}.");
        }

        if (!await _db.Users.AnyAsync(u => u.Id == request.OwnerUserId))
        {
            return BadRequest($"No user with ID {request.OwnerUserId}.");
        }

        // Same duplicate-name rule as create, with one addition: exclude THIS
        // row. Without `i.Id != id`, saving a record without renaming it would
        // collide with itself and report a conflict against its own name.
        if (await _db.IncomeSources.AnyAsync(i => i.Name == request.Name && i.Id != id))
        {
            return Conflict($"An income source named '{request.Name}' already exists.");
        }

        var scheduleError = ValidateSchedule(
            request.PayType, request.PayWeekday, request.FirstPayDate,
            request.PayDay1, request.PayDay2);

        if (scheduleError is not null)
        {
            return BadRequest(scheduleError);
        }

        // No _db.Update() call needed. Because FindAsync returned a tracked
        // entity, EF compares these values against what it originally loaded
        // and writes an UPDATE containing only the columns that actually changed.
        source.Name = request.Name;
        source.OwnerUserId = request.OwnerUserId;
        source.PayType = request.PayType;
        source.AmountPerCheck = request.AmountPerCheck;
        source.PayWeekday = request.PayWeekday;
        source.FirstPayDate = request.FirstPayDate;
        source.PayDay1 = request.PayDay1;
        source.PayDay2 = request.PayDay2;
        source.IsActive = request.IsActive;

        await _db.SaveChangesAsync();

        // 204 No Content: "it worked, and there is nothing to tell you."
        // The caller already knows what they sent, so echoing it back is noise.
        return NoContent();
    }

    // ------------------------------------------------------------------
    // DELETE /api/income/5
    // ------------------------------------------------------------------
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteIncome(int id)
    {
        var source = await _db.IncomeSources.FindAsync(id);

        if (source is null)
        {
            return NotFound($"No income source with ID {id}.");
        }

        // Deleting income is safe: nothing holds a foreign key pointing at
        // IncomeSources. Bills and Debts are different - the spending log
        // points at those - so those deletes have to be refused if history
        // exists. See BillsController.
        _db.IncomeSources.Remove(source);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // ------------------------------------------------------------------
    // Shared validation
    // ------------------------------------------------------------------

    /// <summary>
    /// Checks the pay-schedule rule from docs/schema.md. Returns null when the
    /// combination is valid, or a message explaining what is wrong.
    ///
    /// It lives in one place because POST and PUT both need it, and a rule
    /// copied into two methods is a rule that eventually gets fixed in only one.
    ///
    /// Every branch checks BOTH halves: what must be present, and what must be
    /// absent. The "must be absent" half is the load-bearing one. Picture
    /// someone setting up a weekly income in the UI, then switching it to
    /// semi-monthly - the weekday from their first attempt is still sitting in
    /// the form data. Without this check it gets saved, and later something
    /// reads a weekday off a semi-monthly income and is confidently wrong.
    /// </summary>
    private static string? ValidateSchedule(
        string payType, byte? payWeekday, DateOnly? firstPayDate,
        byte? payDay1, byte? payDay2)
    {
        if (payType == "weekly")
        {
            if (payWeekday is null)
            {
                return "Weekly income needs a pay weekday.";
            }
            if (firstPayDate is not null || payDay1 is not null || payDay2 is not null)
            {
                return "Weekly income must not have a first pay date or pay days.";
            }
        }
        else if (payType == "biweekly")
        {
            if (payWeekday is null || firstPayDate is null)
            {
                return "Biweekly income needs a pay weekday and first pay date.";
            }
            if (payDay1 is not null || payDay2 is not null)
            {
                return "Biweekly income must not have pay days.";
            }
        }
        else if (payType == "semi-monthly")
        {
            if (payDay1 is null || payDay2 is null)
            {
                return "Semi-monthly income needs two pay days.";
            }
            if (payWeekday is not null || firstPayDate is not null)
            {
                return "Semi-monthly income must not have a pay weekday or first pay date.";
            }
            // Mirrors CK_IncomeSources_PayDayOrder in the database.
            if (payDay2 <= payDay1)
            {
                return "The second pay day must come after the first.";
            }
        }
        else
        {
            return "Pay type must be one of: weekly, biweekly, semi-monthly.";
        }

        return null; // null means "no problem found"
    }
}
