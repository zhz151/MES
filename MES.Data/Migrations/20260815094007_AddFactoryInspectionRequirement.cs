using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFactoryInspectionRequirement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FactoryInspectionRequirement",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StandardNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ChemicalComposition = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PmiInspection = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SurfaceInspection = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Dimension = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Endoscopy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    HydrostaticTest = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UnderwaterPressure = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EddyCurrent = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UltrasonicTest = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PortColoring = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RadiographicTest = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    HardnessRockwell = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    HardnessBrinell = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    HardnessVickers = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TensileRoomTemp = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TensileHighTemp = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    WeldJointTensile = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ImpactTest = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    WeldJointImpact = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FlatteningTest = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FlaringTest = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ExpandingTest = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BendTest = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    WeldJointBend = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    GrainSize = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IntergranularCorrosion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PittingCorrosion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FerriteContent = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Macrostructure = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FactoryInspectionRequirement", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UK_FactoryInspectionRequirement_StandardNo",
                table: "FactoryInspectionRequirement",
                column: "StandardNo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FactoryInspectionRequirement");
        }
    }
}
