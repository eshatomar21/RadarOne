using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Radar_CRM.Migrations
{
    /// <inheritdoc />
    public partial class AddCallProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Age",
                table: "CallRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AveragePatientFee",
                table: "CallRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClinicType",
                table: "CallRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CollegeName",
                table: "CallRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentSoftwareName",
                table: "CallRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentlyUsingSoftware",
                table: "CallRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DateOfBirth",
                table: "CallRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HasComputer",
                table: "CallRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IsHomeopathicDoctor",
                table: "CallRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumberOfClinics",
                table: "CallRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PatientsPerDay",
                table: "CallRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Qualification",
                table: "CallRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TotalExperience",
                table: "CallRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkType",
                table: "CallRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "YearOfPassing",
                table: "CallRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "YearsOfPractice",
                table: "CallRecords",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Age",
                table: "CallRecords");

            migrationBuilder.DropColumn(
                name: "AveragePatientFee",
                table: "CallRecords");

            migrationBuilder.DropColumn(
                name: "ClinicType",
                table: "CallRecords");

            migrationBuilder.DropColumn(
                name: "CollegeName",
                table: "CallRecords");

            migrationBuilder.DropColumn(
                name: "CurrentSoftwareName",
                table: "CallRecords");

            migrationBuilder.DropColumn(
                name: "CurrentlyUsingSoftware",
                table: "CallRecords");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "CallRecords");

            migrationBuilder.DropColumn(
                name: "HasComputer",
                table: "CallRecords");

            migrationBuilder.DropColumn(
                name: "IsHomeopathicDoctor",
                table: "CallRecords");

            migrationBuilder.DropColumn(
                name: "NumberOfClinics",
                table: "CallRecords");

            migrationBuilder.DropColumn(
                name: "PatientsPerDay",
                table: "CallRecords");

            migrationBuilder.DropColumn(
                name: "Qualification",
                table: "CallRecords");

            migrationBuilder.DropColumn(
                name: "TotalExperience",
                table: "CallRecords");

            migrationBuilder.DropColumn(
                name: "WorkType",
                table: "CallRecords");

            migrationBuilder.DropColumn(
                name: "YearOfPassing",
                table: "CallRecords");

            migrationBuilder.DropColumn(
                name: "YearsOfPractice",
                table: "CallRecords");
        }
    }
}
