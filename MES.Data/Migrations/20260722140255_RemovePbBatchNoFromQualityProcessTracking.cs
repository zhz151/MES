using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemovePbBatchNoFromQualityProcessTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PbBatchNo",
                table: "QualityProcessTracking");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PbBatchNo",
                table: "QualityProcessTracking",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }
    }
}
