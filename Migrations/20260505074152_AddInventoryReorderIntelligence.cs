using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZoneBill_Lloren.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryReorderIntelligence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InventoryAlertEmail",
                table: "Businesses",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "InventoryAlertEnabled",
                table: "Businesses",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "InventoryLeadTimeDays",
                table: "Businesses",
                type: "int",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<int>(
                name: "InventoryReorderLookbackDays",
                table: "Businesses",
                type: "int",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<int>(
                name: "InventorySafetyStockDays",
                table: "Businesses",
                type: "int",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "InventoryTargetCoverageDays",
                table: "Businesses",
                type: "int",
                nullable: false,
                defaultValue: 7);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InventoryAlertEmail",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "InventoryAlertEnabled",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "InventoryLeadTimeDays",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "InventoryReorderLookbackDays",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "InventorySafetyStockDays",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "InventoryTargetCoverageDays",
                table: "Businesses");
        }
    }
}
