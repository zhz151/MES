-- =====================================================
-- 新增执行状态"入库存疑"：有入库数据但无检验记录的批量更新
-- 执行方式：SQL Server Management Studio 或 sqlcmd
-- =====================================================

SET NOCOUNT ON;

PRINT N'开始更新 QualityStatus = "入库存疑"...';

UPDATE qpt
SET
    qpt.[QualityStatus] = N'入库存疑',
    qpt.[UpdatedTime] = SYSDATETIMEOFFSET(),
    qpt.[LastRefreshTime] = SYSDATETIME()
FROM [dbo].[QualityProcessTracking] qpt
INNER JOIN [dbo].[MaterialReceiveCheck] rc ON qpt.[MaterialReceiveCheckId] = rc.[Id]
INNER JOIN [dbo].[ProductionBatch] pb ON rc.[ProductionBatchId] = pb.[Id]
WHERE qpt.[QualityStatus] = N'待检验'
  AND EXISTS (SELECT 1 FROM [dbo].[InventoryBatch] ib WHERE ib.[ProductionBatchNo] = pb.[BatchNo])
  AND NOT EXISTS (SELECT 1 FROM [dbo].[FinalInspection] fi WHERE fi.[ProductionBatchId] = rc.[ProductionBatchId]);

PRINT N'更新完成。受影响行数: ' + CAST(@@ROWCOUNT AS NVARCHAR(10));
GO
