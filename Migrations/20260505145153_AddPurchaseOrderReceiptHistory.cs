using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZoneBill_Lloren.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseOrderReceiptHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PurchaseOrderReceipts",
                columns: table => new
                {
                    PurchaseOrderReceiptId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PurchaseOrderId = table.Column<int>(type: "int", nullable: false),
                    BusinessId = table.Column<int>(type: "int", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    QuantityReceived = table.Column<int>(type: "int", nullable: false),
                    PreviousReceivedQuantity = table.Column<int>(type: "int", nullable: false),
                    NewReceivedQuantity = table.Column<int>(type: "int", nullable: false),
                    PreviousStock = table.Column<int>(type: "int", nullable: false),
                    NewStock = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderReceipts", x => x.PurchaseOrderReceiptId);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderReceipts_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "BusinessId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderReceipts_MenuItems_ItemId",
                        column: x => x.ItemId,
                        principalTable: "MenuItems",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderReceipts_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "PurchaseOrderId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderReceipts_BusinessId_ItemId_ReceivedAt",
                table: "PurchaseOrderReceipts",
                columns: new[] { "BusinessId", "ItemId", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderReceipts_ItemId",
                table: "PurchaseOrderReceipts",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderReceipts_PurchaseOrderId_ReceivedAt",
                table: "PurchaseOrderReceipts",
                columns: new[] { "PurchaseOrderId", "ReceivedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PurchaseOrderReceipts");
        }
    }
}
