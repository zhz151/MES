-- 回填 ProductionBatchInventory.OutboundRecordId
--
-- 背景：新增 OutboundRecordId 字段后，旧数据该字段为 NULL。
-- 旧数据不存在同一仓库批分批出库的情况，因此每个 InventoryBatchId
-- 在 OutboundRecord 表中只有一条匹配记录（OutboundType='ProductionPick'）。
--
-- 使用 CTE + ROW_NUMBER 确保即使存在多条出库记录也仅取第一条，避免非确定性更新。

WITH cte AS (
    SELECT
        pbi.Id AS PbiId,
        o.Id AS OutboundId,
        ROW_NUMBER() OVER (PARTITION BY pbi.Id ORDER BY o.Id) AS rn
    FROM ProductionBatchInventory pbi
    INNER JOIN OutboundRecord o
        ON o.InventoryBatchId = pbi.InventoryBatchId
        AND o.OutboundType = 'ProductionPick'
    WHERE pbi.OutboundRecordId IS NULL
)
UPDATE pbi
SET OutboundRecordId = cte.OutboundId
FROM ProductionBatchInventory pbi
INNER JOIN cte ON cte.PbiId = pbi.Id AND cte.rn = 1;

-- 输出影响行数
DECLARE @affected INT = @@ROWCOUNT;
PRINT '已回填 ProductionBatchInventory.OutboundRecordId 行数: ' + CAST(@affected AS NVARCHAR(10));
GO
