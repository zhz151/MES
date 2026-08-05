using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class QualityProcessTrackingAddManufacturingStatusAndEndCustomer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EndCustomer",
                table: "QualityProcessTracking",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManufacturingStatus",
                table: "QualityProcessTracking",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            // 从 ProductionBatch 回填存量数据（制造状态 + 最终用户）
            migrationBuilder.Sql(
                @"UPDATE qpt SET qpt.ManufacturingStatus = pb.ManufacturingStatus, qpt.EndCustomer = pb.EndCustomer
                    FROM QualityProcessTracking qpt INNER JOIN ProductionBatch pb ON pb.Id = qpt.ProductionBatchId;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndCustomer",
                table: "QualityProcessTracking");

            migrationBuilder.DropColumn(
                name: "ManufacturingStatus",
                table: "QualityProcessTracking");
        }
    }
}
