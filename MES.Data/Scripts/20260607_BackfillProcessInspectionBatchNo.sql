-- ============================================================
-- 回填 ProcessInspection（过程检验记录）BatchNo 字段
-- 实体已有 BatchNo 冗余字段，但早期创建记录时未填充
-- 从关联 ProductionBatch 复制批次号
-- ============================================================

BEGIN TRANSACTION;

UPDATE pi
SET pi.BatchNo = pb.BatchNo
FROM ProcessInspection pi
INNER JOIN ProductionBatch pb ON pi.ProductionBatchId = pb.Id
WHERE pi.BatchNo IS NULL;

COMMIT TRANSACTION;

-- 验证结果
SELECT
    COUNT(*) AS TotalRecords,
    SUM(CASE WHEN BatchNo IS NOT NULL THEN 1 ELSE 0 END) AS HasBatchNo,
    SUM(CASE WHEN BatchNo IS NULL THEN 1 ELSE 0 END) AS MissingBatchNo
FROM ProcessInspection;
GO
