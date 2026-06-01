-- =====================================================
-- 脚本：修复 CurrentGroupName + 回填 NextProcess
-- 说明：
--   1) 将仍为"冷轧"的 CurrentGroupName 按 ProcessGroup 更新
--   2) 回填 NextProcess（下个工序组的工序名称）
-- 日期：2026-06-02
-- =====================================================

BEGIN TRANSACTION;

-- ========== 1. 修复 CurrentGroupName ==========
-- 之前使用的 COALESCE(ProductionRecord, ProcessGroup) 会从
-- 尚未更新的 ProductionRecord 中重新读到"冷轧"，改用仅 ProcessGroup 方式
UPDATE b
SET b.CurrentGroupName = pg.ProcessName
FROM ProductionBatch b
CROSS APPLY (
    SELECT TOP 1 pg.ProcessName
    FROM ProcessGroup pg
    WHERE pg.ProductionBatchId = b.Id
      AND pg.ProcessName IN ('60冷轧', '50冷轧', '30冷轧', '20冷轧', '三辊冷轧')
    ORDER BY pg.SequenceNumber
) pg
WHERE b.CurrentGroupName = '冷轧';

SELECT @@ROWCOUNT AS CurrentGroupNameFixed;

-- ========== 2. 回填 NextProcess ==========
-- 思路：用 UNPIVOT 展开所有 ProcessGroup 的工段，得到全局序号
-- 取各批次最大执行序号 + 1 的工段所属 ProcessGroup 的 ProcessName

WITH SectionUnpivot AS (
    SELECT
        pg.ProductionBatchId,
        pg.ProcessName,
        pg.SequenceNumber AS PgSeq,
        v.SectionName,
        v.Sequence
    FROM ProcessGroup pg
    CROSS APPLY (
        SELECT '冷轧拔', ColdRollDraw WHERE ColdRollDraw IS NOT NULL
        UNION ALL SELECT '油管断', OilPipeCut WHERE OilPipeCut IS NOT NULL
        UNION ALL SELECT '去油', Degrease WHERE Degrease IS NOT NULL
        UNION ALL SELECT '固溶', Solution WHERE Solution IS NOT NULL
        UNION ALL SELECT '矫直', Straighten WHERE Straighten IS NOT NULL
        UNION ALL SELECT '断切', Cut WHERE Cut IS NOT NULL
        UNION ALL SELECT '测壁厚', ThicknessMeasure WHERE ThicknessMeasure IS NOT NULL
        UNION ALL SELECT '酸洗', Pickle WHERE Pickle IS NOT NULL
        UNION ALL SELECT '外抛光', OuterPolish WHERE OuterPolish IS NOT NULL
        UNION ALL SELECT '内修磨', InnerGrinding WHERE InnerGrinding IS NOT NULL
        UNION ALL SELECT '外点磨', OuterSpotGrinding WHERE OuterSpotGrinding IS NOT NULL
        UNION ALL SELECT '检验', Inspection WHERE Inspection IS NOT NULL
        UNION ALL SELECT '打焊头', WeldingHead WHERE WeldingHead IS NOT NULL
        UNION ALL SELECT '润滑', Lubrication WHERE Lubrication IS NOT NULL
        UNION ALL SELECT '入库', Warehouse WHERE Warehouse IS NOT NULL
    ) v(SectionName, Sequence)
),
-- 各批次所有记录的最大序列号
MaxSeqByBatch AS (
    SELECT ProductionBatchId, MAX(SequenceNumber) AS MaxSeq
    FROM (
        SELECT ProductionBatchId, SequenceNumber FROM ProductionRecord
        UNION ALL
        SELECT ProductionBatchId, SequenceNumber FROM SectionOutsource
        UNION ALL
        SELECT ProductionBatchId, SequenceNumber FROM ProcessInspection
    ) AllRecords
    GROUP BY ProductionBatchId
),
-- 取 MaxSeq+1 所在工段及其 ProcessGroup
NextSectionInfo AS (
    SELECT
        m.ProductionBatchId,
        s.ProcessName AS NextProcess
    FROM MaxSeqByBatch m
    INNER JOIN SectionUnpivot s ON s.ProductionBatchId = m.ProductionBatchId
        AND s.Sequence = m.MaxSeq + 1
)
UPDATE b
SET b.NextProcess = n.NextProcess
FROM ProductionBatch b
INNER JOIN NextSectionInfo n ON n.ProductionBatchId = b.Id;

SELECT @@ROWCOUNT AS NextProcessBackfilled;

COMMIT;
GO
