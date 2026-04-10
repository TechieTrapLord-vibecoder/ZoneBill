using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZoneBill_Lloren.Migrations
{
    /// <inheritdoc />
    public partial class AddTableLayoutAndSplitCheckout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Spaces_BusinessId",
                table: "Spaces");

            migrationBuilder.AddColumn<int>(
                name: "Capacity",
                table: "Spaces",
                type: "int",
                nullable: false,
                defaultValue: 4);

            migrationBuilder.AddColumn<string>(
                name: "FloorArea",
                table: "Spaces",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Main Floor");

            migrationBuilder.CreateIndex(
                name: "IX_Spaces_BusinessId_FloorArea_CurrentStatus",
                table: "Spaces",
                columns: new[] { "BusinessId", "FloorArea", "CurrentStatus" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Spaces_BusinessId_FloorArea_CurrentStatus",
                table: "Spaces");

            migrationBuilder.DropColumn(
                name: "Capacity",
                table: "Spaces");

            migrationBuilder.DropColumn(
                name: "FloorArea",
                table: "Spaces");

            migrationBuilder.CreateIndex(
                name: "IX_Spaces_BusinessId",
                table: "Spaces",
                column: "BusinessId");
        }
    }
}
