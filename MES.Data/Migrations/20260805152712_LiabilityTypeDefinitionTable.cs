using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class LiabilityTypeDefinitionTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 责任类型 LiabilityType 中文化 → 英文稳定 Key（枚举化，配配置表 LiabilityTypeDefinitions）
            // 存量值域已验证：厂部→FactoryDepartment / 外购→OutsourcedPurchase / bb（脏值）→NULL
            // 未知值（非内置）一律置 NULL（仅厂部/外购为合法内置值）
            var caseSql = "CASE LiabilityType WHEN N'厂部' THEN N'FactoryDepartment' WHEN N'外购' THEN N'OutsourcedPurchase' ELSE NULL END";
            migrationBuilder.Sql($"UPDATE [InventoryBatch] SET LiabilityType = {caseSql} WHERE LiabilityType IS NOT NULL AND LiabilityType != ''");

            migrationBuilder.CreateTable(
                name: "LiabilityTypeDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LiabilityKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LiabilityName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiabilityTypeDefinitions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UK_LTD_LiabilityKey",
                table: "LiabilityTypeDefinitions",
                column: "LiabilityKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 英文 Key → 中文（回滚）；NULL 保持 NULL（脏值已丢失不可恢复）
            var caseSql = "CASE LiabilityType WHEN N'FactoryDepartment' THEN N'厂部' WHEN N'OutsourcedPurchase' THEN N'外购' ELSE LiabilityType END";
            migrationBuilder.Sql($"UPDATE [InventoryBatch] SET LiabilityType = {caseSql} WHERE LiabilityType IS NOT NULL");

            migrationBuilder.DropTable(
                name: "LiabilityTypeDefinitions");
        }
    }
}
