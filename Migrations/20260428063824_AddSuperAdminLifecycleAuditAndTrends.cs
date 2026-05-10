using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZoneBill_Lloren.Migrations
{
    /// <inheritdoc />
    public partial class AddSuperAdminLifecycleAuditAndTrends : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BusinessLifecycleEvents",
                columns: table => new
                {
                    EventId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BusinessId = table.Column<int>(type: "int", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PreviousValue = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ActorUserId = table.Column<int>(type: "int", nullable: true),
                    ActorName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessLifecycleEvents", x => x.EventId);
                    table.ForeignKey(
                        name: "FK_BusinessLifecycleEvents_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "BusinessId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SuperAdminAuditLogs",
                columns: table => new
                {
                    AuditLogId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ActionType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    BusinessId = table.Column<int>(type: "int", nullable: true),
                    BusinessName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Details = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ActorUserId = table.Column<int>(type: "int", nullable: true),
                    ActorName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SuperAdminAuditLogs", x => x.AuditLogId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessLifecycleEvents_BusinessId_CreatedAt",
                table: "BusinessLifecycleEvents",
                columns: new[] { "BusinessId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessLifecycleEvents_EventType_CreatedAt",
                table: "BusinessLifecycleEvents",
                columns: new[] { "EventType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SuperAdminAuditLogs_ActionType_CreatedAt",
                table: "SuperAdminAuditLogs",
                columns: new[] { "ActionType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SuperAdminAuditLogs_EntityType_CreatedAt",
                table: "SuperAdminAuditLogs",
                columns: new[] { "EntityType", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusinessLifecycleEvents");

            migrationBuilder.DropTable(
                name: "SuperAdminAuditLogs");
        }
    }
}
