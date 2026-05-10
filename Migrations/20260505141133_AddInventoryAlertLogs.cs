using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZoneBill_Lloren.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryAlertLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InventoryAlertLogs",
                columns: table => new
                {
                    InventoryAlertLogId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BusinessId = table.Column<int>(type: "int", nullable: false),
                    AlertType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TriggerSource = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RecipientEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RecipientName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    RecommendationCount = table.Column<int>(type: "int", nullable: false),
                    RecommendedUnits = table.Column<int>(type: "int", nullable: false),
                    AlertSignature = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RecommendationSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryAlertLogs", x => x.InventoryAlertLogId);
                    table.ForeignKey(
                        name: "FK_InventoryAlertLogs_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "BusinessId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryAlertLogs_BusinessId_AlertType_AlertSignature_SentAt",
                table: "InventoryAlertLogs",
                columns: new[] { "BusinessId", "AlertType", "AlertSignature", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryAlertLogs_BusinessId_SentAt",
                table: "InventoryAlertLogs",
                columns: new[] { "BusinessId", "SentAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryAlertLogs");
        }
    }
}
