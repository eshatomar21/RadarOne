using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Radar_CRM.Migrations
{
    /// <inheritdoc />
    public partial class korean : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Stage",
                table: "Leads");

            migrationBuilder.AddColumn<string>(
                name: "Stage",
                table: "Deals",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Stage",
                table: "Deals");

            migrationBuilder.AddColumn<string>(
                name: "Stage",
                table: "Leads",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
