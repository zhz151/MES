using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRoundBarPiercingPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RoundBarPiercingPlan",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkOrderId = table.Column<int>(type: "int", nullable: false),
                    PlanDate = table.Column<DateTime>(type: "date", nullable: false),
                    AdjustedWallThickness = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    YieldRate = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    InputMultiple = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    QualifiedRate = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Density = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    UnitWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    RawUnitWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    PlantGrade = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RawMaterialType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RoundBarSpec = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PiercingSpec = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequiredUnitWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    RequiredPieces = table.Column<int>(type: "int", nullable: true),
                    RequiredWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    RequiredDate = table.Column<DateTime>(type: "date", nullable: false),
                    ProcessPlan = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoundBarPiercingPlan", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoundBarPiercingPlan_WorkOrder_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RoundBarPiercingPlan_WorkOrderId",
                table: "RoundBarPiercingPlan",
                column: "WorkOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoundBarPiercingPlan");
        }
    }
}
