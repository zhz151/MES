-- ============================================================
-- 20260727 统一全系统枚举字符串值（ProductionBatch + 关联表）
--
-- 背景：
--   ProductionBatch.ManufacturingItem 在早期迁移中被设为
--   'OrderFinishedProduct'（见 20260715_BackfillMaterialEnumRename.sql），
--   但代码中已使用 MaterialType.OrderFinished 枚举名 'OrderFinished'
--   做过滤比较（如 GetAvailableMainWorkOrderBatchesAsync）。
--
--   这导致 893 条有效批次因值不匹配被过滤掉，在产主工单计划
--   查不到可用批次。
--
-- 统一范围：
--   1. ProductionBatch.ManufacturingItem
--      OrderFinishedProduct → OrderFinished
--      PreparedMaterial → Surplus
--   2. QualityProcessTracking.ManufacturingItem
--      OrderFinishedProduct → OrderFinished
--      PreparedMaterial → Surplus
--   3. InventoryBatch.InboundSource
--      '其他'(中文) → Other（枚举名）
-- ============================================================

SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ========== 1. ProductionBatch ==========

-- 旧枚举 OrderFinishedProduct → OrderFinished（MaterialType.OrderFinished）
UPDATE [ProductionBatch]
SET [ManufacturingItem] = 'OrderFinished'
WHERE [ManufacturingItem] = 'OrderFinishedProduct';

-- 旧值 PreparedMaterial → Surplus（MaterialType.Surplus = 余库料）
UPDATE [ProductionBatch]
SET [ManufacturingItem] = 'Surplus'
WHERE [ManufacturingItem] = 'PreparedMaterial';

-- ========== 2. QualityProcessTracking ==========

UPDATE [QualityProcessTracking]
SET [ManufacturingItem] = 'OrderFinished'
WHERE [ManufacturingItem] = 'OrderFinishedProduct';

UPDATE [QualityProcessTracking]
SET [ManufacturingItem] = 'Surplus'
WHERE [ManufacturingItem] = 'PreparedMaterial';

-- ========== 3. InventoryBatch ==========

-- 注意：中文"其他"需用十六进制匹配（编码 0x8253E890）
UPDATE [InventoryBatch]
SET [InboundSource] = 'Other'
WHERE CAST([InboundSource] AS varbinary(100)) = 0x8253E890;

COMMIT TRANSACTION;

-- ============================================================
-- 验证
-- ============================================================
-- SELECT 'ProductionBatch' AS Tbl, ManufacturingItem, COUNT(*) FROM ProductionBatch GROUP BY ManufacturingItem;
-- SELECT 'QualityProcessTracking' AS Tbl, ManufacturingItem, COUNT(*) FROM QualityProcessTracking GROUP BY ManufacturingItem;
-- SELECT 'InventoryBatch' AS Tbl, InboundSource, COUNT(*) FROM InventoryBatch GROUP BY InboundSource;
--
-- 期望结果：
--   ProductionBatch:      Finished(4), OrderFinished(900), SpecialDeliveryStatus(1), Surplus(15)
--   QualityProcessTracking: OrderFinished(512), Surplus(1)
--   InventoryBatch:       Purchase(2102), InspectionInbound(2), Other(1097), Subcontract(1)
-- ============================================================
