using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class NcrAddProductionMainNo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProductionMainNo",
                table: "Ncr",
                type: "nvarchar(max)",
                nullable: true);

            // 存量回填：从 ProductionBatch 按生产编号关联冗余主号
            migrationBuilder.Sql("""
                UPDATE n
                SET n.ProductionMainNo = pb.ProductionMainNo
                FROM Ncr n
                INNER JOIN ProductionBatch pb ON n.BatchNo = pb.BatchNo
                WHERE n.ProductionMainNo IS NULL AND pb.ProductionMainNo IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProductionMainNo",
                table: "Ncr");
        }
    }
}
