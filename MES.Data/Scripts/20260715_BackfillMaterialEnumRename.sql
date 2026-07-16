-- ============================================================
-- 20260715 物料枚举重命名数据迁移
--
-- 变更内容：
--   1. ManufacturingItem 移除 IntermediateProduct
--      - ProductionBatch:    列必填，设为 OrderFinishedProduct
--      - MaterialReceiveCheck: 设为 NULL
--      - QualityProcessTracking: 设为 NULL
--   2. MaterialCategory StockFinished → PreparedFinished
--   3. PipeCategory Intermediate → WorkInProgress
-- ============================================================

BEGIN TRANSACTION;

-- ========== 1. ManufacturingItem ==========

-- ProductionBatch：ManufacturingItem 为必填列，设为 OrderFinishedProduct
UPDATE [ProductionBatch]
SET [ManufacturingItem] = 'OrderFinishedProduct'
WHERE [ManufacturingItem] = 'IntermediateProduct';

-- MaterialReceiveCheck：设为 NULL
UPDATE [MaterialReceiveCheck]
SET [ManufacturingItem] = NULL
WHERE [ManufacturingItem] = 'IntermediateProduct';

-- QualityProcessTracking：设为 NULL
UPDATE [QualityProcessTracking]
SET [ManufacturingItem] = NULL
WHERE [ManufacturingItem] = 'IntermediateProduct';

-- ========== 2. MaterialCategory StockFinished → PreparedFinished ==========

UPDATE [Material]
SET [MaterialCategory] = 'PreparedFinished'
WHERE [MaterialCategory] = 'StockFinished';

UPDATE [SupplierProfile]
SET [MaterialCategory] = 'PreparedFinished'
WHERE [MaterialCategory] = 'StockFinished';

UPDATE [PurchaseOrder]
SET [MaterialCategory] = 'PreparedFinished'
WHERE [MaterialCategory] = 'StockFinished';

UPDATE [SubcontractOrder]
SET [OutMaterialCategory] = 'PreparedFinished'
WHERE [OutMaterialCategory] = 'StockFinished';

UPDATE [SubcontractReturnItem]
SET [MaterialCategory] = 'PreparedFinished'
WHERE [MaterialCategory] = 'StockFinished';

-- ========== 3. PipeCategory Intermediate → WorkInProgress ==========

UPDATE [Ncr]
SET [PipeCategory] = 'WorkInProgress'
WHERE [PipeCategory] = 'Intermediate';

-- ========== 4. InventoryBatch.MaterialType 中间品 → 半成品 ==========

UPDATE [InventoryBatch]
SET [MaterialType] = '半成品'
WHERE [MaterialType] = '中间品';

UPDATE [InventoryBatch]
SET [MaterialType] = '次品半成品'
WHERE [MaterialType] = '次品中间品';

-- ========== 5. 新增次品在制（暂无数据迁移，为新类型预留） ==========

-- ========== 6. RawMaterialType SemiFinished → RoughTube ==========
-- 枚举重命名：RawMaterialType.SemiFinished → RoughTube（英文名语义修正）
UPDATE [PurchaseSemiPlan]
SET [RawMaterialType] = 'RoughTube'
WHERE [RawMaterialType] = 'SemiFinished';

UPDATE [RoundBarPiercingPlan]
SET [RawMaterialType] = 'RoughTube'
WHERE [RawMaterialType] = 'SemiFinished';

-- ============================================================
COMMIT TRANSACTION;
