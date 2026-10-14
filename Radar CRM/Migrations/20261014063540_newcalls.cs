using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Radar_CRM.Migrations
{
    /// <inheritdoc />
    public partial class newcalls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GapAnalysis",
                table: "CallRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecommendedProducts",
                table: "CallRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SalespersonConfidence",
                table: "CallRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SalespersonEmpathy",
                table: "CallRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SalespersonTone",
                table: "CallRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserNeeds",
                table: "CallRecords",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GapAnalysis",
                table: "CallRecords");

            migrationBuilder.DropColumn(
                name: "RecommendedProducts",
                table: "CallRecords");

            migrationBuilder.DropColumn(
                name: "SalespersonConfidence",
                table: "CallRecords");

            migrationBuilder.DropColumn(
                name: "SalespersonEmpathy",
                table: "CallRecords");

            migrationBuilder.DropColumn(
                name: "SalespersonTone",
                table: "CallRecords");

            migrationBuilder.DropColumn(
                name: "UserNeeds",
                table: "CallRecords");
        }
    }
}
