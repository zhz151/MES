using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedEnumDisplayInspectionRequirementStage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // R17：新增枚举 InspectionRequirementStage（成品检验项检验阶段 4 值）注册到「枚举显示配置」参数表，
            // 使编辑下拉/打印/筛选显示中文（终/预/预+终/-），并可参数化改名与排序；
            // 新增枚举不触发 DbInitializer 的 !Any() 种子（存量库已有数据），故此处补数据迁移。
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [EnumDisplayDefinitions] WHERE [EnumKey] = N'InspectionRequirementStage' AND [Value] = N'None')
                    INSERT INTO [EnumDisplayDefinitions] ([EnumKey], [Value], [DisplayName], [DisplayOrder], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'InspectionRequirementStage', N'None', N'-', 1, N'不要求', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [EnumDisplayDefinitions] WHERE [EnumKey] = N'InspectionRequirementStage' AND [Value] = N'FinalOnly')
                    INSERT INTO [EnumDisplayDefinitions] ([EnumKey], [Value], [DisplayName], [DisplayOrder], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'InspectionRequirementStage', N'FinalOnly', N'终', 2, N'仅正式成检', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [EnumDisplayDefinitions] WHERE [EnumKey] = N'InspectionRequirementStage' AND [Value] = N'PreOnly')
                    INSERT INTO [EnumDisplayDefinitions] ([EnumKey], [Value], [DisplayName], [DisplayOrder], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'InspectionRequirementStage', N'PreOnly', N'预', 3, N'仅预成检', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [EnumDisplayDefinitions] WHERE [EnumKey] = N'InspectionRequirementStage' AND [Value] = N'PreAndFinal')
                    INSERT INTO [EnumDisplayDefinitions] ([EnumKey], [Value], [DisplayName], [DisplayOrder], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'InspectionRequirementStage', N'PreAndFinal', N'预+终', 4, N'预成检与正式成检均需', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 撤销：删除本迁移写入的枚举显示配置（仅限 CreatedBy=System 且本枚举的行）
            migrationBuilder.Sql("""
                DELETE FROM [EnumDisplayDefinitions]
                WHERE [EnumKey] = N'InspectionRequirementStage' AND [CreatedBy] = N'System';
                """);
        }
    }
}
