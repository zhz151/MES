-- =====================================================
-- 脚本：批量更新 CurrentGroupName 冷轧→子类型
-- 说明：将 CurrentGroupName='冷轧' 的批次，根据其生产记录
--       或工序组中的实际冷轧子类型进行更新
-- 日期：2026-06-02
-- =====================================================

BEGIN TRANSACTION;

UPDATE b
SET b.CurrentGroupName = COALESCE(
    -- 优先取生产记录中最大 SequenceNumber 对应的工序名称
    (
        SELECT TOP 1 pr.ProcessName
        FROM ProductionRecord pr
        INNER JOIN ProcessGroup pg ON pg.Id = pr.ProcessGroupId
        WHERE pr.ProductionBatchId = b.Id
          AND pg.ProcessName IN ('60冷轧', '50冷轧', '30冷轧', '20冷轧', '三辊冷轧')
        ORDER BY pr.SequenceNumber DESC
    ),
    -- 无生产记录时取工序组中第一个冷轧子类型
    (
        SELECT TOP 1 pg.ProcessName
        FROM ProcessGroup pg
        WHERE pg.ProductionBatchId = b.Id
          AND pg.ProcessName IN ('60冷轧', '50冷轧', '30冷轧', '20冷轧', '三辊冷轧')
        ORDER BY pg.SequenceNumber
    )
)
FROM ProductionBatch b
WHERE b.CurrentGroupName = '冷轧';

SELECT @@ROWCOUNT AS UpdatedCount;

-- 更新工段委外中的 ProcessName（旧数据可能也有"冷轧"）
UPDATE s
SET s.ProcessName = pg.ProcessName
FROM SectionOutsource s
INNER JOIN ProcessGroup pg ON pg.Id = s.ProcessGroupId
WHERE s.ProcessName = '冷轧'
  AND pg.ProcessName IN ('60冷轧', '50冷轧', '30冷轧', '20冷轧', '三辊冷轧');

SELECT @@ROWCOUNT AS OutsourceUpdatedCount;

-- 更新生产记录中的 ProcessName
UPDATE r
SET r.ProcessName = pg.ProcessName
FROM ProductionRecord r
INNER JOIN ProcessGroup pg ON pg.Id = r.ProcessGroupId
WHERE r.ProcessName = '冷轧'
  AND pg.ProcessName IN ('60冷轧', '50冷轧', '30冷轧', '20冷轧', '三辊冷轧');

SELECT @@ROWCOUNT AS RecordUpdatedCount;

-- 更新过程检验中的 ProcessName
UPDATE pi
SET pi.ProcessName = pg.ProcessName
FROM ProcessInspection pi
INNER JOIN ProcessGroup pg ON pg.Id = pi.ProcessGroupId
WHERE pi.ProcessName = '冷轧'
  AND pg.ProcessName IN ('60冷轧', '50冷轧', '30冷轧', '20冷轧', '三辊冷轧');

SELECT @@ROWCOUNT AS InspectionUpdatedCount;

COMMIT;
GO
