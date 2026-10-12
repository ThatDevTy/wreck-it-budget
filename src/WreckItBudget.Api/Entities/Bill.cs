namespace WreckItBudget.Api.Entities;
public class Bill
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required decimal AmountDue { get; set; }
    public required string PayCycle { get; set; }
    public required byte DueDay { get; set; } 
    
    public string? Category { get; set; } 
}