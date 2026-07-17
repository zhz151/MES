-- ============================================================
-- 数据迁移：修复 Subcontract 订单表枚举值变更导致的数据兼容问题
-- 问题：SubcontractReturnItem.ProcessStatus 从
--   SubcontractProcessStatus（Pending/PartialReturned/Completed）
--   改为 SubcontractOrderStatus（Sent/PartialReturned/Completed）
--   DB 中旧值 "Pending" 无法被 EF Core 解析为 SubcontractOrderStatus，
--   导致列表页查询抛出 500 错误。
--
-- 同时清理 SubcontractOrder/SubcontractReturnItem 中其他
-- 枚举字段的中文显示值，确保数据与代码枚举名一致。
-- ============================================================
-- 注意：此脚本应在确认无并发写入时执行。
-- 执行前建议备份数据库或至少备份受影响表。
-- ============================================================

BEGIN TRANSACTION;

-- ============================================================
-- 1. SubcontractReturnItem.ProcessStatus
--    SubcontractProcessStatus → SubcontractOrderStatus 映射
--    Pending → Sent（原 Pending 重命名为 Sent）
--    PartialReturned → PartialReturned（不变）
--    Completed → Completed（不变）
-- ============================================================
UPDATE SubcontractReturnItem SET ProcessStatus = 'Sent' WHERE ProcessStatus = 'Pending';

-- 处理中文值（如果之前存储了中文显示名）
UPDATE SubcontractReturnItem SET ProcessStatus = 'Sent'             WHERE ProcessStatus = '已发出';
UPDATE SubcontractReturnItem SET ProcessStatus = 'PartialReturned'  WHERE ProcessStatus = '部分收回';
UPDATE SubcontractReturnItem SET ProcessStatus = 'Completed'        WHERE ProcessStatus = '已完成';

-- ============================================================
-- 2. SubcontractOrder.Status
--    SubcontractOrderStatus 枚举本身未变更，但可能存有中文值
-- ============================================================
UPDATE SubcontractOrder SET Status = 'Sent'             WHERE Status = '已发出';
UPDATE SubcontractOrder SET Status = 'PartialReturned'  WHERE Status = '部分收回';
UPDATE SubcontractOrder SET Status = 'Completed'        WHERE Status = '已完成';

-- ============================================================
-- 3. SubcontractOrder.OutMaterialCategory
--    MaterialType 枚举中文名 → 枚举名
-- ============================================================
UPDATE SubcontractOrder SET OutMaterialCategory = 'Finished'              WHERE OutMaterialCategory = '备料成品';
UPDATE SubcontractOrder SET OutMaterialCategory = 'OrderFinished'         WHERE OutMaterialCategory = '订单成品';
UPDATE SubcontractOrder SET OutMaterialCategory = 'CriticalFinished'      WHERE OutMaterialCategory = '临界成品';
UPDATE SubcontractOrder SET OutMaterialCategory = 'Surplus'               WHERE OutMaterialCategory = '余库料';
UPDATE SubcontractOrder SET OutMaterialCategory = 'SemiFinished'          WHERE OutMaterialCategory = '半成品';
UPDATE SubcontractOrder SET OutMaterialCategory = 'DefectSemi'            WHERE OutMaterialCategory = '次品半成品';
UPDATE SubcontractOrder SET OutMaterialCategory = 'DefectFinished'        WHERE OutMaterialCategory = '次品成品';
UPDATE SubcontractOrder SET OutMaterialCategory = 'RoughTube'             WHERE OutMaterialCategory = '荒管';
UPDATE SubcontractOrder SET OutMaterialCategory = 'RoundBar'              WHERE OutMaterialCategory = '圆棒';
UPDATE SubcontractOrder SET OutMaterialCategory = 'DefectRoundBar'        WHERE OutMaterialCategory = '次品圆棒';
UPDATE SubcontractOrder SET OutMaterialCategory = 'DefectRoughTube'       WHERE OutMaterialCategory = '次品荒管';
UPDATE SubcontractOrder SET OutMaterialCategory = 'Scrap'                 WHERE OutMaterialCategory = '报废品';
UPDATE SubcontractOrder SET OutMaterialCategory = 'SpecialDeliveryStatus' WHERE OutMaterialCategory = '特定交态成品';
UPDATE SubcontractOrder SET OutMaterialCategory = 'WorkInProgress'        WHERE OutMaterialCategory = '在制品';
UPDATE SubcontractOrder SET OutMaterialCategory = 'DefectWIP'             WHERE OutMaterialCategory = '次品在制';

-- 兼容旧 PipeCategory 枚举值
UPDATE SubcontractOrder SET OutMaterialCategory = 'RoughTube' WHERE OutMaterialCategory = 'TubeBlank';
UPDATE SubcontractOrder SET OutMaterialCategory = 'Surplus'   WHERE OutMaterialCategory = 'SurplusInventory';
UPDATE SubcontractOrder SET OutMaterialCategory = 'Finished'  WHERE OutMaterialCategory = 'PreparedFinished';

-- ============================================================
-- 4. SubcontractOrder.ProcessType
--    SubcontractProcessType 枚举中文名 → 枚举名
-- ============================================================
UPDATE SubcontractOrder SET ProcessType = 'Piercing'      WHERE ProcessType = '穿孔';
UPDATE SubcontractOrder SET ProcessType = 'ColdDrawing'   WHERE ProcessType = '冷拔';
UPDATE SubcontractOrder SET ProcessType = 'HeatTreatment' WHERE ProcessType = '热处理';
UPDATE SubcontractOrder SET ProcessType = 'Threading'     WHERE ProcessType = '车丝';
UPDATE SubcontractOrder SET ProcessType = 'Polishing'     WHERE ProcessType = '抛光';
UPDATE SubcontractOrder SET ProcessType = 'Cutting'       WHERE ProcessType = '切割';

-- ============================================================
-- 5. SubcontractReturnItem.MaterialCategory
--    MaterialType 枚举中文名 → 枚举名
-- ============================================================
UPDATE SubcontractReturnItem SET MaterialCategory = 'Finished'              WHERE MaterialCategory = '备料成品';
UPDATE SubcontractReturnItem SET MaterialCategory = 'OrderFinished'         WHERE MaterialCategory = '订单成品';
UPDATE SubcontractReturnItem SET MaterialCategory = 'CriticalFinished'      WHERE MaterialCategory = '临界成品';
UPDATE SubcontractReturnItem SET MaterialCategory = 'Surplus'               WHERE MaterialCategory = '余库料';
UPDATE SubcontractReturnItem SET MaterialCategory = 'SemiFinished'          WHERE MaterialCategory = '半成品';
UPDATE SubcontractReturnItem SET MaterialCategory = 'DefectSemi'            WHERE MaterialCategory = '次品半成品';
UPDATE SubcontractReturnItem SET MaterialCategory = 'DefectFinished'        WHERE MaterialCategory = '次品成品';
UPDATE SubcontractReturnItem SET MaterialCategory = 'RoughTube'             WHERE MaterialCategory = '荒管';
UPDATE SubcontractReturnItem SET MaterialCategory = 'RoundBar'              WHERE MaterialCategory = '圆棒';
UPDATE SubcontractReturnItem SET MaterialCategory = 'DefectRoundBar'        WHERE MaterialCategory = '次品圆棒';
UPDATE SubcontractReturnItem SET MaterialCategory = 'DefectRoughTube'       WHERE MaterialCategory = '次品荒管';
UPDATE SubcontractReturnItem SET MaterialCategory = 'Scrap'                 WHERE MaterialCategory = '报废品';
UPDATE SubcontractReturnItem SET MaterialCategory = 'SpecialDeliveryStatus' WHERE MaterialCategory = '特定交态成品';
UPDATE SubcontractReturnItem SET MaterialCategory = 'WorkInProgress'        WHERE MaterialCategory = '在制品';
UPDATE SubcontractReturnItem SET MaterialCategory = 'DefectWIP'             WHERE MaterialCategory = '次品在制';

-- ============================================================
-- 6. SubcontractReturnItem.ProcessStatusRemark
--    如果存储了中文枚举状态文本（视业务情况处理）
-- ============================================================
-- ProcessStatusRemark 为自由文本备注，不强制转换

-- ============================================================
-- 提交事务
-- ============================================================
COMMIT;

-- 打印受影响行数
SELECT 'SubcontractReturnItem.ProcessStatus 更新行数: ' + CAST(@@ROWCOUNT AS VARCHAR) AS Result;
GO
SELECT 'SubcontractOrder.Status 更新行数: ' + CAST(@@ROWCOUNT AS VARCHAR) AS Result;
GO
SELECT 'SubcontractOrder.OutMaterialCategory 更新行数: ' + CAST(@@ROWCOUNT AS VARCHAR) AS Result;
GO
SELECT 'SubcontractOrder.ProcessType 更新行数: ' + CAST(@@ROWCOUNT AS VARCHAR) AS Result;
GO
SELECT 'SubcontractReturnItem.MaterialCategory 更新行数: ' + CAST(@@ROWCOUNT AS VARCHAR) AS Result;
GO
