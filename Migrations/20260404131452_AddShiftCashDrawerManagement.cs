using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZoneBill_Lloren.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftCashDrawerManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PosShifts",
                columns: table => new
                {
                    ShiftId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BusinessId = table.Column<int>(type: "int", nullable: false),
                    CashierId = table.Column<int>(type: "int", nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OpeningCash = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ExpectedCash = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ActualCash = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Variance = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Open"),
                    Notes = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PosShifts", x => x.ShiftId);
                    table.ForeignKey(
                        name: "FK_PosShifts_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "BusinessId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PosShifts_Users_CashierId",
                        column: x => x.CashierId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CashDrawerTransactions",
                columns: table => new
                {
                    DrawerTransactionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BusinessId = table.Column<int>(type: "int", nullable: false),
                    ShiftId = table.Column<int>(type: "int", nullable: false),
                    TransactionType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashDrawerTransactions", x => x.DrawerTransactionId);
                    table.ForeignKey(
                        name: "FK_CashDrawerTransactions_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "BusinessId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashDrawerTransactions_PosShifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "PosShifts",
                        principalColumn: "ShiftId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashDrawerTransactions_BusinessId_ShiftId_CreatedAt",
                table: "CashDrawerTransactions",
                columns: new[] { "BusinessId", "ShiftId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CashDrawerTransactions_ShiftId",
                table: "CashDrawerTransactions",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_PosShifts_BusinessId_CashierId_Status_OpenedAt",
                table: "PosShifts",
                columns: new[] { "BusinessId", "CashierId", "Status", "OpenedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PosShifts_CashierId",
                table: "PosShifts",
                column: "CashierId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashDrawerTransactions");

            migrationBuilder.DropTable(
                name: "PosShifts");
        }
    }
}
