using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class FinalInspectionNdtFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CalibrationFrequency",
                table: "FinalInspection",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Couplant",
                table: "FinalInspection",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DetectionFrequency",
                table: "FinalInspection",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DetectionPhase",
                table: "FinalInspection",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DetectionSensitivity",
                table: "FinalInspection",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DetectionSpeed",
                table: "FinalInspection",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InspectionGrade",
                table: "FinalInspection",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InspectionStandard",
                table: "FinalInspection",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstrumentModel",
                table: "FinalInspection",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NdtMethod",
                table: "FinalInspection",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProbeType",
                table: "FinalInspection",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QualificationLevel",
                table: "FinalInspection",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StandardSampleDefect",
                table: "FinalInspection",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StandardSampleSize",
                table: "FinalInspection",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CalibrationFrequency",
                table: "FinalInspection");

            migrationBuilder.DropColumn(
                name: "Couplant",
                table: "FinalInspection");

            migrationBuilder.DropColumn(
                name: "DetectionFrequency",
                table: "FinalInspection");

            migrationBuilder.DropColumn(
                name: "DetectionPhase",
                table: "FinalInspection");

            migrationBuilder.DropColumn(
                name: "DetectionSensitivity",
                table: "FinalInspection");

            migrationBuilder.DropColumn(
                name: "DetectionSpeed",
                table: "FinalInspection");

            migrationBuilder.DropColumn(
                name: "InspectionGrade",
                table: "FinalInspection");

            migrationBuilder.DropColumn(
                name: "InspectionStandard",
                table: "FinalInspection");

            migrationBuilder.DropColumn(
                name: "InstrumentModel",
                table: "FinalInspection");

            migrationBuilder.DropColumn(
                name: "NdtMethod",
                table: "FinalInspection");

            migrationBuilder.DropColumn(
                name: "ProbeType",
                table: "FinalInspection");

            migrationBuilder.DropColumn(
                name: "QualificationLevel",
                table: "FinalInspection");

            migrationBuilder.DropColumn(
                name: "StandardSampleDefect",
                table: "FinalInspection");

            migrationBuilder.DropColumn(
                name: "StandardSampleSize",
                table: "FinalInspection");
        }
    }
}
