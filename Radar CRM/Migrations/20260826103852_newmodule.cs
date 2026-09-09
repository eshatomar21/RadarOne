using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Radar_CRM.Migrations
{
    /// <inheritdoc />
    public partial class newmodule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DataSource = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AccountOwner = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CoOwner = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AccountType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateOfEntry = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CurrentStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContactStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AccountName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MobileNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AlternateMobile = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContactPersonName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AlternateEmailID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProfilePendingReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QualificationStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Addr1_Country = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Addr1_FlatHouse = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Addr1_Street = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Addr1_City = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Addr1_State = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Addr1_Zip = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Addr1_Latitude = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Addr1_Longitude = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Addr2_Country = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Addr2_FlatHouse = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Addr2_Street = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Addr2_City = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Addr2_State = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Addr2_Zip = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Addr2_Latitude = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Addr2_Longitude = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsHomeopathicDoctor = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClinicType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    YearsOfPractice = table.Column<int>(type: "int", nullable: true),
                    Qualification = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    YearOfPassing = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AveragePatientFee = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WorkType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HasComputer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PatientsPerDay = table.Column<int>(type: "int", nullable: true),
                    CollegeName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TotalExperience = table.Column<int>(type: "int", nullable: true),
                    NumberOfClinics = table.Column<int>(type: "int", nullable: true),
                    Age = table.Column<int>(type: "int", nullable: true),
                    CurrentlyUsingSoftware = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CurrentSoftwareName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProductPurchased = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PurchaseDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PurchaseValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PaymentType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PaymentStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProfileCompletionPercentage = table.Column<int>(type: "int", nullable: true),
                    ReferralSource = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContactPerson1 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContactPerson2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContactPerson3 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Contact1Phone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Contact2Phone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Contact3Phone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDuplicated = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Accounts");
        }
    }
}
