using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Radar_CRM.Migrations
{
    /// <inheritdoc />
    public partial class sectionupdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccountId",
                table: "ProductPaymentRow",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ProductId",
                table: "ProductPaymentRow",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ProductPaymentRow_AccountId",
                table: "ProductPaymentRow",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductPaymentRow_ProductId",
                table: "ProductPaymentRow",
                column: "ProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductPaymentRow_Accounts_AccountId",
                table: "ProductPaymentRow",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductPaymentRow_Products_ProductId",
                table: "ProductPaymentRow",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductPaymentRow_Accounts_AccountId",
                table: "ProductPaymentRow");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductPaymentRow_Products_ProductId",
                table: "ProductPaymentRow");

            migrationBuilder.DropIndex(
                name: "IX_ProductPaymentRow_AccountId",
                table: "ProductPaymentRow");

            migrationBuilder.DropIndex(
                name: "IX_ProductPaymentRow_ProductId",
                table: "ProductPaymentRow");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "ProductPaymentRow");

            migrationBuilder.DropColumn(
                name: "ProductId",
                table: "ProductPaymentRow");
        }
    }
}
