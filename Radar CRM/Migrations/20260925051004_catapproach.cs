using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Radar_CRM.Migrations
{
    /// <inheritdoc />
    public partial class catapproach : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LeadId",
                table: "Deals",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Deals_LeadId",
                table: "Deals",
                column: "LeadId");

            migrationBuilder.AddForeignKey(
                name: "FK_Deals_Leads_LeadId",
                table: "Deals",
                column: "LeadId",
                principalTable: "Leads",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Deals_Leads_LeadId",
                table: "Deals");

            migrationBuilder.DropIndex(
                name: "IX_Deals_LeadId",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "LeadId",
                table: "Deals");
        }
    }
}
