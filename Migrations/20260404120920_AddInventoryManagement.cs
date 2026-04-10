using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZoneBill_Lloren.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CostPrice",
                table: "MenuItems",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "LowStockThreshold",
                table: "MenuItems",
                type: "int",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.CreateTable(
                name: "InventoryTransactions",
                columns: table => new
                {
                    InventoryTransactionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BusinessId = table.Column<int>(type: "int", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    QuantityChange = table.Column<int>(type: "int", nullable: false),
                    PreviousStock = table.Column<int>(type: "int", nullable: false),
                    NewStock = table.Column<int>(type: "int", nullable: false),
                    TransactionType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryTransactions", x => x.InventoryTransactionId);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "BusinessId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_MenuItems_ItemId",
                        column: x => x.ItemId,
                        principalTable: "MenuItems",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_BusinessId_ItemId_CreatedAt",
                table: "InventoryTransactions",
                columns: new[] { "BusinessId", "ItemId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_ItemId",
                table: "InventoryTransactions",
                column: "ItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryTransactions");

            migrationBuilder.DropColumn(
                name: "CostPrice",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "LowStockThreshold",
                table: "MenuItems");
        }
    }
}
