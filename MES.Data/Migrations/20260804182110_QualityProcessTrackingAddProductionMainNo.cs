using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class QualityProcessTrackingAddProductionMainNo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProductionMainNo",
                table: "QualityProcessTracking",
                type: "nvarchar(max)",
                nullable: true);

            // 存量回填：从 ProductionBatch 关联冗余主号
            migrationBuilder.Sql("""
                UPDATE qt
                SET qt.ProductionMainNo = pb.ProductionMainNo
                FROM QualityProcessTracking qt
                INNER JOIN ProductionBatch pb ON qt.ProductionBatchId = pb.Id
                WHERE qt.ProductionMainNo IS NULL AND pb.ProductionMainNo IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProductionMainNo",
                table: "QualityProcessTracking");
        }
    }
}
