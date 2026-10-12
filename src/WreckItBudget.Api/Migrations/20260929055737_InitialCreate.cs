using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WreckItBudget.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Bills",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AmountDue = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    PayCycle = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    DueDay = table.Column<byte>(type: "tinyint", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bills", x => x.Id);
                    table.CheckConstraint("CK_Bills_AmountDue", "[AmountDue] > 0");
                    table.CheckConstraint("CK_Bills_DueDay", "[DueDay] BETWEEN 1 AND 31");
                    table.CheckConstraint("CK_Bills_PayCycle", "[PayCycle] IN ('monthly', 'weekly', 'yearly')");
                });

            migrationBuilder.CreateTable(
                name: "Debts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CurrentBalance = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    InterestRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    DueDay = table.Column<byte>(type: "tinyint", nullable: false),
                    StatementDay = table.Column<byte>(type: "tinyint", nullable: true),
                    MinPayment = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    IsCleared = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ClearedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Priority = table.Column<byte>(type: "tinyint", nullable: true),
                    ColorIndex = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Debts", x => x.Id);
                    table.CheckConstraint("CK_Debts_ColorIndex", "[ColorIndex] BETWEEN 0 AND 24");
                    table.CheckConstraint("CK_Debts_CurrentBalance", "[CurrentBalance] >= 0");
                    table.CheckConstraint("CK_Debts_DueDay", "[DueDay] BETWEEN 1 AND 31");
                    table.CheckConstraint("CK_Debts_InterestRate", "[InterestRate] >= 0");
                    table.CheckConstraint("CK_Debts_MinPayment", "[MinPayment] >= 0");
                    table.CheckConstraint("CK_Debts_Priority", "[Priority] BETWEEN 1 AND 25");
                    table.CheckConstraint("CK_Debts_StatementDay", "[StatementDay] BETWEEN 1 AND 31");
                });

            migrationBuilder.CreateTable(
                name: "Snapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CapturedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    TotalDebtBalance = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    MonthlyIncome = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    MonthlyBills = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    MonthlyMinDebtPayments = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Snapshots", x => x.Id);
                    table.CheckConstraint("CK_Snapshots_MonthlyBills", "[MonthlyBills] >= 0");
                    table.CheckConstraint("CK_Snapshots_MonthlyIncome", "[MonthlyIncome] >= 0");
                    table.CheckConstraint("CK_Snapshots_MonthlyMinDebtPayments", "[MonthlyMinDebtPayments] >= 0");
                    table.CheckConstraint("CK_Snapshots_TotalDebtBalance", "[TotalDebtBalance] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    PasswordHash = table.Column<string>(type: "varchar(120)", unicode: false, maxLength: 120, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IncomeSources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OwnerUserId = table.Column<int>(type: "int", nullable: false),
                    PayType = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    AmountPerCheck = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    PayWeekday = table.Column<byte>(type: "tinyint", nullable: true),
                    FirstPayDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PayDay1 = table.Column<byte>(type: "tinyint", nullable: true),
                    PayDay2 = table.Column<byte>(type: "tinyint", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncomeSources", x => x.Id);
                    table.CheckConstraint("CK_IncomeSources_AmountPerCheck", "[AmountPerCheck] > 0");
                    table.CheckConstraint("CK_IncomeSources_PayDay1", "[PayDay1] BETWEEN 1 AND 31");
                    table.CheckConstraint("CK_IncomeSources_PayDay2", "[PayDay2] BETWEEN 1 AND 31");
                    table.CheckConstraint("CK_IncomeSources_PayDayOrder", "[PayDay2] > [PayDay1]");
                    table.CheckConstraint("CK_IncomeSources_PayType", "[PayType] IN ('weekly', 'biweekly', 'semi-monthly')");
                    table.CheckConstraint("CK_IncomeSources_PayWeekday", "[PayWeekday] BETWEEN 0 AND 6");
                    table.CheckConstraint("CK_IncomeSources_Schedule", "   ([PayType] = 'weekly'       AND [PayWeekday] IS NOT NULL AND [FirstPayDate] IS NULL\n                               AND [PayDay1]    IS NULL     AND [PayDay2]      IS NULL)\nOR ([PayType] = 'biweekly'     AND [PayWeekday] IS NOT NULL AND [FirstPayDate] IS NOT NULL\n                               AND [PayDay1]    IS NULL     AND [PayDay2]      IS NULL)\nOR ([PayType] = 'semi-monthly' AND [PayWeekday] IS NULL     AND [FirstPayDate] IS NULL\n                               AND [PayDay1]    IS NOT NULL AND [PayDay2]      IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_IncomeSources_Users",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SpendingLogEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Category = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    DebtId = table.Column<int>(type: "int", nullable: true),
                    BillId = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Direction = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: true),
                    MinPaymentAtTime = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    PlannedAmount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    ActualAmount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LoggedByUserId = table.Column<int>(type: "int", nullable: false),
                    LoggedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpendingLogEntries", x => x.Id);
                    table.CheckConstraint("CK_SpendingLogEntries_ActualAmount", "[ActualAmount] > 0");
                    table.CheckConstraint("CK_SpendingLogEntries_Category", "[Category] IN ('debt', 'bill', 'other')");
                    table.CheckConstraint("CK_SpendingLogEntries_Direction", "[Direction] IN ('payment', 'charge')");
                    table.CheckConstraint("CK_SpendingLogEntries_MinPaymentAtTime", "[MinPaymentAtTime] >= 0");
                    table.CheckConstraint("CK_SpendingLogEntries_PlannedAmount", "[PlannedAmount] > 0");
                    table.CheckConstraint("CK_SpendingLogEntries_Target", "   ([Category] = 'debt'  AND [Direction] = 'payment'\n        AND [DebtId] IS NOT NULL AND [BillId] IS NULL\n        AND [Description] IS NULL AND [MinPaymentAtTime] IS NOT NULL)\nOR ([Category] = 'debt'  AND [Direction] = 'charge'\n        AND [DebtId] IS NOT NULL AND [BillId] IS NULL\n        AND [Description] IS NULL AND [MinPaymentAtTime] IS NULL)\nOR ([Category] = 'bill'  AND [Direction] IS NULL\n        AND [DebtId] IS NULL     AND [BillId] IS NOT NULL\n        AND [Description] IS NULL AND [MinPaymentAtTime] IS NULL)\nOR ([Category] = 'other' AND [Direction] IS NULL\n        AND [DebtId] IS NULL     AND [BillId] IS NULL\n        AND [Description] IS NOT NULL AND [MinPaymentAtTime] IS NULL)");
                    table.ForeignKey(
                        name: "FK_SpendingLogEntries_Bills",
                        column: x => x.BillId,
                        principalTable: "Bills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SpendingLogEntries_Debts",
                        column: x => x.DebtId,
                        principalTable: "Debts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SpendingLogEntries_Users",
                        column: x => x.LoggedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bills_Name",
                table: "Bills",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Debts_Name",
                table: "Debts",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IncomeSources_Name",
                table: "IncomeSources",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IncomeSources_OwnerUserId",
                table: "IncomeSources",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SpendingLogEntries_BillId",
                table: "SpendingLogEntries",
                column: "BillId");

            migrationBuilder.CreateIndex(
                name: "IX_SpendingLogEntries_DebtId",
                table: "SpendingLogEntries",
                column: "DebtId");

            migrationBuilder.CreateIndex(
                name: "IX_SpendingLogEntries_LoggedByUserId",
                table: "SpendingLogEntries",
                column: "LoggedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IncomeSources");

            migrationBuilder.DropTable(
                name: "Snapshots");

            migrationBuilder.DropTable(
                name: "SpendingLogEntries");

            migrationBuilder.DropTable(
                name: "Bills");

            migrationBuilder.DropTable(
                name: "Debts");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
