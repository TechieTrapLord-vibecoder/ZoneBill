using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZoneBill_Lloren.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryDemandForecasting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InventoryForecastHorizonDays",
                table: "Businesses",
                type: "int",
                nullable: false,
                defaultValue: 7);

            migrationBuilder.AddColumn<int>(
                name: "InventoryForecastLookbackDays",
                table: "Businesses",
                type: "int",
                nullable: false,
                defaultValue: 28);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InventoryForecastHorizonDays",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "InventoryForecastLookbackDays",
                table: "Businesses");
        }
    }
}
