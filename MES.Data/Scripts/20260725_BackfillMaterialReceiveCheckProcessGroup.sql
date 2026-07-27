-- ============================================================
-- 回填 MaterialReceiveCheck 新增字段：ProcessGroupId / ProcessName / SequenceNumber
-- 匹配规则：ProcessGroup.ManufacturingSpec == ProductionBatch.Specification
-- 优先非"附加成检"的工序组，同优先级取工序组序号最小
-- SequenceNumber = pg.Inspection（检验工段在该工序组中的执行序号，非工序组序号）
-- ============================================================

BEGIN TRANSACTION;

WITH MatchedGroups AS (
    SELECT
        rc.Id AS MaterialReceiveCheckId,
        pg.Id AS ProcessGroupId,
        pg.ProcessName,
        pg.Inspection AS SequenceNumber,
        ROW_NUMBER() OVER (
            PARTITION BY rc.Id
            ORDER BY
                CASE WHEN pg.ProcessName = N'附加成检' THEN 1 ELSE 0 END,  -- 非附加成检优先
                pg.SequenceNumber ASC                                       -- 同优先级取工序组序号最小
        ) AS rn
    FROM MaterialReceiveCheck rc
    INNER JOIN ProductionBatch pb ON rc.ProductionBatchId = pb.Id
    INNER JOIN ProcessGroup pg ON pg.ProductionBatchId = pb.Id
        AND pg.ManufacturingSpec = pb.Specification
        AND pg.Inspection IS NOT NULL
    WHERE rc.ProcessGroupId = 0  -- 仅回填尚未配置的记录
)
UPDATE rc
SET
    rc.ProcessGroupId = mg.ProcessGroupId,
    rc.ProcessName = mg.ProcessName,
    rc.SequenceNumber = mg.SequenceNumber
FROM MaterialReceiveCheck rc
INNER JOIN MatchedGroups mg ON mg.MaterialReceiveCheckId = rc.Id AND mg.rn = 1;

COMMIT TRANSACTION;

-- 验证结果
SELECT
    COUNT(*) AS TotalRecords,
    SUM(CASE WHEN ProcessGroupId > 0 THEN 1 ELSE 0 END) AS HasProcessGroup,
    SUM(CASE WHEN ProcessGroupId = 0 THEN 1 ELSE 0 END) AS MissingProcessGroup
FROM MaterialReceiveCheck;
GO
