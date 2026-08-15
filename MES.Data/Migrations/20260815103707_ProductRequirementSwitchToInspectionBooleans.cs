using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProductRequirementSwitchToInspectionBooleans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MechanicalProperty",
                table: "ProductRequirement");

            migrationBuilder.DropColumn(
                name: "NdtRequirement",
                table: "ProductRequirement");

            migrationBuilder.DropColumn(
                name: "SurfaceQuality",
                table: "ProductRequirement");

            migrationBuilder.DropColumn(
                name: "ToleranceRequirement",
                table: "ProductRequirement");

            migrationBuilder.AlterColumn<bool>(
                name: "ChemicalComposition",
                table: "ProductRequirement",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "BendTest",
                table: "ProductRequirement",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Dimension",
                table: "ProductRequirement",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EddyCurrent",
                table: "ProductRequirement",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Endoscopy",
                table: "ProductRequirement",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ExpandingTest",
                table: "ProductRequirement",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "FerriteContent",
                table: "ProductRequirement",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "FlaringTest",
                table: "ProductRequirement",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "FlatteningTest",
                table: "ProductRequirement",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "GrainSize",
                table: "ProductRequirement",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HardnessBrinell",
                table: "ProductRequirement",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HardnessRockwell",
                table: "ProductRequirement",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HardnessVickers",
                table: "ProductRequirement",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HydrostaticTest",
                table: "ProductRequirement",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ImpactTest",
                table: "ProductRequirement",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IntergranularCorrosion",
                table: "ProductRequirement",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Macrostructure",
                table: "ProductRequirement",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PittingCorrosion",
                table: "ProductRequirement",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PmiInspection",
                table: "ProductRequirement",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PortColoring",
                table: "ProductRequirement",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RadiographicTest",
                table: "ProductRequirement",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SurfaceInspection",
                table: "ProductRequirement",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TensileHighTemp",
                table: "ProductRequirement",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TensileRoomTemp",
                table: "ProductRequirement",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "UltrasonicTest",
                table: "ProductRequirement",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "UnderwaterPressure",
                table: "ProductRequirement",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "WeldJointBend",
                table: "ProductRequirement",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "WeldJointImpact",
                table: "ProductRequirement",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "WeldJointTensile",
                table: "ProductRequirement",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BendTest",
                table: "ProductRequirement");

            migrationBuilder.DropColumn(
                name: "Dimension",
                table: "ProductRequirement");

            migrationBuilder.DropColumn(
                name: "EddyCurrent",
                table: "ProductRequirement");

            migrationBuilder.DropColumn(
                name: "Endoscopy",
                table: "ProductRequirement");

            migrationBuilder.DropColumn(
                name: "ExpandingTest",
                table: "ProductRequirement");

            migrationBuilder.DropColumn(
                name: "FerriteContent",
                table: "ProductRequirement");

            migrationBuilder.DropColumn(
                name: "FlaringTest",
                table: "ProductRequirement");

            migrationBuilder.DropColumn(
                name: "FlatteningTest",
                table: "ProductRequirement");

            migrationBuilder.DropColumn(
                name: "GrainSize",
                table: "ProductRequirement");

            migrationBuilder.DropColumn(
                name: "HardnessBrinell",
                table: "ProductRequirement");

            migrationBuilder.DropColumn(
                name: "HardnessRockwell",
                table: "ProductRequirement");

            migrationBuilder.DropColumn(
                name: "HardnessVickers",
                table: "ProductRequirement");

            migrationBuilder.DropColumn(
                name: "HydrostaticTest",
                table: "ProductRequirement");

            migrationBuilder.DropColumn(
                name: "ImpactTest",
                table: "ProductRequirement");

            migrationBuilder.DropColumn(
                name: "IntergranularCorrosion",
                table: "ProductRequirement");

            migrationBuilder.DropColumn(
                name: "Macrostructure",
                table: "ProductRequirement");

            migrationBuilder.DropColumn(
                name: "PittingCorrosion",
                table: "ProductRequirement");

            migrationBuilder.DropColumn(
                name: "PmiInspection",
                table: "ProductRequirement");

            migrationBuilder.DropColumn(
                name: "PortColoring",
                table: "ProductRequirement");

            migrationBuilder.DropColumn(
                name: "RadiographicTest",
                table: "ProductRequirement");

            migrationBuilder.DropColumn(
                name: "SurfaceInspection",
                table: "ProductRequirement");

            migrationBuilder.DropColumn(
                name: "TensileHighTemp",
                table: "ProductRequirement");

            migrationBuilder.DropColumn(
                name: "TensileRoomTemp",
                table: "ProductRequirement");

            migrationBuilder.DropColumn(
                name: "UltrasonicTest",
                table: "ProductRequirement");

            migrationBuilder.DropColumn(
                name: "UnderwaterPressure",
                table: "ProductRequirement");

            migrationBuilder.DropColumn(
                name: "WeldJointBend",
                table: "ProductRequirement");

            migrationBuilder.DropColumn(
                name: "WeldJointImpact",
                table: "ProductRequirement");

            migrationBuilder.DropColumn(
                name: "WeldJointTensile",
                table: "ProductRequirement");

            migrationBuilder.AlterColumn<string>(
                name: "ChemicalComposition",
                table: "ProductRequirement",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MechanicalProperty",
                table: "ProductRequirement",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NdtRequirement",
                table: "ProductRequirement",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SurfaceQuality",
                table: "ProductRequirement",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ToleranceRequirement",
                table: "ProductRequirement",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }
    }
}
