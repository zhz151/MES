-- ============================================================
-- ProcessInspection（过程检验记录）添加查询性能索引
-- 1. IX_ProcessInspection_InspectionDate — 默认排序优化
-- 2. IX_ProcessInspection_BatchNo — 搜索/筛选优化（实体已冗余 BatchNo 字段）
-- ============================================================

BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ProcessInspection_InspectionDate')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ProcessInspection_InspectionDate]
        ON [dbo].[ProcessInspection] ([InspectionDate] DESC)
        INCLUDE ([ProductionBatchId], [ProcessGroupId], [ProcessName], [ManufacturingSpec], [SectionName], [SequenceNumber],
                 [EquipmentName], [Inspector], [Shift], [Quantity], [Weight], [InspectionItem],
                 [QualifiedQuantity], [QualifiedWeight], [QualifiedConcessionQuantity], [ConcessionRemark],
                 [DefectReworkQuantity], [DefectWarehouseQuantity], [DefectScrapQuantity], [DefectDescription],
                 [SourceUnit], [TagNo], [BatchNo], [PlantGrade], [Remark], [DataSource], [CreatedTime], [UpdatedTime]);
    PRINT 'Created IX_ProcessInspection_InspectionDate';
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ProcessInspection_BatchNo')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ProcessInspection_BatchNo]
        ON [dbo].[ProcessInspection] ([BatchNo])
        INCLUDE ([InspectionDate], [ProcessName], [SectionName], [Quantity], [Weight]);
    PRINT 'Created IX_ProcessInspection_BatchNo';
END

COMMIT TRANSACTION;
GO
