using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Radar_CRM.Migrations
{
    /// <inheritdoc />
    public partial class collectionofbooks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssignmentRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RuleName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TargetModule = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConditionField = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConditionOperator = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConditionValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssignToUserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignmentRules", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssignmentRules");
        }
    }
}
