using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Radar_CRM.Migrations
{
    /// <inheritdoc />
    public partial class notes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Note_Tasks_LeadId",
                table: "Note");

            migrationBuilder.CreateIndex(
                name: "IX_Note_TaskId",
                table: "Note",
                column: "TaskId");

            migrationBuilder.AddForeignKey(
                name: "FK_Note_Tasks_TaskId",
                table: "Note",
                column: "TaskId",
                principalTable: "Tasks",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Note_Tasks_TaskId",
                table: "Note");

            migrationBuilder.DropIndex(
                name: "IX_Note_TaskId",
                table: "Note");

            migrationBuilder.AddForeignKey(
                name: "FK_Note_Tasks_LeadId",
                table: "Note",
                column: "LeadId",
                principalTable: "Tasks",
                principalColumn: "Id");
        }
    }
}
