using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZoneBill_Lloren.Migrations
{
    /// <inheritdoc />
    public partial class AddPosAuditTrailLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PosAuditLogs",
                columns: table => new
                {
                    PosAuditLogId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BusinessId = table.Column<int>(type: "int", nullable: false),
                    CashierId = table.Column<int>(type: "int", nullable: false),
                    BookingId = table.Column<int>(type: "int", nullable: true),
                    ActionType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SourceSpaceId = table.Column<int>(type: "int", nullable: true),
                    SourceSpaceName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TargetSpaceId = table.Column<int>(type: "int", nullable: true),
                    TargetSpaceName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SplitCount = table.Column<int>(type: "int", nullable: true),
                    InvoiceIds = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Details = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PosAuditLogs", x => x.PosAuditLogId);
                    table.ForeignKey(
                        name: "FK_PosAuditLogs_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "BusinessId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PosAuditLogs_Users_CashierId",
                        column: x => x.CashierId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PosAuditLogs_BusinessId_ActionType_CreatedAt",
                table: "PosAuditLogs",
                columns: new[] { "BusinessId", "ActionType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PosAuditLogs_BusinessId_CashierId_CreatedAt",
                table: "PosAuditLogs",
                columns: new[] { "BusinessId", "CashierId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PosAuditLogs_CashierId",
                table: "PosAuditLogs",
                column: "CashierId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PosAuditLogs");
        }
    }
}
