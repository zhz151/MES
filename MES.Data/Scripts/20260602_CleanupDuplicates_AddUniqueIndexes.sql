-- =====================================================
-- 脚本：清理重复数据 + 创建唯一索引
-- 说明：
--   1) 清理 ProductionRecord 重复（保留最小 Id）
--   2) 清理 SectionOutsource 重复（优先保留有回收记录的）
--   3) 删除旧非唯一索引，创建唯一索引
-- 日期：2026-06-02
-- =====================================================

BEGIN TRANSACTION;

-- ========== 1. 清理 ProductionRecord 重复 ==========
-- 保留每组 (ProductionBatchId, ProcessGroupId, SectionName) 中 Id 最小的
DELETE r
FROM ProductionRecord r
INNER JOIN (
    SELECT ProductionBatchId, ProcessGroupId, SectionName,
        MIN(Id) AS KeepId
    FROM ProductionRecord
    GROUP BY ProductionBatchId, ProcessGroupId, SectionName
    HAVING COUNT(*) > 1
) dup ON dup.ProductionBatchId = r.ProductionBatchId
    AND dup.ProcessGroupId = r.ProcessGroupId
    AND dup.SectionName = r.SectionName
    AND r.Id > dup.KeepId;

SELECT @@ROWCOUNT AS ProductionRecordDeleted;

-- ========== 2. 清理 SectionOutsource 重复 ==========
-- 优先保留有回收记录的行；都没回收记录则保留 Id 最小的
-- Step 2a: 找出每组 (ProductionBatchId, ProcessGroupId, SectionName) 中有回收记录的行
-- Step 2b: 若没有有回收记录的行，保留 Id 最小的

-- 临时表：每组应保留的 Id
SELECT ProductionBatchId, ProcessGroupId, SectionName,
    COALESCE(
        -- 优先选有回收记录的
        (SELECT TOP 1 s2.Id
         FROM SectionOutsource s2
         WHERE s2.ProductionBatchId = s.ProductionBatchId
           AND s2.ProcessGroupId = s.ProcessGroupId
           AND s2.SectionName = s.SectionName
           AND EXISTS (SELECT 1 FROM OutsourceRecovery WHERE SectionOutsourceId = s2.Id)
         ORDER BY s2.Id),
        -- 无回收记录则选最小 Id
        MIN(s.Id)
    ) AS KeepId
INTO #SectionOutsourceToKeep
FROM SectionOutsource s
GROUP BY ProductionBatchId, ProcessGroupId, SectionName
HAVING COUNT(*) > 1;

DELETE s
FROM SectionOutsource s
INNER JOIN #SectionOutsourceToKeep k ON k.ProductionBatchId = s.ProductionBatchId
    AND k.ProcessGroupId = s.ProcessGroupId
    AND k.SectionName = s.SectionName
WHERE s.Id <> k.KeepId;

SELECT @@ROWCOUNT AS SectionOutsourceDeleted;

DROP TABLE #SectionOutsourceToKeep;

-- ========== 3. 删除旧非唯一索引 ==========
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ProductionRecord_Section' AND object_id = OBJECT_ID('ProductionRecord'))
    DROP INDEX [IX_ProductionRecord_Section] ON [ProductionRecord];

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SectionOutsource_Section' AND object_id = OBJECT_ID('SectionOutsource'))
    DROP INDEX [IX_SectionOutsource_Section] ON [SectionOutsource];

-- ========== 4. 创建唯一索引 ==========
CREATE UNIQUE INDEX [UK_ProductionRecord_Section]
    ON [ProductionRecord] ([ProductionBatchId], [ProcessGroupId], [SectionName]);

CREATE UNIQUE INDEX [UK_SectionOutsource_Section]
    ON [SectionOutsource] ([ProductionBatchId], [ProcessGroupId], [SectionName]);

COMMIT;
GO
