using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class MaterialReceiveCheckAddIsDeliveryStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IsDeliveryStatus",
                table: "MaterialReceiveCheck",
                type: "nvarchar(max)",
                nullable: true);

            // 存量数据回填：批次制造状态==交货状态为"是"，否则"否"
            migrationBuilder.Sql(
                """
                UPDATE m
                SET IsDeliveryStatus = CASE WHEN pb.ManufacturingStatus = pb.DeliveryState THEN N'是' ELSE N'否' END
                FROM MaterialReceiveCheck m
                INNER JOIN ProductionBatch pb ON pb.Id = m.ProductionBatchId
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDeliveryStatus",
                table: "MaterialReceiveCheck");
        }
    }
}
