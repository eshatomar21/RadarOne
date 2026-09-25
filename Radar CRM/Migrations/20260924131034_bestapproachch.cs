using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Radar_CRM.Migrations
{
    /// <inheritdoc />
    public partial class bestapproachch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Leads",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Leads",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Deals",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Deals",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Deals");
        }
    }
}
