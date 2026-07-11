-- =====================================================
-- 制造状态 ProductStatus 重算（4 表）
-- 规则：
--   1. 工序 = '荒管处理' → '荒管'
--   2. 末道工序 + 制造物品成品类 → '成品'
--   3. 在制修检 + 非末道 + 批次有荒管处理 + 规格匹配 → '荒管'
--   4. 否则 → '在制'
-- =====================================================

-- ===== 1. ProductionRecord =====
UPDATE pr
SET ProductStatus = calculated.NewStatus
FROM ProductionRecord pr
INNER JOIN (
    SELECT
        rec.Id,
        CASE
            WHEN rec.ProcessName = N'荒管处理' THEN N'荒管'
            WHEN blp.LastProcessName = rec.ProcessName
                AND pb.ManufacturingItem IN ('OrderFinishedProduct', 'PreparedMaterial', 'SpecialDeliveryStatus')
                THEN N'成品'
            WHEN rec.ProcessName = N'在制修检'
                AND (blp.LastProcessName IS NULL OR rec.ProcessName != blp.LastProcessName)
                AND brt.ProductionBatchId IS NOT NULL
                AND rec.ManufacturingSpec = brt.ManufacturingSpec
                THEN N'荒管'
            ELSE N'在制'
        END AS NewStatus
    FROM ProductionRecord rec
    INNER JOIN ProductionBatch pb ON rec.ProductionBatchId = pb.Id
    OUTER APPLY (
        SELECT TOP 1 pg.ProcessName AS LastProcessName
        FROM ProcessGroup pg
        WHERE pg.ProductionBatchId = rec.ProductionBatchId
        ORDER BY pg.SequenceNumber DESC, pg.Id
    ) blp
    OUTER APPLY (
        SELECT TOP 1 pg.ProductionBatchId, pg.ManufacturingSpec
        FROM ProcessGroup pg
        WHERE pg.ProductionBatchId = rec.ProductionBatchId
            AND pg.ProcessName = N'荒管处理'
    ) brt
) calculated ON pr.Id = calculated.Id
WHERE pr.ProductStatus IS NULL
   OR pr.ProductStatus != calculated.NewStatus;

SELECT 'ProductionRecord' AS TableName, ProductStatus, COUNT(*) AS Count
FROM ProductionRecord
GROUP BY ProductStatus
ORDER BY ProductStatus;

-- ===== 2. SectionOutsource =====
UPDATE s
SET ProductStatus = calculated.NewStatus
FROM SectionOutsource s
INNER JOIN (
    SELECT
        rec.Id,
        CASE
            WHEN rec.ProcessName = N'荒管处理' THEN N'荒管'
            WHEN blp.LastProcessName = rec.ProcessName
                AND pb.ManufacturingItem IN ('OrderFinishedProduct', 'PreparedMaterial', 'SpecialDeliveryStatus')
                THEN N'成品'
            WHEN rec.ProcessName = N'在制修检'
                AND (blp.LastProcessName IS NULL OR rec.ProcessName != blp.LastProcessName)
                AND brt.ProductionBatchId IS NOT NULL
                AND rec.ManufacturingSpec = brt.ManufacturingSpec
                THEN N'荒管'
            ELSE N'在制'
        END AS NewStatus
    FROM SectionOutsource rec
    INNER JOIN ProductionBatch pb ON rec.ProductionBatchId = pb.Id
    OUTER APPLY (
        SELECT TOP 1 pg.ProcessName AS LastProcessName
        FROM ProcessGroup pg
        WHERE pg.ProductionBatchId = rec.ProductionBatchId
        ORDER BY pg.SequenceNumber DESC, pg.Id
    ) blp
    OUTER APPLY (
        SELECT TOP 1 pg.ProductionBatchId, pg.ManufacturingSpec
        FROM ProcessGroup pg
        WHERE pg.ProductionBatchId = rec.ProductionBatchId
            AND pg.ProcessName = N'荒管处理'
    ) brt
) calculated ON s.Id = calculated.Id
WHERE s.ProductStatus IS NULL
   OR s.ProductStatus != calculated.NewStatus;

SELECT 'SectionOutsource' AS TableName, ProductStatus, COUNT(*) AS Count
FROM SectionOutsource
GROUP BY ProductStatus
ORDER BY ProductStatus;

-- ===== 3. PicklingInRecord =====
UPDATE pir
SET ProductStatus = calculated.NewStatus
FROM PicklingInRecord pir
INNER JOIN (
    SELECT
        rec.Id,
        CASE
            WHEN rec.ProcessName = N'荒管处理' THEN N'荒管'
            WHEN blp.LastProcessName = rec.ProcessName
                AND pb.ManufacturingItem IN ('OrderFinishedProduct', 'PreparedMaterial', 'SpecialDeliveryStatus')
                THEN N'成品'
            WHEN rec.ProcessName = N'在制修检'
                AND (blp.LastProcessName IS NULL OR rec.ProcessName != blp.LastProcessName)
                AND brt.ProductionBatchId IS NOT NULL
                AND rec.ManufacturingSpec = brt.ManufacturingSpec
                THEN N'荒管'
            ELSE N'在制'
        END AS NewStatus
    FROM PicklingInRecord rec
    INNER JOIN ProductionBatch pb ON rec.ProductionBatchId = pb.Id
    OUTER APPLY (
        SELECT TOP 1 pg.ProcessName AS LastProcessName
        FROM ProcessGroup pg
        WHERE pg.ProductionBatchId = rec.ProductionBatchId
        ORDER BY pg.SequenceNumber DESC, pg.Id
    ) blp
    OUTER APPLY (
        SELECT TOP 1 pg.ProductionBatchId, pg.ManufacturingSpec
        FROM ProcessGroup pg
        WHERE pg.ProductionBatchId = rec.ProductionBatchId
            AND pg.ProcessName = N'荒管处理'
    ) brt
) calculated ON pir.Id = calculated.Id
WHERE pir.ProductStatus IS NULL
   OR pir.ProductStatus != calculated.NewStatus;

SELECT 'PicklingInRecord' AS TableName, ProductStatus, COUNT(*) AS Count
FROM PicklingInRecord
GROUP BY ProductStatus
ORDER BY ProductStatus;

-- ===== 4. ProcessInspection =====
UPDATE pi
SET ProductStatus = calculated.NewStatus
FROM ProcessInspection pi
INNER JOIN (
    SELECT
        rec.Id,
        CASE
            WHEN rec.ProcessName = N'荒管处理' THEN N'荒管'
            WHEN blp.LastProcessName = rec.ProcessName
                AND pb.ManufacturingItem IN ('OrderFinishedProduct', 'PreparedMaterial', 'SpecialDeliveryStatus')
                THEN N'成品'
            WHEN rec.ProcessName = N'在制修检'
                AND (blp.LastProcessName IS NULL OR rec.ProcessName != blp.LastProcessName)
                AND brt.ProductionBatchId IS NOT NULL
                AND rec.ManufacturingSpec = brt.ManufacturingSpec
                THEN N'荒管'
            ELSE N'在制'
        END AS NewStatus
    FROM ProcessInspection rec
    INNER JOIN ProductionBatch pb ON rec.ProductionBatchId = pb.Id
    OUTER APPLY (
        SELECT TOP 1 pg.ProcessName AS LastProcessName
        FROM ProcessGroup pg
        WHERE pg.ProductionBatchId = rec.ProductionBatchId
        ORDER BY pg.SequenceNumber DESC, pg.Id
    ) blp
    OUTER APPLY (
        SELECT TOP 1 pg.ProductionBatchId, pg.ManufacturingSpec
        FROM ProcessGroup pg
        WHERE pg.ProductionBatchId = rec.ProductionBatchId
            AND pg.ProcessName = N'荒管处理'
    ) brt
) calculated ON pi.Id = calculated.Id
WHERE pi.ProductStatus IS NULL
   OR pi.ProductStatus != calculated.NewStatus;

SELECT 'ProcessInspection' AS TableName, ProductStatus, COUNT(*) AS Count
FROM ProcessInspection
GROUP BY ProductStatus
ORDER BY ProductStatus;

PRINT '=== 重算完成 ===';
