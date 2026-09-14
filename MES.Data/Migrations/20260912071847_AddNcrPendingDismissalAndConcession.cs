using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNcrPendingDismissalAndConcession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConcessionQuantity",
                table: "Ncr",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConcessionRemark",
                table: "Ncr",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConcessionWeight",
                table: "Ncr",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceGroupKey",
                table: "Ncr",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NcrPendingDismissal",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ProductionBatchId = table.Column<int>(type: "int", nullable: false),
                    InspectionType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: ""),
                    InspectionItem = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: ""),
                    ProcessName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: ""),
                    DismissedQuantity = table.Column<int>(type: "int", nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
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

            // ===== 阈值参数改造（2026-09-12）=====
            // 旧口径按流向分设 8 项（ReworkCount/ReworkPercent/WarehouseCount/WarehousePercent/ScrapCount/ScrapPercent/ReturnCount/ReturnPercent）
            // 新口径统一为「组内不合格合计（让步放行 + 各流向）」两项：Count（绝对支数）/ Percent（占比），均严格大于。
            migrationBuilder.Sql(@"
DELETE FROM [ConfigParameters] WHERE [Category] = N'NcrThreshold' AND [ParamKey] NOT IN (N'Count', N'Percent');
IF NOT EXISTS (SELECT 1 FROM [ConfigParameters] WHERE [Category] = N'NcrThreshold' AND [ParamKey] = N'Count')
    INSERT INTO [ConfigParameters] ([Category], [CategoryDisplay], [Context], [ParamKey], [ParamValue], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
    VALUES (N'NcrThreshold', N'质量-NCR触发阈值', N'质量', N'Count', 5, N'组内不合格合计触发绝对支数（严格大于）', SYSDATETIMEOFFSET(), N'system', SYSDATETIMEOFFSET(), N'system');
IF NOT EXISTS (SELECT 1 FROM [ConfigParameters] WHERE [Category] = N'NcrThreshold' AND [ParamKey] = N'Percent')
    INSERT INTO [ConfigParameters] ([Category], [CategoryDisplay], [Context], [ParamKey], [ParamValue], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
    VALUES (N'NcrThreshold', N'质量-NCR触发阈值', N'质量', N'Percent', 0.10, N'组内不合格合计触发占比（严格大于，0.1=10%）', SYSDATETIMEOFFSET(), N'system', SYSDATETIMEOFFSET(), N'system');
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NcrPendingDismissal");

            migrationBuilder.DropColumn(
                name: "ConcessionQuantity",
                table: "Ncr");

            migrationBuilder.DropColumn(
                name: "ConcessionRemark",
                table: "Ncr");

            migrationBuilder.DropColumn(
                name: "ConcessionWeight",
                table: "Ncr");

            migrationBuilder.DropColumn(
                name: "SourceGroupKey",
                table: "Ncr");
        }
    }
}
