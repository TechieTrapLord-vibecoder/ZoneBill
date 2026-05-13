using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZoneBill_Lloren.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantAuditLogs",
                columns: table => new
                {
                    LogId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BusinessId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    UserRole = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ActionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Details = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantAuditLogs", x => x.LogId);
                    table.ForeignKey(
                        name: "FK_TenantAuditLogs_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "BusinessId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenantAuditLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantAuditLogs_BusinessId",
                table: "TenantAuditLogs",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantAuditLogs_UserId",
                table: "TenantAuditLogs",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantAuditLogs");
        }
    }
}
