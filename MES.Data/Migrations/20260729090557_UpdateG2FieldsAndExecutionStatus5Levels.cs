using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateG2FieldsAndExecutionStatus5Levels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderChangeNotification");

            migrationBuilder.DropColumn(
                name: "LatestRequiredDate",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "MaterialPlanRate",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.RenameColumn(
                name: "LatestPlanDate",
                table: "WorkOrderExecutionSummary",
                newName: "TheoreticalCutoffDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TheoreticalCutoffDate",
                table: "WorkOrderExecutionSummary",
                newName: "LatestPlanDate");

            migrationBuilder.AddColumn<DateTime>(
                name: "LatestRequiredDate",
                table: "WorkOrderExecutionSummary",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaterialPlanRate",
                table: "WorkOrderExecutionSummary",
                type: "decimal(7,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "OrderChangeNotification",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChangeType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    OrderNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    WorkOrderCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderChangeNotification", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderChangeNotification_CreatedTime",
                table: "OrderChangeNotification",
                column: "CreatedTime");

            migrationBuilder.CreateIndex(
                name: "IX_OrderChangeNotification_IsRead",
                table: "OrderChangeNotification",
                column: "IsRead");

            migrationBuilder.CreateIndex(
                name: "IX_OrderChangeNotification_OrderNumber",
                table: "OrderChangeNotification",
                column: "OrderNumber");
        }
    }
}
