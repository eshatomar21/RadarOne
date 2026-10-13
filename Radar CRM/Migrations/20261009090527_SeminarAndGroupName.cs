using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Radar_CRM.Migrations
{
    /// <inheritdoc />
    public partial class SeminarAndGroupName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GroupName",
                table: "Deals",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SeminarName",
                table: "Deals",
                type: "nvarchar(max)",
                nullable: true);

            //migrationBuilder.CreateTable(
            //    name: "TimelineEvents",
            //    columns: table => new
            //    {
            //        Id = table.Column<int>(type: "int", nullable: false)
            //            .Annotation("SqlServer:Identity", "1, 1"),
            //        EntityType = table.Column<string>(type: "nvarchar(max)", nullable: false),
            //        EntityId = table.Column<int>(type: "int", nullable: false),
            //        EventType = table.Column<string>(type: "nvarchar(max)", nullable: false),
            //        ActionDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
            //        FieldName = table.Column<string>(type: "nvarchar(max)", nullable: false),
            //        OldValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
            //        NewValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
            //        CreatedById = table.Column<string>(type: "nvarchar(max)", nullable: false),
            //        CreatedByName = table.Column<string>(type: "nvarchar(max)", nullable: false),
            //        CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
            //    },
            //    constraints: table =>
            //    {
            //        table.PrimaryKey("PK_TimelineEvents", x => x.Id);
            //    });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TimelineEvents");

            migrationBuilder.DropColumn(
                name: "GroupName",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "SeminarName",
                table: "Deals");
        }
    }
}
