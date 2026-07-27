-- ============================================================
-- 回填 MaterialReceiveCheck 新增字段：LengthStatus / ProductionCutQuantity / ProductionWeight
-- 创建时快照，现有记录从 ProductionBatch + ProductionRecord 计算
-- 逻辑与 ComputeMaterialCheckQuantities() 保持一致
-- ============================================================

BEGIN TRANSACTION;

-- 1. 回填 LengthStatus（从关联批次复制）
UPDATE rc
SET rc.LengthStatus = pb.LengthStatus
FROM MaterialReceiveCheck rc
INNER JOIN ProductionBatch pb ON rc.ProductionBatchId = pb.Id
WHERE rc.LengthStatus IS NULL;

-- 2. 回填 ProductionCutQuantity 和 ProductionWeight
--    库存类（库存/外购/返整/委外加工）→ 现有效原料支数/重量
--    加工类（荒管生产/在制生产/对外加工 及其他）→ 切管记录汇总 / 目标重量
UPDATE rc
SET
    rc.ProductionCutQuantity =
        CASE
            WHEN ISNULL(rc.ProductionType, pb.ProductionType) IN ('Inventory', 'OutsourcedPurchased', 'Rework', 'Subcontract')
            THEN ISNULL(pb.CurrentValidQty, 0)
            ELSE ISNULL((
                SELECT SUM(ISNULL(pr.PostCutQuantity, 0))
                FROM ProductionRecord pr
                WHERE pr.ProductionBatchId = rc.ProductionBatchId
                  AND pr.SectionName = N'断切'
                  AND pr.IsFinished = 1
            ), 0)
        END,
    rc.ProductionWeight =
        CASE
            WHEN ISNULL(rc.ProductionType, pb.ProductionType) IN ('Inventory', 'OutsourcedPurchased', 'Rework', 'Subcontract')
            THEN pb.CurrentValidWeight
            WHEN pb.CurrentValidWeight IS NULL THEN NULL
            ELSE CAST(ROUND(pb.CurrentValidWeight * (1.0 - ISNULL(egc.GroupCount, 0) * 0.025), 0) AS DECIMAL(18,2))
        END
FROM MaterialReceiveCheck rc
INNER JOIN ProductionBatch pb ON rc.ProductionBatchId = pb.Id
OUTER APPLY (
    SELECT COUNT(*) AS GroupCount
    FROM ProcessGroup pg
    WHERE pg.ProductionBatchId = pb.Id
      AND pg.ProcessName NOT IN ('在制修检', '附加成检')
      AND (pg.ColdRollDraw IS NOT NULL OR pg.OilPipeCut IS NOT NULL
           OR pg.Degrease IS NOT NULL OR pg.Solution IS NOT NULL
           OR pg.Straighten IS NOT NULL OR pg.Cut IS NOT NULL
           OR pg.ThicknessMeasure IS NOT NULL OR pg.Pickle IS NOT NULL
           OR pg.OuterPolish IS NOT NULL OR pg.InnerGrinding IS NOT NULL
           OR pg.OuterSpotGrinding IS NOT NULL OR pg.Inspection IS NOT NULL
           OR pg.WeldingHead IS NOT NULL OR pg.Lubrication IS NOT NULL
           OR pg.Warehouse IS NOT NULL)
) egc;

COMMIT TRANSACTION;

-- 验证结果
SELECT
    COUNT(*) AS TotalRecords,
    SUM(CASE WHEN LengthStatus IS NOT NULL THEN 1 ELSE 0 END) AS HasLengthStatus,
    SUM(CASE WHEN ProductionCutQuantity > 0 OR rc.ProductionType IN ('Inventory','OutsourcedPurchased','Rework','Subcontract') THEN 1 ELSE 0 END) AS HasProductionCutQuantity,
    SUM(CASE WHEN ProductionWeight IS NOT NULL THEN 1 ELSE 0 END) AS HasProductionWeight
FROM MaterialReceiveCheck rc;
GO
