using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Radar_CRM.Migrations
{
    /// <inheritdoc />
    public partial class website : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModuleName",
                table: "Note");

            migrationBuilder.DropColumn(
                name: "NoteOwner",
                table: "Note");

            migrationBuilder.DropColumn(
                name: "RecordId",
                table: "Note");

            migrationBuilder.DropColumn(
                name: "AccountName",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "AccountOwner",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "CoOwner",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "DemoOwner",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "AccountOwner",
                table: "Accounts");

            migrationBuilder.RenameColumn(
                name: "LeadOwner",
                table: "Leads",
                newName: "ZohoRecordId");

            migrationBuilder.RenameColumn(
                name: "DealOwner",
                table: "Deals",
                newName: "ZohoRecordId");

            migrationBuilder.RenameColumn(
                name: "CoOwner",
                table: "Accounts",
                newName: "ZohoRecordId");

            migrationBuilder.AddColumn<int>(
                name: "AccountId",
                table: "Tasks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DealId",
                table: "Tasks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LeadId",
                table: "Tasks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "Tasks",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LeadId1",
                table: "ProductPaymentRow",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AccountId",
                table: "Note",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DealId",
                table: "Note",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LeadId",
                table: "Note",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NoteOwnerId",
                table: "Note",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TaskId",
                table: "Note",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ZohoRecordId",
                table: "Note",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AccountId",
                table: "Leads",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccountOwnerId",
                table: "Leads",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CoOwnerId",
                table: "Leads",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DemoOwnerId",
                table: "Leads",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LeadOwnerId",
                table: "Leads",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AccountId",
                table: "Deals",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DealOwnerId",
                table: "Deals",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AccountName",
                table: "Accounts",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccountOwnerId",
                table: "Accounts",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CoOwnerId",
                table: "Accounts",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_AccountId",
                table: "Tasks",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_DealId",
                table: "Tasks",
                column: "DealId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_LeadId",
                table: "Tasks",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_UserId",
                table: "Tasks",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductPaymentRow_LeadId1",
                table: "ProductPaymentRow",
                column: "LeadId1");

            migrationBuilder.CreateIndex(
                name: "IX_Note_AccountId",
                table: "Note",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Note_DealId",
                table: "Note",
                column: "DealId");

            migrationBuilder.CreateIndex(
                name: "IX_Note_LeadId",
                table: "Note",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_Note_NoteOwnerId",
                table: "Note",
                column: "NoteOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_AccountId",
                table: "Leads",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_AccountOwnerId",
                table: "Leads",
                column: "AccountOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_CoOwnerId",
                table: "Leads",
                column: "CoOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_DemoOwnerId",
                table: "Leads",
                column: "DemoOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_LeadOwnerId",
                table: "Leads",
                column: "LeadOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Deals_AccountId",
                table: "Deals",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Deals_DealOwnerId",
                table: "Deals",
                column: "DealOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_AccountOwnerId",
                table: "Accounts",
                column: "AccountOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_CoOwnerId",
                table: "Accounts",
                column: "CoOwnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Accounts_Users_AccountOwnerId",
                table: "Accounts",
                column: "AccountOwnerId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Accounts_Users_CoOwnerId",
                table: "Accounts",
                column: "CoOwnerId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Deals_Accounts_AccountId",
                table: "Deals",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Deals_Users_DealOwnerId",
                table: "Deals",
                column: "DealOwnerId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Leads_Accounts_AccountId",
                table: "Leads",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Leads_Users_AccountOwnerId",
                table: "Leads",
                column: "AccountOwnerId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Leads_Users_CoOwnerId",
                table: "Leads",
                column: "CoOwnerId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Leads_Users_DemoOwnerId",
                table: "Leads",
                column: "DemoOwnerId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Leads_Users_LeadOwnerId",
                table: "Leads",
                column: "LeadOwnerId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Note_Accounts_AccountId",
                table: "Note",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Note_Deals_DealId",
                table: "Note",
                column: "DealId",
                principalTable: "Deals",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Note_Leads_LeadId",
                table: "Note",
                column: "LeadId",
                principalTable: "Leads",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Note_Tasks_LeadId",
                table: "Note",
                column: "LeadId",
                principalTable: "Tasks",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Note_Users_NoteOwnerId",
                table: "Note",
                column: "NoteOwnerId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductPaymentRow_Leads_LeadId1",
                table: "ProductPaymentRow",
                column: "LeadId1",
                principalTable: "Leads",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_Accounts_AccountId",
                table: "Tasks",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_Deals_DealId",
                table: "Tasks",
                column: "DealId",
                principalTable: "Deals",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_Leads_LeadId",
                table: "Tasks",
                column: "LeadId",
                principalTable: "Leads",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_Users_UserId",
                table: "Tasks",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Accounts_Users_AccountOwnerId",
                table: "Accounts");

            migrationBuilder.DropForeignKey(
                name: "FK_Accounts_Users_CoOwnerId",
                table: "Accounts");

            migrationBuilder.DropForeignKey(
                name: "FK_Deals_Accounts_AccountId",
                table: "Deals");

            migrationBuilder.DropForeignKey(
                name: "FK_Deals_Users_DealOwnerId",
                table: "Deals");

            migrationBuilder.DropForeignKey(
                name: "FK_Leads_Accounts_AccountId",
                table: "Leads");

            migrationBuilder.DropForeignKey(
                name: "FK_Leads_Users_AccountOwnerId",
                table: "Leads");

            migrationBuilder.DropForeignKey(
                name: "FK_Leads_Users_CoOwnerId",
                table: "Leads");

            migrationBuilder.DropForeignKey(
                name: "FK_Leads_Users_DemoOwnerId",
                table: "Leads");

            migrationBuilder.DropForeignKey(
                name: "FK_Leads_Users_LeadOwnerId",
                table: "Leads");

            migrationBuilder.DropForeignKey(
                name: "FK_Note_Accounts_AccountId",
                table: "Note");

            migrationBuilder.DropForeignKey(
                name: "FK_Note_Deals_DealId",
                table: "Note");

            migrationBuilder.DropForeignKey(
                name: "FK_Note_Leads_LeadId",
                table: "Note");

            migrationBuilder.DropForeignKey(
                name: "FK_Note_Tasks_LeadId",
                table: "Note");

            migrationBuilder.DropForeignKey(
                name: "FK_Note_Users_NoteOwnerId",
                table: "Note");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductPaymentRow_Leads_LeadId1",
                table: "ProductPaymentRow");

            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_Accounts_AccountId",
                table: "Tasks");

            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_Deals_DealId",
                table: "Tasks");

            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_Leads_LeadId",
                table: "Tasks");

            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_Users_UserId",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_AccountId",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_DealId",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_LeadId",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_UserId",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_ProductPaymentRow_LeadId1",
                table: "ProductPaymentRow");

            migrationBuilder.DropIndex(
                name: "IX_Note_AccountId",
                table: "Note");

            migrationBuilder.DropIndex(
                name: "IX_Note_DealId",
                table: "Note");

            migrationBuilder.DropIndex(
                name: "IX_Note_LeadId",
                table: "Note");

            migrationBuilder.DropIndex(
                name: "IX_Note_NoteOwnerId",
                table: "Note");

            migrationBuilder.DropIndex(
                name: "IX_Leads_AccountId",
                table: "Leads");

            migrationBuilder.DropIndex(
                name: "IX_Leads_AccountOwnerId",
                table: "Leads");

            migrationBuilder.DropIndex(
                name: "IX_Leads_CoOwnerId",
                table: "Leads");

            migrationBuilder.DropIndex(
                name: "IX_Leads_DemoOwnerId",
                table: "Leads");

            migrationBuilder.DropIndex(
                name: "IX_Leads_LeadOwnerId",
                table: "Leads");

            migrationBuilder.DropIndex(
                name: "IX_Deals_AccountId",
                table: "Deals");

            migrationBuilder.DropIndex(
                name: "IX_Deals_DealOwnerId",
                table: "Deals");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_AccountOwnerId",
                table: "Accounts");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_CoOwnerId",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "DealId",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "LeadId",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "LeadId1",
                table: "ProductPaymentRow");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "Note");

            migrationBuilder.DropColumn(
                name: "DealId",
                table: "Note");

            migrationBuilder.DropColumn(
                name: "LeadId",
                table: "Note");

            migrationBuilder.DropColumn(
                name: "NoteOwnerId",
                table: "Note");

            migrationBuilder.DropColumn(
                name: "TaskId",
                table: "Note");

            migrationBuilder.DropColumn(
                name: "ZohoRecordId",
                table: "Note");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "AccountOwnerId",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "CoOwnerId",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "DemoOwnerId",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "LeadOwnerId",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "DealOwnerId",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "AccountOwnerId",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "CoOwnerId",
                table: "Accounts");

            migrationBuilder.RenameColumn(
                name: "ZohoRecordId",
                table: "Leads",
                newName: "LeadOwner");

            migrationBuilder.RenameColumn(
                name: "ZohoRecordId",
                table: "Deals",
                newName: "DealOwner");

            migrationBuilder.RenameColumn(
                name: "ZohoRecordId",
                table: "Accounts",
                newName: "CoOwner");

            migrationBuilder.AddColumn<string>(
                name: "ModuleName",
                table: "Note",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NoteOwner",
                table: "Note",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "RecordId",
                table: "Note",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AccountName",
                table: "Leads",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccountOwner",
                table: "Leads",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CoOwner",
                table: "Leads",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DemoOwner",
                table: "Leads",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AccountName",
                table: "Accounts",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "AccountOwner",
                table: "Accounts",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
