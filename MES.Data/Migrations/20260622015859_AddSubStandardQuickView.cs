using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSubStandardQuickView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SubStandardQuickView",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StandardNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ChemicalComposition = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    HydrostaticTest = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EddyCurrent = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UltrasonicTest = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_SubStandardQuickView", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UK_SubStandardQuickView_StandardNo",
                table: "SubStandardQuickView",
                column: "StandardNo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubStandardQuickView");
        }
    }
}
