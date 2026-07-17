-- ============================================================
-- 数据迁移：将中文枚举显示值转换为英文枚举名
-- 由于此前枚举字段存储了中文文本（如"备料成品"），
-- 而代码现已使用枚举名（如"Finished"），需同步 DB 数据。
-- ============================================================
-- 注意：此脚本应在确认无并发写入时执行。
-- 执行前建议备份数据库或至少备份受影响表。
-- ============================================================

BEGIN TRANSACTION;

-- ============================================================
-- 1. MaterialType 枚举映射（14 个值）
-- ============================================================
-- 影响表:
--   InventoryBatch.MaterialType
--   ProductionBatch.SourceMaterialType

UPDATE InventoryBatch SET MaterialType = 'Finished'              WHERE MaterialType = '备料成品';
UPDATE InventoryBatch SET MaterialType = 'OrderFinished'         WHERE MaterialType = '订单成品';
UPDATE InventoryBatch SET MaterialType = 'CriticalFinished'      WHERE MaterialType = '临界成品';
UPDATE InventoryBatch SET MaterialType = 'Surplus'               WHERE MaterialType = '余库料';
UPDATE InventoryBatch SET MaterialType = 'SemiFinished'          WHERE MaterialType = '半成品';
UPDATE InventoryBatch SET MaterialType = 'DefectSemi'            WHERE MaterialType = '次品半成品';
UPDATE InventoryBatch SET MaterialType = 'DefectFinished'        WHERE MaterialType = '次品成品';
UPDATE InventoryBatch SET MaterialType = 'RoughTube'             WHERE MaterialType = '荒管';
UPDATE InventoryBatch SET MaterialType = 'RoundBar'              WHERE MaterialType = '圆棒';
UPDATE InventoryBatch SET MaterialType = 'DefectRoundBar'        WHERE MaterialType = '次品圆棒';
UPDATE InventoryBatch SET MaterialType = 'DefectRoughTube'       WHERE MaterialType = '次品荒管';
UPDATE InventoryBatch SET MaterialType = 'Scrap'                 WHERE MaterialType = '报废品';
UPDATE InventoryBatch SET MaterialType = 'SpecialDeliveryStatus' WHERE MaterialType = '特定交态成品';
UPDATE InventoryBatch SET MaterialType = 'DefectWIP'             WHERE MaterialType = '次品在制';

UPDATE ProductionBatch SET SourceMaterialType = 'Finished'              WHERE SourceMaterialType = '备料成品';
UPDATE ProductionBatch SET SourceMaterialType = 'OrderFinished'         WHERE SourceMaterialType = '订单成品';
UPDATE ProductionBatch SET SourceMaterialType = 'CriticalFinished'      WHERE SourceMaterialType = '临界成品';
UPDATE ProductionBatch SET SourceMaterialType = 'Surplus'               WHERE SourceMaterialType = '余库料';
UPDATE ProductionBatch SET SourceMaterialType = 'SemiFinished'          WHERE SourceMaterialType = '半成品';
UPDATE ProductionBatch SET SourceMaterialType = 'DefectSemi'            WHERE SourceMaterialType = '次品半成品';
UPDATE ProductionBatch SET SourceMaterialType = 'DefectFinished'        WHERE SourceMaterialType = '次品成品';
UPDATE ProductionBatch SET SourceMaterialType = 'RoughTube'             WHERE SourceMaterialType = '荒管';
UPDATE ProductionBatch SET SourceMaterialType = 'RoundBar'              WHERE SourceMaterialType = '圆棒';
UPDATE ProductionBatch SET SourceMaterialType = 'DefectRoundBar'        WHERE SourceMaterialType = '次品圆棒';
UPDATE ProductionBatch SET SourceMaterialType = 'DefectRoughTube'       WHERE SourceMaterialType = '次品荒管';
UPDATE ProductionBatch SET SourceMaterialType = 'Scrap'                 WHERE SourceMaterialType = '报废品';
UPDATE ProductionBatch SET SourceMaterialType = 'SpecialDeliveryStatus' WHERE SourceMaterialType = '特定交态成品';
UPDATE ProductionBatch SET SourceMaterialType = 'DefectWIP'             WHERE SourceMaterialType = '次品在制';

-- ============================================================
-- 2. InboundSource 枚举映射（7 个值）
-- ============================================================
-- 影响表:
--   InventoryBatch.InboundSource
--   ProductionBatch.InboundSource

UPDATE InventoryBatch SET InboundSource = 'Purchase'            WHERE InboundSource = '外购';
UPDATE InventoryBatch SET InboundSource = 'Subcontract'         WHERE InboundSource = '委外';
UPDATE InventoryBatch SET InboundSource = 'ReturnIn'            WHERE InboundSource = '退货入库';
UPDATE InventoryBatch SET InboundSource = 'ProductionInbound'   WHERE InboundSource = '生产入库';
UPDATE InventoryBatch SET InboundSource = 'InspectionInbound'   WHERE InboundSource = '检验入库';
UPDATE InventoryBatch SET InboundSource = 'TransferIn'          WHERE InboundSource = '移库入库';
UPDATE InventoryBatch SET InboundSource = 'Other'               WHERE InboundSource = '其它';

UPDATE ProductionBatch SET InboundSource = 'Purchase'            WHERE InboundSource = '外购';
UPDATE ProductionBatch SET InboundSource = 'Subcontract'         WHERE InboundSource = '委外';
UPDATE ProductionBatch SET InboundSource = 'ReturnIn'            WHERE InboundSource = '退货入库';
UPDATE ProductionBatch SET InboundSource = 'ProductionInbound'   WHERE InboundSource = '生产入库';
UPDATE ProductionBatch SET InboundSource = 'InspectionInbound'   WHERE InboundSource = '检验入库';
UPDATE ProductionBatch SET InboundSource = 'TransferIn'          WHERE InboundSource = '移库入库';
UPDATE ProductionBatch SET InboundSource = 'Other'               WHERE InboundSource = '其它';

-- ============================================================
-- 3. PipeManufacturingType (ProductionBatch.MaterialName)
--    原"物料名称"字段，存储 PipeManufacturingType
-- ============================================================
-- 注意：MaterialName 在旧系统中可能是自由文本，
-- 只转换明确为"无缝管"/"焊管"的值。

UPDATE ProductionBatch SET MaterialName = 'SeamlessPipe' WHERE MaterialName = '无缝管';
UPDATE ProductionBatch SET MaterialName = 'WeldedPipe'   WHERE MaterialName = '焊管';

-- ============================================================
-- 4. 仓库物料关联配置表（如果有）
-- ============================================================
-- InventoryMaterialTypes.WarehouseAllowedTypes 中使用的 MaterialType
-- 已改为枚举名，但该配置为内存常量，不涉及 DB 数据。

-- ============================================================
-- 提交事务
-- ============================================================
COMMIT;

-- 打印受影响行数
SELECT 'InventoryBatch.MaterialType 更新行数: ' + CAST(@@ROWCOUNT AS VARCHAR) AS Result;
GO

SELECT 'InventoryBatch.InboundSource 更新行数: ' + CAST(@@ROWCOUNT AS VARCHAR) AS Result;
GO

SELECT 'ProductionBatch.SourceMaterialType 更新行数: ' + CAST(@@ROWCOUNT AS VARCHAR) AS Result;
GO

SELECT 'ProductionBatch.InboundSource 更新行数: ' + CAST(@@ROWCOUNT AS VARCHAR) AS Result;
GO

SELECT 'ProductionBatch.MaterialName 更新行数: ' + CAST(@@ROWCOUNT AS VARCHAR) AS Result;
GO
