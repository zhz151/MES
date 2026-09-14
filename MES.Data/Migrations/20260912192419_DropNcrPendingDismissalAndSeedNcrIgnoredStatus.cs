using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropNcrPendingDismissalAndSeedNcrIgnoredStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NcrPendingDismissal");

            // 「忽略」不再走独立台账，改为 NcrStatus 新增一档（登记即忽略）。
            // 新增枚举值不触发 DbInitializer 的 !Any() 种子（存量库已有数据），故此处补数据迁移。
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [EnumDisplayDefinitions] WHERE [EnumKey] = N'NcrStatus' AND [Value] = N'Ignored')
                    INSERT INTO [EnumDisplayDefinitions] ([EnumKey], [Value], [DisplayName], [DisplayOrder], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'NcrStatus', N'Ignored', N'忽略', 4, NULL, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM [EnumDisplayDefinitions]
                WHERE [EnumKey] = N'NcrStatus' AND [Value] = N'Ignored' AND [CreatedBy] = N'System';
                """);

            migrationBuilder.CreateTable(
                name: "NcrPendingDismissal",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DismissedQuantity = table.Column<int>(type: "int", nullable: false),
                    InspectionItem = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: ""),
                    InspectionType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: ""),
                    ProcessName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: ""),
                    ProductionBatchId = table.Column<int>(type: "int", nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SourceType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NcrPendingDismissal", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UK_NcrPendingDismissal_Group",
                table: "NcrPendingDismissal",
                columns: new[] { "SourceType", "ProductionBatchId", "InspectionType", "InspectionItem", "ProcessName" },
                unique: true);
        }
    }
}
