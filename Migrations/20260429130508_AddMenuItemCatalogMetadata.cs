using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZoneBill_Lloren.Migrations
{
    /// <inheritdoc />
    public partial class AddMenuItemCatalogMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MenuItems_BusinessId",
                table: "MenuItems");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "MenuItems",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "General");

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "MenuItems",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "MenuItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_BusinessId_Category_SortOrder_ItemName",
                table: "MenuItems",
                columns: new[] { "BusinessId", "Category", "SortOrder", "ItemName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MenuItems_BusinessId_Category_SortOrder_ItemName",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "MenuItems");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_BusinessId",
                table: "MenuItems",
                column: "BusinessId");
        }
    }
}
