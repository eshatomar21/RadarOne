using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Radar_CRM.Migrations
{
    /// <inheritdoc />
    public partial class blacktesting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Leads",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Accounts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CallRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SalespersonPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RecordingUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DurationSeconds = table.Column<int>(type: "int", nullable: false),
                    TranscriptionText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AiSummary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CallDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LeadId = table.Column<int>(type: "int", nullable: true),
                    AccountId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CallRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CallRecords_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CallRecords_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CallRecords_AccountId",
                table: "CallRecords",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_CallRecords_LeadId",
                table: "CallRecords",
                column: "LeadId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CallRecords");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "Accounts");
        }
    }
}
