using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Radar_CRM.Migrations
{
    /// <inheritdoc />
    public partial class radarcrmchangeten : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductPaymentRow_Leads_LeadId1",
                table: "ProductPaymentRow");

            migrationBuilder.DropIndex(
                name: "IX_ProductPaymentRow_LeadId1",
                table: "ProductPaymentRow");

            migrationBuilder.DropColumn(
                name: "LeadId1",
                table: "ProductPaymentRow");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LeadId1",
                table: "ProductPaymentRow",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductPaymentRow_LeadId1",
                table: "ProductPaymentRow",
                column: "LeadId1");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductPaymentRow_Leads_LeadId1",
                table: "ProductPaymentRow",
                column: "LeadId1",
                principalTable: "Leads",
                principalColumn: "Id");
        }
    }
}
