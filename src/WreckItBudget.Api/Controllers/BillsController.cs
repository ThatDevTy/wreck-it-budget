using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WreckItBudget.Api.Data;
using WreckItBudget.Api.Entities;
using WreckItBudget.Api.Models;

namespace WreckItBudget.Api.Controllers;

/// <summary>
/// WIB-24. Deliberately the same shape as IncomeController - same gate order,
/// same status codes, same helper pattern. Once the first resource is right,
/// the rest should be boring, and boring is the goal: a reader who has seen
/// one of these controllers should be able to predict the others.
///
/// Three things genuinely differ from income:
///   1. No owner. A bill belongs to the household, not a person, so there is
///      no foreign-key existence check.
///   2. Simpler validation. No schedule grid - just a cycle from a fixed list,
///      a positive amount, and a day in range.
///   3. DELETE can be refused. The spending log points at bills, so a bill
///      with history cannot be removed.
/// </summary>
[ApiController]
[Route("api/bills")]
public class BillsController : ControllerBase
{
    private readonly WreckItBudgetContext _db;

    public BillsController(WreckItBudgetContext db)
    {
        _db = db;
    }

    // The allowed pay cycles, matching CK_Bills_PayCycle in the database.
    // Kept as one array so the check and the error message can never drift
    // apart - the message is built from this list rather than typed out again.
    private static readonly string[] ValidCycles = ["monthly", "weekly", "yearly"];

    // ------------------------------------------------------------------
    // GET /api/bills
    // ------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> GetBills()
    {
        var bills = await _db.Bills
            .OrderBy(b => b.Name)
            .Select(b => new BillResponse(
                b.Id, b.Name, b.AmountDue, b.PayCycle, b.DueDay, b.Category))
            .ToListAsync();

        return Ok(bills);
    }

    // ------------------------------------------------------------------
    // GET /api/bills/5
    // ------------------------------------------------------------------
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetBill(int id)
    {
        var bill = await _db.Bills
            .Where(b => b.Id == id)
            .Select(b => new BillResponse(
                b.Id, b.Name, b.AmountDue, b.PayCycle, b.DueDay, b.Category))
            .FirstOrDefaultAsync();

        if (bill is null)
        {
            return NotFound($"No bill with ID {id}.");
        }

        return Ok(bill);
    }

    // ------------------------------------------------------------------
    // POST /api/bills
    // ------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> CreateBill(CreateBillRequest request)
    {
        // Name is UNIQUE in the schema - every bill is a window on the Bill
        // House, and two identically labelled windows are indistinguishable.
        if (await _db.Bills.AnyAsync(b => b.Name == request.Name))
        {
            return Conflict($"A bill named '{request.Name}' already exists.");
        }

        var error = Validate(request.AmountDue, request.PayCycle, request.DueDay);
        if (error is not null)
        {
            return BadRequest(error);
        }

        var bill = new Bill
        {
            Name = request.Name,
            AmountDue = request.AmountDue,
            PayCycle = request.PayCycle,
            DueDay = request.DueDay,
            Category = request.Category,
        };

        _db.Bills.Add(bill);
        await _db.SaveChangesAsync();

        // No re-query here, unlike income. A bill has no related data to
        // resolve, so the entity we just saved already holds everything the
        // response needs - including the Id, which EF read back on insert.
        var created = new BillResponse(
            bill.Id, bill.Name, bill.AmountDue, bill.PayCycle, bill.DueDay, bill.Category);

        return CreatedAtAction(nameof(GetBill), new { id = bill.Id }, created);
    }

    // ------------------------------------------------------------------
    // PUT /api/bills/5
    // ------------------------------------------------------------------
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateBill(int id, UpdateBillRequest request)
    {
        var bill = await _db.Bills.FindAsync(id);

        if (bill is null)
        {
            return NotFound($"No bill with ID {id}.");
        }

        // `b.Id != id` excludes this row, so saving without renaming does not
        // report a conflict against the bill's own name.
        if (await _db.Bills.AnyAsync(b => b.Name == request.Name && b.Id != id))
        {
            return Conflict($"A bill named '{request.Name}' already exists.");
        }

        var error = Validate(request.AmountDue, request.PayCycle, request.DueDay);
        if (error is not null)
        {
            return BadRequest(error);
        }

        bill.Name = request.Name;
        bill.AmountDue = request.AmountDue;
        bill.PayCycle = request.PayCycle;
        bill.DueDay = request.DueDay;
        bill.Category = request.Category;

        await _db.SaveChangesAsync();

        return NoContent();
    }

    // ------------------------------------------------------------------
    // DELETE /api/bills/5
    // ------------------------------------------------------------------
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteBill(int id)
    {
        var bill = await _db.Bills.FindAsync(id);

        if (bill is null)
        {
            return NotFound($"No bill with ID {id}.");
        }

        // This is the real difference from income.
        //
        // SpendingLogEntries has a foreign key pointing at Bills, configured
        // as DeleteBehavior.Restrict. If we tried to delete a bill with
        // history, SQL Server would refuse and EF would throw - and the caller
        // would get an unhelpful 500. So we ask first and answer properly.
        //
        // 409 rather than 400: the request is well formed, it just conflicts
        // with the current state of the data.
        if (await _db.SpendingLogEntries.AnyAsync(s => s.BillId == id))
        {
            return Conflict(
                $"'{bill.Name}' has payments logged against it and cannot be deleted. " +
                "Delete its log entries first, or leave it in place.");
        }

        _db.Bills.Remove(bill);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // ------------------------------------------------------------------
    // Shared validation
    // ------------------------------------------------------------------

    /// <summary>
    /// Returns null when the bill is valid, or a message explaining what is wrong.
    /// Each rule mirrors a CHECK constraint in the database - the constraint is
    /// the net, this is the useful error message.
    /// </summary>
    private static string? Validate(decimal amountDue, string payCycle, byte dueDay)
    {
        // Mirrors CK_Bills_AmountDue. Note > 0, not >= 0: a zero-pound bill is
        // a typo, not a bill. (Debts are the opposite - a zero BALANCE is the
        // goal, so that one is >= 0.)
        if (amountDue <= 0)
        {
            return "Amount due must be greater than 0.";
        }

        // Mirrors CK_Bills_PayCycle.
        if (!ValidCycles.Contains(payCycle))
        {
            return $"Pay cycle must be one of: {string.Join(", ", ValidCycles)}.";
        }

        // Mirrors CK_Bills_DueDay.
        //
        // 1-31 is deliberate, and 29/30/31 are deliberately ALLOWED. A bill
        // genuinely due on the 31st is real. Whether the 31st exists depends
        // on the month, which this row cannot know - so resolving it is
        // application logic at display time, clamping to the last day of the
        // month. See "Rules that live in code" in docs/schema.md.
        if (dueDay is < 1 or > 31)
        {
            return "Due day must be between 1 and 31.";
        }

        return null;
    }
}
