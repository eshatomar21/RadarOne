using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Radar_CRM.Migrations
{
    /// <inheritdoc />
    public partial class grrengreen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Postcodes");

            migrationBuilder.DropColumn(
                name: "CreatedTime",
                table: "Postcodes");

            migrationBuilder.DropColumn(
                name: "LayoutId",
                table: "Postcodes");

            migrationBuilder.DropColumn(
                name: "ModifiedById",
                table: "Postcodes");

            migrationBuilder.DropColumn(
                name: "ModifiedTime",
                table: "Postcodes");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Postcodes");

            migrationBuilder.DropColumn(
                name: "RecordId",
                table: "Postcodes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedById",
                table: "Postcodes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedTime",
                table: "Postcodes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LayoutId",
                table: "Postcodes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedById",
                table: "Postcodes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedTime",
                table: "Postcodes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "Postcodes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecordId",
                table: "Postcodes",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
