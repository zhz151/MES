-- 修复在产主工单计划的 StandardCycle：取主工单各计划的最大工艺周期
-- 旧数据使用 DefaultValue/StandardCycle=2 创建，现需更正为从主工单的计划推算
-- 若主工单无任何计划（如 D26Z2604093-X04），则回退到 DefaultProcessCycle(22)
UPDATE imp
SET imp.StandardCycle = COALESCE(sub.MaxCycle, 22)
FROM InMainWorkOrderPlan imp
INNER JOIN WorkOrder w ON w.WorkOrderNo = imp.MainWorkOrderNo
OUTER APPLY (
    SELECT MAX(c) AS MaxCycle FROM (
        SELECT StandardCycle AS c FROM PurchaseSemiPlan WHERE WorkOrderId = w.Id AND StandardCycle > 0
        UNION ALL
        SELECT StandardCycle FROM PurchaseFinishedPlan WHERE WorkOrderId = w.Id AND StandardCycle > 0
        UNION ALL
        SELECT StandardCycle FROM InventoryPlan WHERE WorkOrderId = w.Id AND PlanStatus != 'Cancelled' AND StandardCycle > 0
        UNION ALL
        SELECT StandardCycle FROM RoundBarPiercingPlan WHERE WorkOrderId = w.Id AND StandardCycle > 0
        UNION ALL
        SELECT StandardCycle FROM InProcessReworkPlan WHERE WorkOrderId = w.Id AND PlanStatus != 'Cancelled' AND StandardCycle > 0
        UNION ALL
        SELECT StandardCycle FROM InMainWorkOrderPlan WHERE WorkOrderId = w.Id AND PlanStatus != 'Cancelled' AND StandardCycle > 0
    ) allc
) sub
WHERE imp.StandardCycle < 22;  -- 只修复低于合理值的旧数据

PRINT '已修复在产主工单计划 StandardCycle';
GO
