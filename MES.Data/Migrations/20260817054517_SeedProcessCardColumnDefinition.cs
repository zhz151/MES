using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedProcessCardColumnDefinition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 工艺卡打印列布局配置：为 5 个区块（BatchInfo/Quality/Warehouse/WorkOrder/ProcessGroup）种子默认列定义，
            // 默认值 = 用户定稿的列宽权重方案（RowIndex 所属行 / ColumnIndex 区块内全局排序键 / ColumnWeight 列宽权重）。
            // 新库走 DbInitializer 种子，存量库（已存在任何配置行或表已有数据）不触发，故此处补数据迁移（幂等 IF NOT EXISTS）。

            // === 批次基本信息（20）===
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'BatchInfo' AND [FieldKey] = N'BatchNo')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'BatchInfo', N'BatchNo', N'生产编号', 1, 1, 1, 4, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'BatchInfo' AND [FieldKey] = N'TagNo')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'BatchInfo', N'TagNo', N'挂牌号', 1, 1, 2, 3, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'BatchInfo' AND [FieldKey] = N'ProductionType')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'BatchInfo', N'ProductionType', N'生产类型', 1, 1, 3, 4, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'BatchInfo' AND [FieldKey] = N'ManufacturingItem')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'BatchInfo', N'ManufacturingItem', N'制造物品', 1, 1, 4, 4, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'BatchInfo' AND [FieldKey] = N'ManufacturingStatus')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'BatchInfo', N'ManufacturingStatus', N'制造状态', 1, 1, 5, 4, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'BatchInfo' AND [FieldKey] = N'ProductionRatio')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'BatchInfo', N'ProductionRatio', N'制成倍数', 1, 1, 6, 3, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'BatchInfo' AND [FieldKey] = N'Remark')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'BatchInfo', N'Remark', N'备注', 1, 1, 7, 8, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'BatchInfo' AND [FieldKey] = N'CreatedBy')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'BatchInfo', N'CreatedBy', N'创建人', 1, 1, 8, 4, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'BatchInfo' AND [FieldKey] = N'CreatedTime')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'BatchInfo', N'CreatedTime', N'创建时间', 1, 1, 9, 4, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'BatchInfo' AND [FieldKey] = N'Status')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'BatchInfo', N'Status', N'状态', 0, 2, 10, 3, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'BatchInfo' AND [FieldKey] = N'CurrentExecDate')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'BatchInfo', N'CurrentExecDate', N'截止执行日', 0, 2, 11, 4, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'BatchInfo' AND [FieldKey] = N'CurrentGroupName')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'BatchInfo', N'CurrentGroupName', N'当前工序', 0, 2, 12, 4, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'BatchInfo' AND [FieldKey] = N'CurrentSectionName')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'BatchInfo', N'CurrentSectionName', N'当前工段', 0, 2, 13, 4, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'BatchInfo' AND [FieldKey] = N'CurrentEquipmentName')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'BatchInfo', N'CurrentEquipmentName', N'当前设备', 0, 2, 14, 4, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'BatchInfo' AND [FieldKey] = N'CurrentOutsource')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'BatchInfo', N'CurrentOutsource', N'当前委外', 0, 2, 15, 4, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'BatchInfo' AND [FieldKey] = N'CurrentSpec')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'BatchInfo', N'CurrentSpec', N'当前规格', 0, 2, 16, 4, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'BatchInfo' AND [FieldKey] = N'NextProcess')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'BatchInfo', N'NextProcess', N'下一工序', 0, 2, 17, 4, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'BatchInfo' AND [FieldKey] = N'NextSectionName')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'BatchInfo', N'NextSectionName', N'下一工段', 0, 2, 18, 4, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'BatchInfo' AND [FieldKey] = N'CorrespondingSpec')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'BatchInfo', N'CorrespondingSpec', N'对应规格', 0, 2, 19, 4, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'BatchInfo' AND [FieldKey] = N'IsForceCompleted')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'BatchInfo', N'IsForceCompleted', N'强制完成', 0, 2, 20, 3, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                """);

            // === 质量要求（2）===
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'Quality' AND [FieldKey] = N'SolutionParams')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Quality', N'SolutionParams', N'固溶参数', 1, 1, 1, 2, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'Quality' AND [FieldKey] = N'QualityRemark')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Quality', N'QualityRemark', N'质量备注', 1, 1, 2, 10, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                """);

            // === 投料信息（15）===
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'Warehouse' AND [FieldKey] = N'SourceBatchNo')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Warehouse', N'SourceBatchNo', N'仓库批', 1, 1, 1, 3, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'Warehouse' AND [FieldKey] = N'SourceMaterialType')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Warehouse', N'SourceMaterialType', N'原料类型', 1, 1, 2, 3, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'Warehouse' AND [FieldKey] = N'SourceName')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Warehouse', N'SourceName', N'来料单位', 1, 1, 3, 6, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'Warehouse' AND [FieldKey] = N'SourceHeatNo')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Warehouse', N'SourceHeatNo', N'炉号', 1, 1, 4, 5, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'Warehouse' AND [FieldKey] = N'SourcePlantGrade')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Warehouse', N'SourcePlantGrade', N'工厂牌号', 1, 1, 5, 4, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'Warehouse' AND [FieldKey] = N'SourceSpecification')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Warehouse', N'SourceSpecification', N'名义规格', 1, 1, 6, 4, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'Warehouse' AND [FieldKey] = N'SourceLengthStatus')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Warehouse', N'SourceLengthStatus', N'长度状态', 1, 1, 7, 4, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'Warehouse' AND [FieldKey] = N'SourceUnitWeight')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Warehouse', N'SourceUnitWeight', N'单支重(kg)', 1, 1, 8, 3, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'Warehouse' AND [FieldKey] = N'InputQuantity')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Warehouse', N'InputQuantity', N'领料支数', 1, 1, 9, 3, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'Warehouse' AND [FieldKey] = N'InputWeight')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Warehouse', N'InputWeight', N'领料重量(kg)', 1, 1, 10, 3, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'Warehouse' AND [FieldKey] = N'InputType')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Warehouse', N'InputType', N'投料类型', 1, 1, 11, 3, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'Warehouse' AND [FieldKey] = N'SourceProductionNo')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Warehouse', N'SourceProductionNo', N'源生产编号', 1, 1, 12, 4, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'Warehouse' AND [FieldKey] = N'SourceRemark')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Warehouse', N'SourceRemark', N'原料备注', 1, 1, 13, 6, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'Warehouse' AND [FieldKey] = N'CurrentValidQty')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Warehouse', N'CurrentValidQty', N'有效原料支数', 1, 1, 14, 3, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'Warehouse' AND [FieldKey] = N'CurrentValidWeight')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Warehouse', N'CurrentValidWeight', N'有效原料重量(kg)', 1, 1, 15, 3, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                """);

            // === 工单信息（27）===
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'WorkOrder' AND [FieldKey] = N'WorkOrderNo')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'WorkOrder', N'WorkOrderNo', N'工单号', 1, 1, 1, 5, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'WorkOrder' AND [FieldKey] = N'SalesOrderNo')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'WorkOrder', N'SalesOrderNo', N'源订单号', 1, 1, 2, 4, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'WorkOrder' AND [FieldKey] = N'ProductionMainNo')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'WorkOrder', N'ProductionMainNo', N'主号', 1, 1, 3, 2, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'WorkOrder' AND [FieldKey] = N'ProductionSubNo')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'WorkOrder', N'ProductionSubNo', N'次号', 1, 1, 4, 2, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'WorkOrder' AND [FieldKey] = N'OrderItemIds')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'WorkOrder', N'OrderItemIds', N'项次ID', 1, 1, 5, 4, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'WorkOrder' AND [FieldKey] = N'SignDate')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'WorkOrder', N'SignDate', N'签订日期', 1, 1, 6, 4, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'WorkOrder' AND [FieldKey] = N'Salesman')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'WorkOrder', N'Salesman', N'业务员', 1, 1, 7, 3, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'WorkOrder' AND [FieldKey] = N'EndCustomer')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'WorkOrder', N'EndCustomer', N'最终用户', 1, 1, 8, 6, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'WorkOrder' AND [FieldKey] = N'DeliveryDate')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'WorkOrder', N'DeliveryDate', N'交货日期', 1, 1, 9, 4, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'WorkOrder' AND [FieldKey] = N'DelayPenalty')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'WorkOrder', N'DelayPenalty', N'延期罚款', 1, 1, 10, 2, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'WorkOrder' AND [FieldKey] = N'MaterialName')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'WorkOrder', N'MaterialName', N'钢管制造', 1, 1, 11, 4, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'WorkOrder' AND [FieldKey] = N'SettlementMethod')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'WorkOrder', N'SettlementMethod', N'结算方式', 1, 1, 12, 3, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'WorkOrder' AND [FieldKey] = N'StandardCode')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'WorkOrder', N'StandardCode', N'标准编码', 1, 1, 13, 5, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'WorkOrder' AND [FieldKey] = N'TechnicalRequirements')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'WorkOrder', N'TechnicalRequirements', N'技术要求', 1, 1, 14, 4, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'WorkOrder' AND [FieldKey] = N'PlantGrade')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'WorkOrder', N'PlantGrade', N'工厂牌号', 1, 2, 15, 4, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'WorkOrder' AND [FieldKey] = N'Specification')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'WorkOrder', N'Specification', N'规格', 1, 2, 16, 6, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'WorkOrder' AND [FieldKey] = N'OuterDiameterTolerance')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'WorkOrder', N'OuterDiameterTolerance', N'外径公差', 1, 2, 17, 4, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'WorkOrder' AND [FieldKey] = N'WallThicknessTolerance')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'WorkOrder', N'WallThicknessTolerance', N'壁厚公差', 1, 2, 18, 4, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'WorkOrder' AND [FieldKey] = N'LengthStatus')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'WorkOrder', N'LengthStatus', N'长度状态', 1, 2, 19, 4, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'WorkOrder' AND [FieldKey] = N'MinLength')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'WorkOrder', N'MinLength', N'最小长度(mm)', 1, 2, 20, 6, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'WorkOrder' AND [FieldKey] = N'MaxLength')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'WorkOrder', N'MaxLength', N'最大长度(mm)', 1, 2, 21, 6, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'WorkOrder' AND [FieldKey] = N'TotalQuantity')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'WorkOrder', N'TotalQuantity', N'总支数', 1, 2, 22, 4, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'WorkOrder' AND [FieldKey] = N'TotalMeters')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'WorkOrder', N'TotalMeters', N'总米数(m)', 1, 2, 23, 4, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'WorkOrder' AND [FieldKey] = N'TotalWeight')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'WorkOrder', N'TotalWeight', N'总重量(kg)', 1, 2, 24, 4, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'WorkOrder' AND [FieldKey] = N'DeliveryState')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'WorkOrder', N'DeliveryState', N'交货状态', 1, 2, 25, 6, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'WorkOrder' AND [FieldKey] = N'TotalItemCount')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'WorkOrder', N'TotalItemCount', N'总项次数', 1, 3, 26, 1, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'WorkOrder' AND [FieldKey] = N'ItemDetails')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'WorkOrder', N'ItemDetails', N'明细', 1, 3, 27, 10, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                """);

            // === 工序组（33 = 7 固定 + 26 工段，全行 1 全 Visible，工段窄列权重 1）===
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'ProcessGroup' AND [FieldKey] = N'ProcessName')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'ProcessGroup', N'ProcessName', N'工序名称', 1, 1, 1, 4, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'ProcessGroup' AND [FieldKey] = N'ManufacturingSpec')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'ProcessGroup', N'ManufacturingSpec', N'制造规格', 1, 1, 2, 4, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'ProcessGroup' AND [FieldKey] = N'OuterDiameterTolerance')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'ProcessGroup', N'OuterDiameterTolerance', N'外径公差', 1, 1, 3, 3, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'ProcessGroup' AND [FieldKey] = N'WallThicknessTolerance')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'ProcessGroup', N'WallThicknessTolerance', N'壁厚公差', 1, 1, 4, 3, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'ProcessGroup' AND [FieldKey] = N'ManufacturingLength')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'ProcessGroup', N'ManufacturingLength', N'制造长度', 1, 1, 5, 3, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'ProcessGroup' AND [FieldKey] = N'CuttingTreatment')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'ProcessGroup', N'CuttingTreatment', N'断切处理', 1, 1, 6, 3, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'ProcessGroup' AND [FieldKey] = N'Remark')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'ProcessGroup', N'Remark', N'备注', 1, 1, 7, 4, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                """);

            // 工序组 26 工段（窄列权重 1，ColumnIndex 8-33）
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'ProcessGroup' AND [FieldKey] = N'ColdRollDraw')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'ProcessGroup', N'ColdRollDraw', N'冷轧拔', 1, 1, 8, 1, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'ProcessGroup' AND [FieldKey] = N'OilPipeCut')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'ProcessGroup', N'OilPipeCut', N'油管断', 1, 1, 9, 1, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'ProcessGroup' AND [FieldKey] = N'Degrease')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'ProcessGroup', N'Degrease', N'去油', 1, 1, 10, 1, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'ProcessGroup' AND [FieldKey] = N'EmulsionWash')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'ProcessGroup', N'EmulsionWash', N'乳液浸洗', 1, 1, 11, 1, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'ProcessGroup' AND [FieldKey] = N'UltrasonicWash')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'ProcessGroup', N'UltrasonicWash', N'超声浸洗', 1, 1, 12, 1, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'ProcessGroup' AND [FieldKey] = N'ClothPolish')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'ProcessGroup', N'ClothPolish', N'打布', 1, 1, 13, 1, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'ProcessGroup' AND [FieldKey] = N'BrightAnnealing')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'ProcessGroup', N'BrightAnnealing', N'光亮退火', 1, 1, 14, 1, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'ProcessGroup' AND [FieldKey] = N'Solution')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'ProcessGroup', N'Solution', N'固溶', 1, 1, 15, 1, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'ProcessGroup' AND [FieldKey] = N'Straighten')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'ProcessGroup', N'Straighten', N'矫直', 1, 1, 16, 1, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'ProcessGroup' AND [FieldKey] = N'Cut')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'ProcessGroup', N'Cut', N'断切', 1, 1, 17, 1, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'ProcessGroup' AND [FieldKey] = N'ThicknessMeasure')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'ProcessGroup', N'ThicknessMeasure', N'测壁厚', 1, 1, 18, 1, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'ProcessGroup' AND [FieldKey] = N'Pickle')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'ProcessGroup', N'Pickle', N'酸洗', 1, 1, 19, 1, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'ProcessGroup' AND [FieldKey] = N'OuterPolish')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'ProcessGroup', N'OuterPolish', N'外抛光', 1, 1, 20, 1, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'ProcessGroup' AND [FieldKey] = N'InnerPolish')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'ProcessGroup', N'InnerPolish', N'内抛', 1, 1, 21, 1, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'ProcessGroup' AND [FieldKey] = N'InnerGrinding')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'ProcessGroup', N'InnerGrinding', N'内修磨', 1, 1, 22, 1, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'ProcessGroup' AND [FieldKey] = N'OuterSpotGrinding')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'ProcessGroup', N'OuterSpotGrinding', N'外点磨', 1, 1, 23, 1, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'ProcessGroup' AND [FieldKey] = N'SandBlasting')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'ProcessGroup', N'SandBlasting', N'喷砂', 1, 1, 24, 1, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'ProcessGroup' AND [FieldKey] = N'ShotBlasting')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'ProcessGroup', N'ShotBlasting', N'喷丸', 1, 1, 25, 1, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'ProcessGroup' AND [FieldKey] = N'Inspection')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'ProcessGroup', N'Inspection', N'检验', 1, 1, 26, 1, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'ProcessGroup' AND [FieldKey] = N'WeldingHead')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'ProcessGroup', N'WeldingHead', N'焊头', 1, 1, 27, 1, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'ProcessGroup' AND [FieldKey] = N'Welding')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'ProcessGroup', N'Welding', N'打头', 1, 1, 28, 1, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'ProcessGroup' AND [FieldKey] = N'Lubrication')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'ProcessGroup', N'Lubrication', N'润滑', 1, 1, 29, 1, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'ProcessGroup' AND [FieldKey] = N'Packing')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'ProcessGroup', N'Packing', N'包装', 1, 1, 30, 1, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'ProcessGroup' AND [FieldKey] = N'Warehouse')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'ProcessGroup', N'Warehouse', N'入库', 1, 1, 31, 1, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'ProcessGroup' AND [FieldKey] = N'Extra1')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'ProcessGroup', N'Extra1', N'备用1', 1, 1, 32, 1, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardColumnDefinitions] WHERE [BlockKey] = N'ProcessGroup' AND [FieldKey] = N'Extra2')
                    INSERT INTO [ProcessCardColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [RowIndex], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'ProcessGroup', N'Extra2', N'备用2', 1, 1, 33, 1, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 撤销：删除本迁移种子的工艺卡打印列布局配置（仅限 CreatedBy=System 的种子行）
            migrationBuilder.Sql("""
                DELETE FROM [ProcessCardColumnDefinitions] WHERE [CreatedBy] = N'System';
                """);
        }
    }
}
