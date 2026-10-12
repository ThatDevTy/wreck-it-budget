using Microsoft.EntityFrameworkCore;
using WreckItBudget.Api.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();

// The connection string lives in user secrets in development, and in
// configuration (an environment variable) once deployed - never in a
// committed file. See WIB-55.
builder.Services.AddDbContext<WreckItBudgetContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();

// Endpoints start at WIB-23 (income), WIB-24 (bills), WIB-25 (debts).

app.Run();
