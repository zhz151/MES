using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDataSourceToAllModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DataSource",
                table: "SectionOutsource",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DataSource",
                table: "ProcessInspection",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DataSource",
                table: "OutsourceRecovery",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DataSource",
                table: "MaterialReceiveCheck",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DataSource",
                table: "FinalInspection",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataSource",
                table: "SectionOutsource");

            migrationBuilder.DropColumn(
                name: "DataSource",
                table: "ProcessInspection");

            migrationBuilder.DropColumn(
                name: "DataSource",
                table: "OutsourceRecovery");

            migrationBuilder.DropColumn(
                name: "DataSource",
                table: "MaterialReceiveCheck");

            migrationBuilder.DropColumn(
                name: "DataSource",
                table: "FinalInspection");
        }
    }
}
