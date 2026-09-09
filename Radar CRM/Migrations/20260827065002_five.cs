using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Radar_CRM.Migrations
{
    /// <inheritdoc />
    public partial class five : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Leads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LeadOwner = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AccountOwner = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LeadName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AccountName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AccountType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MobileNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmailID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SocialLeadID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CampaignSource = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CurrentStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CoOwner = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DemoOwner = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDateAndTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ContactName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GroupName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AlternateMobile = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AlternateEmailID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Pipeline = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewSoftware = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MetaCampaignName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DataSources = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LeadCreatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Stage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Budget = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ExpectedRevenue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    LeadStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TimePeriodToBuy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Probability = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Addr1_Country = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Addr1_FlatHouse = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Addr1_Street = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Addr1_City = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Addr1_State = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Addr1_Zip = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Addr1_Coordinates = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Addr2_Country = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Addr2_FlatHouse = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Addr2_Street = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Addr2_City = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Addr2_State = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Addr2_Zip = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Addr2_Coordinates = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsHomeopathicDoctor = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClinicType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WorkType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HasComputer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Age = table.Column<int>(type: "int", nullable: true),
                    YearOfPractice = table.Column<int>(type: "int", nullable: true),
                    TotalExperience = table.Column<int>(type: "int", nullable: true),
                    AveragePatientFee = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    NumberOfClinics = table.Column<int>(type: "int", nullable: true),
                    PatientsPerDay = table.Column<int>(type: "int", nullable: true),
                    Qualification = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    YearOfPassing = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CollegeName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CurrentlyUsingSoftware = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RadarOpusVersion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CurrentSoftwareName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RadarOpusLicenseNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProductPackage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PurchaseValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PurchaseDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaymentStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomerStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DealType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DealValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PackageSelected = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Discount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SubTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Taxes = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Adjustment = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    GrandTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PaymentMode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PaymentType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContactPerson1 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContactPerson2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContactPerson3 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Contact1Phone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Contact2Phone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Contact3Phone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FirstCallDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextFollowUpDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConversationRemarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastContactDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InterestedPackage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LostReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LeadProfile = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductPaymentRow",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LeadId = table.Column<int>(type: "int", nullable: false),
                    DealType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: true),
                    ProductPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Total = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DiscountPercent = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    FinalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductPaymentRow", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductPaymentRow_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductPaymentRow_LeadId",
                table: "ProductPaymentRow",
                column: "LeadId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductPaymentRow");

            migrationBuilder.DropTable(
                name: "Leads");
        }
    }
}
