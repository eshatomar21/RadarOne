using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Radar_CRM.Migrations
{
    /// <inheritdoc />
    public partial class MakeForeignKeysNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductPaymentRow_Accounts_AccountId",
                table: "ProductPaymentRow");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductPaymentRow_Products_ProductId",
                table: "ProductPaymentRow");

            migrationBuilder.AlterColumn<int>(
                name: "ProductId",
                table: "ProductPaymentRow",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "AccountId",
                table: "ProductPaymentRow",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductPaymentRow_Accounts_AccountId",
                table: "ProductPaymentRow",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductPaymentRow_Products_ProductId",
                table: "ProductPaymentRow",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id");
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

            migrationBuilder.AlterColumn<int>(
                name: "ProductId",
                table: "ProductPaymentRow",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "AccountId",
                table: "ProductPaymentRow",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

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
    }
}
