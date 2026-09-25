using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Radar_CRM.Migrations
{
    /// <inheritdoc />
    public partial class putput : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Title",
                table: "Note",
                newName: "NoteTitle");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "NoteTitle",
                table: "Note",
                newName: "Title");
        }
    }
}
