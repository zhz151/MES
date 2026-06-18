using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameSectionFlowCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 完全重建 SectionFlowAnalysis 类别体系
            migrationBuilder.Sql(@"
DELETE FROM [SectionFlowCategoryItems];
DELETE FROM [SectionFlowCategorySettings];

DECLARE @A_ID int, @B_ID int, @C_ID int, @D_ID int, @E_ID int, @F_ID int, @G_ID int,
        @H_ID int, @I_ID int, @J_ID int, @K_ID int, @L_ID int, @M_ID int, @N_ID int;

INSERT INTO [SectionFlowCategorySettings] ([CategoryCode],[CategoryName],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES ('A','外抛光','migration',GETDATE(),'migration',GETDATE()); SET @A_ID = SCOPE_IDENTITY();
INSERT INTO [SectionFlowCategorySettings] ([CategoryCode],[CategoryName],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES ('B','内修磨','migration',GETDATE(),'migration',GETDATE()); SET @B_ID = SCOPE_IDENTITY();
INSERT INTO [SectionFlowCategorySettings] ([CategoryCode],[CategoryName],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES ('C','外点磨','migration',GETDATE(),'migration',GETDATE()); SET @C_ID = SCOPE_IDENTITY();
INSERT INTO [SectionFlowCategorySettings] ([CategoryCode],[CategoryName],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES ('D','荒管检','migration',GETDATE(),'migration',GETDATE()); SET @D_ID = SCOPE_IDENTITY();
INSERT INTO [SectionFlowCategorySettings] ([CategoryCode],[CategoryName],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES ('E','在制检','migration',GETDATE(),'migration',GETDATE()); SET @E_ID = SCOPE_IDENTITY();
INSERT INTO [SectionFlowCategorySettings] ([CategoryCode],[CategoryName],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES ('F','固溶','migration',GETDATE(),'migration',GETDATE()); SET @F_ID = SCOPE_IDENTITY();
INSERT INTO [SectionFlowCategorySettings] ([CategoryCode],[CategoryName],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES ('G','矫直','migration',GETDATE(),'migration',GETDATE()); SET @G_ID = SCOPE_IDENTITY();
INSERT INTO [SectionFlowCategorySettings] ([CategoryCode],[CategoryName],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES ('H','切割','migration',GETDATE(),'migration',GETDATE()); SET @H_ID = SCOPE_IDENTITY();
INSERT INTO [SectionFlowCategorySettings] ([CategoryCode],[CategoryName],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES ('I','去油','migration',GETDATE(),'migration',GETDATE()); SET @I_ID = SCOPE_IDENTITY();
INSERT INTO [SectionFlowCategorySettings] ([CategoryCode],[CategoryName],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES ('J','酸洗','migration',GETDATE(),'migration',GETDATE()); SET @J_ID = SCOPE_IDENTITY();
INSERT INTO [SectionFlowCategorySettings] ([CategoryCode],[CategoryName],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES ('K','大轧','migration',GETDATE(),'migration',GETDATE()); SET @K_ID = SCOPE_IDENTITY();
INSERT INTO [SectionFlowCategorySettings] ([CategoryCode],[CategoryName],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES ('L','小轧','migration',GETDATE(),'migration',GETDATE()); SET @L_ID = SCOPE_IDENTITY();
INSERT INTO [SectionFlowCategorySettings] ([CategoryCode],[CategoryName],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES ('M','冷拔','migration',GETDATE(),'migration',GETDATE()); SET @M_ID = SCOPE_IDENTITY();
INSERT INTO [SectionFlowCategorySettings] ([CategoryCode],[CategoryName],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES ('N','成品待检','migration',GETDATE(),'migration',GETDATE()); SET @N_ID = SCOPE_IDENTITY();

-- A 外抛光
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@A_ID,'荒管处理','外抛光',1,1,'migration',GETDATE(),'migration',GETDATE());
-- B 内修磨
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@B_ID,'荒管处理','内修磨',1,1,'migration',GETDATE(),'migration',GETDATE());
-- C 外点磨
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@C_ID,'荒管处理','外点磨',1,1,'migration',GETDATE(),'migration',GETDATE());
-- D 荒管检
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@D_ID,'荒管处理','检验',1,1,'migration',GETDATE(),'migration',GETDATE());
-- E 在制检
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@E_ID,'全部','检验',1,1,'migration',GETDATE(),'migration',GETDATE());
-- F 固溶
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@F_ID,'20冷轧','固溶',1,1,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@F_ID,'30冷轧','固溶',1,2,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@F_ID,'50冷轧','固溶',1,3,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@F_ID,'60冷轧','固溶',1,4,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@F_ID,'冷拔','固溶',1,5,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@F_ID,'三辊冷轧','固溶',1,6,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@F_ID,'在制修检','固溶',1,7,'migration',GETDATE(),'migration',GETDATE());

-- G 矫直
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@G_ID,'20冷轧','矫直',1,1,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@G_ID,'30冷轧','矫直',1,2,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@G_ID,'50冷轧','矫直',0.5,3,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@G_ID,'60冷轧','矫直',0.5,4,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@G_ID,'荒管处理','矫直',0.25,5,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@G_ID,'冷拔','矫直',1,6,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@G_ID,'三辊冷轧','矫直',1,7,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@G_ID,'在制修检','矫直',1,8,'migration',GETDATE(),'migration',GETDATE());

-- H 切割
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@H_ID,'20冷轧','断切',1,1,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@H_ID,'30冷轧','断切',1,2,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@H_ID,'50冷轧','断切',0.5,3,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@H_ID,'60冷轧','断切',0.5,4,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@H_ID,'荒管处理','断切',0.25,5,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@H_ID,'冷拔','断切',1,6,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@H_ID,'三辊冷轧','断切',1,7,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@H_ID,'在制修检','断切',0.25,8,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@H_ID,'20冷轧','油管断',0.75,9,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@H_ID,'30冷轧','油管断',0.75,10,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@H_ID,'50冷轧','油管断',0.5,11,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@H_ID,'60冷轧','油管断',0.5,12,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@H_ID,'三辊冷轧','油管断',0.75,13,'migration',GETDATE(),'migration',GETDATE());

-- I 去油
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@I_ID,'20冷轧','去油',1,1,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@I_ID,'30冷轧','去油',1,2,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@I_ID,'50冷轧','去油',0.5,3,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@I_ID,'60冷轧','去油',0.5,4,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@I_ID,'三辊冷轧','去油',1,5,'migration',GETDATE(),'migration',GETDATE());

-- J 酸洗
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@J_ID,'20冷轧','酸洗',1,1,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@J_ID,'30冷轧','酸洗',1,2,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@J_ID,'50冷轧','酸洗',0.5,3,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@J_ID,'60冷轧','酸洗',0.5,4,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@J_ID,'荒管处理','酸洗',0.25,5,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@J_ID,'冷拔','酸洗',1,6,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@J_ID,'三辊冷轧','酸洗',1,7,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@J_ID,'在制修检','酸洗',0.25,8,'migration',GETDATE(),'migration',GETDATE());

-- K 大轧
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@K_ID,'50冷轧','冷轧拔',1,1,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@K_ID,'60冷轧','冷轧拔',1,2,'migration',GETDATE(),'migration',GETDATE());

-- L 小轧
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@L_ID,'20冷轧','冷轧拔',1,1,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@L_ID,'30冷轧','冷轧拔',1,2,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@L_ID,'三辊冷轧','冷轧拔',1,3,'migration',GETDATE(),'migration',GETDATE());

-- M 冷拔
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@M_ID,'冷拔','冷轧拔',1,1,'migration',GETDATE(),'migration',GETDATE());

-- N 成品待检
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@N_ID,'全部','检验',1,1,'migration',GETDATE(),'migration',GETDATE());
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM [SectionFlowCategoryItems];
DELETE FROM [SectionFlowCategorySettings];

DECLARE @A_ID int, @B_ID int, @C_ID int, @D_ID int, @E_ID int, @F_ID int, @G_ID int,
        @H_ID int, @J_ID int, @K_ID int, @L_ID int, @M_ID int;

INSERT INTO [SectionFlowCategorySettings] ([CategoryCode],[CategoryName],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES ('A','荒管处理','migration',GETDATE(),'migration',GETDATE()); SET @A_ID = SCOPE_IDENTITY();
INSERT INTO [SectionFlowCategorySettings] ([CategoryCode],[CategoryName],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES ('B','固溶','migration',GETDATE(),'migration',GETDATE()); SET @B_ID = SCOPE_IDENTITY();
INSERT INTO [SectionFlowCategorySettings] ([CategoryCode],[CategoryName],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES ('C','矫直','migration',GETDATE(),'migration',GETDATE()); SET @C_ID = SCOPE_IDENTITY();
INSERT INTO [SectionFlowCategorySettings] ([CategoryCode],[CategoryName],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES ('D','切割','migration',GETDATE(),'migration',GETDATE()); SET @D_ID = SCOPE_IDENTITY();
INSERT INTO [SectionFlowCategorySettings] ([CategoryCode],[CategoryName],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES ('E','去油','migration',GETDATE(),'migration',GETDATE()); SET @E_ID = SCOPE_IDENTITY();
INSERT INTO [SectionFlowCategorySettings] ([CategoryCode],[CategoryName],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES ('F','酸洗','migration',GETDATE(),'migration',GETDATE()); SET @F_ID = SCOPE_IDENTITY();
INSERT INTO [SectionFlowCategorySettings] ([CategoryCode],[CategoryName],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES ('G','大轧','migration',GETDATE(),'migration',GETDATE()); SET @G_ID = SCOPE_IDENTITY();
INSERT INTO [SectionFlowCategorySettings] ([CategoryCode],[CategoryName],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES ('H','小轧','migration',GETDATE(),'migration',GETDATE()); SET @H_ID = SCOPE_IDENTITY();
INSERT INTO [SectionFlowCategorySettings] ([CategoryCode],[CategoryName],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES ('J','冷拔','migration',GETDATE(),'migration',GETDATE()); SET @J_ID = SCOPE_IDENTITY();
INSERT INTO [SectionFlowCategorySettings] ([CategoryCode],[CategoryName],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES ('K','荒管检','migration',GETDATE(),'migration',GETDATE()); SET @K_ID = SCOPE_IDENTITY();
INSERT INTO [SectionFlowCategorySettings] ([CategoryCode],[CategoryName],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES ('L','在制检','migration',GETDATE(),'migration',GETDATE()); SET @L_ID = SCOPE_IDENTITY();
INSERT INTO [SectionFlowCategorySettings] ([CategoryCode],[CategoryName],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES ('M','成品待检','migration',GETDATE(),'migration',GETDATE()); SET @M_ID = SCOPE_IDENTITY();

-- A 荒管处理
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@A_ID,'荒管处理','外点磨',1,1,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@A_ID,'荒管处理','外抛光',1,2,'migration',GETDATE(),'migration',GETDATE());
-- B 固溶
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@B_ID,'20冷轧','固溶',1,1,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@B_ID,'30冷轧','固溶',1,2,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@B_ID,'50冷轧','固溶',1,3,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@B_ID,'60冷轧','固溶',1,4,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@B_ID,'冷拔','固溶',1,5,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@B_ID,'三辊冷轧','固溶',1,6,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@B_ID,'在制修检','固溶',1,7,'migration',GETDATE(),'migration',GETDATE());
-- C 矫直
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@C_ID,'20冷轧','矫直',1,1,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@C_ID,'30冷轧','矫直',1,2,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@C_ID,'50冷轧','矫直',0.5,3,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@C_ID,'60冷轧','矫直',0.5,4,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@C_ID,'荒管处理','矫直',0.25,5,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@C_ID,'冷拔','矫直',1,6,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@C_ID,'三辊冷轧','矫直',1,7,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@C_ID,'在制修检','矫直',1,8,'migration',GETDATE(),'migration',GETDATE());
-- D 切割
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@D_ID,'20冷轧','断切',1,1,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@D_ID,'30冷轧','断切',1,2,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@D_ID,'50冷轧','断切',0.5,3,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@D_ID,'60冷轧','断切',0.5,4,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@D_ID,'荒管处理','断切',0.25,5,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@D_ID,'冷拔','断切',1,6,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@D_ID,'三辊冷轧','断切',1,7,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@D_ID,'在制修检','断切',0.25,8,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@D_ID,'20冷轧','油管断',0.75,9,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@D_ID,'30冷轧','油管断',0.75,10,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@D_ID,'50冷轧','油管断',0.5,11,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@D_ID,'60冷轧','油管断',0.5,12,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@D_ID,'三辊冷轧','油管断',0.75,13,'migration',GETDATE(),'migration',GETDATE());
-- E 去油
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@E_ID,'20冷轧','去油',1,1,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@E_ID,'30冷轧','去油',1,2,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@E_ID,'50冷轧','去油',0.5,3,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@E_ID,'60冷轧','去油',0.5,4,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@E_ID,'三辊冷轧','去油',1,5,'migration',GETDATE(),'migration',GETDATE());
-- F 酸洗
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@F_ID,'20冷轧','酸洗',1,1,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@F_ID,'30冷轧','酸洗',1,2,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@F_ID,'50冷轧','酸洗',0.5,3,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@F_ID,'60冷轧','酸洗',0.5,4,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@F_ID,'荒管处理','酸洗',0.25,5,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@F_ID,'冷拔','酸洗',1,6,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@F_ID,'三辊冷轧','酸洗',1,7,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@F_ID,'在制修检','酸洗',0.25,8,'migration',GETDATE(),'migration',GETDATE());
-- G 大轧
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@G_ID,'50冷轧','冷轧拔',1,1,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@G_ID,'60冷轧','冷轧拔',1,2,'migration',GETDATE(),'migration',GETDATE());
-- H 小轧
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@H_ID,'20冷轧','冷轧拔',1,1,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@H_ID,'30冷轧','冷轧拔',1,2,'migration',GETDATE(),'migration',GETDATE());
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@H_ID,'三辊冷轧','冷轧拔',1,3,'migration',GETDATE(),'migration',GETDATE());
-- J 冷拔
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@J_ID,'冷拔','冷轧拔',1,1,'migration',GETDATE(),'migration',GETDATE());
-- K 荒管检
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@K_ID,'荒管处理','检验',1,1,'migration',GETDATE(),'migration',GETDATE());
-- L 在制检
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@L_ID,'全部','检验',1,1,'migration',GETDATE(),'migration',GETDATE());
-- M 成品待检
INSERT INTO [SectionFlowCategoryItems] ([SettingId],[ProcessGroupName],[SectionName],[Coefficient],[DisplayOrder],[CreatedBy],[CreatedTime],[UpdatedBy],[UpdatedTime]) VALUES (@M_ID,'全部','检验',1,1,'migration',GETDATE(),'migration',GETDATE());
");
        }
    }
}
