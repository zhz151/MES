using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMaterialAndSupplierCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SupplierCode",
                table: "SupplierProfile",
                type: "nvarchar(6)",
                maxLength: 6,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MaterialCode",
                table: "Material",
                type: "nvarchar(6)",
                maxLength: 6,
                nullable: false,
                defaultValue: "");

            // 为现有行回填编码（按 Id 递增生成 MA0001 / SU0001...）
            // 使用子查询规避 SQL Server 限制：开窗函数不能直接用在 UPDATE SET 中
            migrationBuilder.Sql(@"
                UPDATE m
                SET m.MaterialCode = t.NewCode
                FROM Material m
                INNER JOIN (
                    SELECT Id, CONCAT('MA', RIGHT('0000' + CAST(ROW_NUMBER() OVER(ORDER BY Id) AS NVARCHAR(4)), 4)) AS NewCode
                    FROM Material
                ) t ON m.Id = t.Id
                WHERE m.MaterialCode = '';
            ");
            migrationBuilder.Sql(@"
                UPDATE s
                SET s.SupplierCode = t.NewCode
                FROM SupplierProfile s
                INNER JOIN (
                    SELECT Id, CONCAT('SU', RIGHT('0000' + CAST(ROW_NUMBER() OVER(ORDER BY Id) AS NVARCHAR(4)), 4)) AS NewCode
                    FROM SupplierProfile
                ) t ON s.Id = t.Id
                WHERE s.SupplierCode = '';
            ");

            migrationBuilder.CreateIndex(
                name: "UK_Supplier_Code",
                table: "SupplierProfile",
                column: "SupplierCode",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_Material_Code",
                table: "Material",
                column: "MaterialCode",
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UK_Supplier_Code",
                table: "SupplierProfile");

            migrationBuilder.DropIndex(
                name: "UK_Material_Code",
                table: "Material");

            migrationBuilder.DropColumn(
                name: "SupplierCode",
                table: "SupplierProfile");

            migrationBuilder.DropColumn(
                name: "MaterialCode",
                table: "Material");
        }
    }
}
