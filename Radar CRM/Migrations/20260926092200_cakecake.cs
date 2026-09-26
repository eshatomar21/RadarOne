using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Radar_CRM.Migrations
{
    /// <inheritdoc />
    public partial class cakecake : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ZohoRecordId",
                table: "Tasks",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ad",
                table: "Leads",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdCampaignName",
                table: "Leads",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AdClickDate",
                table: "Leads",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdGroupName",
                table: "Leads",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdNetwork",
                table: "Leads",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Adgroupid",
                table: "Leads",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Adid",
                table: "Leads",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClickType",
                table: "Leads",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConversionExportStatus",
                table: "Leads",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConversionExportedOn",
                table: "Leads",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CostPerClick",
                table: "Leads",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CostPerConversion",
                table: "Leads",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceType",
                table: "Leads",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Gadconfigid",
                table: "Leads",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Gclid",
                table: "Leads",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Keyword",
                table: "Leads",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Keywordid",
                table: "Leads",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedTime",
                table: "Leads",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NotInterestedReason",
                table: "Leads",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReasonForConversionFailure",
                table: "Leads",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SearchPartnerNetwork",
                table: "Leads",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Zcampaignid",
                table: "Leads",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddonModuleName",
                table: "Deals",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentPackage",
                table: "Deals",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DealType",
                table: "Deals",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DemoOwnerId",
                table: "Deals",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Discount",
                table: "Deals",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FinalAmount",
                table: "Deals",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GstAmount",
                table: "Deals",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceNumber",
                table: "Deals",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedTime",
                table: "Deals",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductCode",
                table: "Deals",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductName",
                table: "Deals",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Quantity",
                table: "Deals",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalWithGst",
                table: "Deals",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitPrice",
                table: "Deals",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ad",
                table: "Accounts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdCampaignName",
                table: "Accounts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AdClickDate",
                table: "Accounts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdGroupName",
                table: "Accounts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdNetwork",
                table: "Accounts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Adgroupid",
                table: "Accounts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Adid",
                table: "Accounts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClickType",
                table: "Accounts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConversionExportStatus",
                table: "Accounts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConversionExportedOn",
                table: "Accounts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CostPerClick",
                table: "Accounts",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CostPerConversion",
                table: "Accounts",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceType",
                table: "Accounts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Gadconfigid",
                table: "Accounts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Gclid",
                table: "Accounts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Keyword",
                table: "Accounts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Keywordid",
                table: "Accounts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LeadStage",
                table: "Accounts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OldLeadStatus",
                table: "Accounts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RadarOpusLicenseNo",
                table: "Accounts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RadarOpusVersion",
                table: "Accounts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReasonForConversionFailure",
                table: "Accounts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SearchPartnerNetwork",
                table: "Accounts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SocialLeadId",
                table: "Accounts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Zcampaignid",
                table: "Accounts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Deals_DemoOwnerId",
                table: "Deals",
                column: "DemoOwnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Deals_Users_DemoOwnerId",
                table: "Deals",
                column: "DemoOwnerId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Deals_Users_DemoOwnerId",
                table: "Deals");

            migrationBuilder.DropIndex(
                name: "IX_Deals_DemoOwnerId",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "ZohoRecordId",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "Ad",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "AdCampaignName",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "AdClickDate",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "AdGroupName",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "AdNetwork",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "Adgroupid",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "Adid",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "ClickType",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "ConversionExportStatus",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "ConversionExportedOn",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "CostPerClick",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "CostPerConversion",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "DeviceType",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "Gadconfigid",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "Gclid",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "Keyword",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "Keywordid",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "ModifiedTime",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "NotInterestedReason",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "ReasonForConversionFailure",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "SearchPartnerNetwork",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "Zcampaignid",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "AddonModuleName",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "CurrentPackage",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "DealType",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "DemoOwnerId",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "Discount",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "FinalAmount",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "GstAmount",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "InvoiceNumber",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "ModifiedTime",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "ProductCode",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "ProductName",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "TotalWithGst",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "UnitPrice",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "Ad",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "AdCampaignName",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "AdClickDate",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "AdGroupName",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "AdNetwork",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "Adgroupid",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "Adid",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "ClickType",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "ConversionExportStatus",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "ConversionExportedOn",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "CostPerClick",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "CostPerConversion",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "DeviceType",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "Gadconfigid",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "Gclid",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "Keyword",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "Keywordid",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "LeadStage",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "OldLeadStatus",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "RadarOpusLicenseNo",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "RadarOpusVersion",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "ReasonForConversionFailure",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "SearchPartnerNetwork",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "SocialLeadId",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "Zcampaignid",
                table: "Accounts");
        }
    }
}
