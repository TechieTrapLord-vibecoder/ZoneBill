using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZoneBill_Lloren.Migrations
{
    /// <inheritdoc />
    public partial class AddStripePriceIdToSubscriptionPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StripePriceId",
                table: "SubscriptionPlans",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StripePriceId",
                table: "SubscriptionPlans");
        }
    }
}
