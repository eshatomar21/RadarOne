using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Radar_CRM.Migrations
{
    /// <inheritdoc />
    public partial class sunghoon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccountName",
                table: "Note",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Note",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DealName",
                table: "Note",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LeadName",
                table: "Note",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Note",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedTime",
                table: "Note",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NoteContent",
                table: "Note",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaskName",
                table: "Note",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "Note",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateOfEntry",
                table: "Deals",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountName",
                table: "Note");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Note");

            migrationBuilder.DropColumn(
                name: "DealName",
                table: "Note");

            migrationBuilder.DropColumn(
                name: "LeadName",
                table: "Note");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Note");

            migrationBuilder.DropColumn(
                name: "ModifiedTime",
                table: "Note");

            migrationBuilder.DropColumn(
                name: "NoteContent",
                table: "Note");

            migrationBuilder.DropColumn(
                name: "TaskName",
                table: "Note");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "Note");

            migrationBuilder.DropColumn(
                name: "DateOfEntry",
                table: "Deals");
        }
    }
}
