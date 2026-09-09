using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Radar_CRM.Migrations
{
    /// <inheritdoc />
    public partial class radarcrmtwenty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductPaymentRow_Accounts_AccountId",
                table: "ProductPaymentRow");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductPaymentRow_Leads_LeadId",
                table: "ProductPaymentRow");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductPaymentRow_Products_ProductId",
                table: "ProductPaymentRow");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ProductPaymentRow",
                table: "ProductPaymentRow");

            migrationBuilder.RenameTable(
                name: "ProductPaymentRow",
                newName: "ProductPaymentRows");

            migrationBuilder.RenameIndex(
                name: "IX_ProductPaymentRow_ProductId",
                table: "ProductPaymentRows",
                newName: "IX_ProductPaymentRows_ProductId");

            migrationBuilder.RenameIndex(
                name: "IX_ProductPaymentRow_LeadId",
                table: "ProductPaymentRows",
                newName: "IX_ProductPaymentRows_LeadId");

            migrationBuilder.RenameIndex(
                name: "IX_ProductPaymentRow_AccountId",
                table: "ProductPaymentRows",
                newName: "IX_ProductPaymentRows_AccountId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ProductPaymentRows",
                table: "ProductPaymentRows",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductPaymentRows_Accounts_AccountId",
                table: "ProductPaymentRows",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductPaymentRows_Leads_LeadId",
                table: "ProductPaymentRows",
                column: "LeadId",
                principalTable: "Leads",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductPaymentRows_Products_ProductId",
                table: "ProductPaymentRows",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductPaymentRows_Accounts_AccountId",
                table: "ProductPaymentRows");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductPaymentRows_Leads_LeadId",
                table: "ProductPaymentRows");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductPaymentRows_Products_ProductId",
                table: "ProductPaymentRows");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ProductPaymentRows",
                table: "ProductPaymentRows");

            migrationBuilder.RenameTable(
                name: "ProductPaymentRows",
                newName: "ProductPaymentRow");

            migrationBuilder.RenameIndex(
                name: "IX_ProductPaymentRows_ProductId",
                table: "ProductPaymentRow",
                newName: "IX_ProductPaymentRow_ProductId");

            migrationBuilder.RenameIndex(
                name: "IX_ProductPaymentRows_LeadId",
                table: "ProductPaymentRow",
                newName: "IX_ProductPaymentRow_LeadId");

            migrationBuilder.RenameIndex(
                name: "IX_ProductPaymentRows_AccountId",
                table: "ProductPaymentRow",
                newName: "IX_ProductPaymentRow_AccountId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ProductPaymentRow",
                table: "ProductPaymentRow",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductPaymentRow_Accounts_AccountId",
                table: "ProductPaymentRow",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductPaymentRow_Leads_LeadId",
                table: "ProductPaymentRow",
                column: "LeadId",
                principalTable: "Leads",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductPaymentRow_Products_ProductId",
                table: "ProductPaymentRow",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id");
        }
    }
}
