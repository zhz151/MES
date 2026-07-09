-- ============================================================
-- 回填 Equipment 物化状态字段（RunningStatus / InspectionStatus / MaintStatus）
-- 在迁移 AddEquipmentMaterializedStatus 执行后运行
-- ============================================================

-- ========== 1. RunningStatus ==========
-- 逻辑：取最新 RepairOrder，完成→Normal，维修中→InProgress，待修→Pending，无记录→Normal
UPDATE e
SET e.RunningStatus =
    CASE
        WHEN latest.RepairEndTime IS NOT NULL THEN 'Normal'
        WHEN latest.RepairStartTime IS NOT NULL THEN 'InProgress'
        WHEN latest.RepairOrderNo IS NOT NULL THEN 'Pending'
        ELSE 'Normal'
    END
FROM Equipment e
OUTER APPLY (
    SELECT TOP 1 r.RepairStartTime, r.RepairEndTime, r.RepairOrderNo
    FROM RepairOrder r
    WHERE r.EquipmentId = e.Id
    ORDER BY COALESCE(r.RepairStartTime, r.RepairEndTime, r.CreatedTime) DESC
) latest;

-- ========== 2. InspectionStatus ==========
-- 逻辑：不须点检→NotApplicable，无起始日→Pending，今天<起始日→Normal
--       周期内有记录→Normal，已过周期末→Overdue，其他→Pending
UPDATE e
SET e.InspectionStatus =
    CASE
        WHEN e.NeedInspection = 0 THEN 'NotApplicable'
        WHEN e.CurrentInspectionStartDate IS NULL THEN 'Pending'
        WHEN CAST(GETDATE() AS DATE) < e.CurrentInspectionStartDate THEN 'Normal'
        WHEN EXISTS (
            SELECT 1 FROM InspectionRecord r
            WHERE r.EquipmentId = e.Id
              AND r.ActualDate IS NOT NULL
              AND r.ActualDate >= e.CurrentInspectionStartDate
              AND r.ActualDate <= DATEADD(DAY, e.InspectionCycleDays - 1, e.CurrentInspectionStartDate)
        ) THEN 'Normal'
        WHEN CAST(GETDATE() AS DATE) > DATEADD(DAY, e.InspectionCycleDays - 1, e.CurrentInspectionStartDate) THEN 'Overdue'
        ELSE 'Pending'
    END
FROM Equipment e;

-- ========== 3. MaintStatus ==========
-- 逻辑同上（查 MaintenanceOrder）
UPDATE e
SET e.MaintStatus =
    CASE
        WHEN e.NeedMaintenance = 0 THEN 'NotApplicable'
        WHEN e.CurrentMaintStartDate IS NULL THEN 'Pending'
        WHEN CAST(GETDATE() AS DATE) < e.CurrentMaintStartDate THEN 'Normal'
        WHEN EXISTS (
            SELECT 1 FROM MaintenanceOrder m
            WHERE m.EquipmentId = e.Id
              AND m.ActualDate IS NOT NULL
              AND m.ActualDate >= e.CurrentMaintStartDate
              AND m.ActualDate <= DATEADD(DAY, e.MaintCycleDays - 1, e.CurrentMaintStartDate)
        ) THEN 'Normal'
        WHEN CAST(GETDATE() AS DATE) > DATEADD(DAY, e.MaintCycleDays - 1, e.CurrentMaintStartDate) THEN 'Overdue'
        ELSE 'Pending'
    END
FROM Equipment e;

-- ========== 验证 ==========
SELECT
    COUNT(*) AS Total,
    SUM(CASE WHEN RunningStatus = 'Normal' THEN 1 ELSE 0 END) AS Running_Normal,
    SUM(CASE WHEN RunningStatus = 'Pending' THEN 1 ELSE 0 END) AS Running_Pending,
    SUM(CASE WHEN RunningStatus = 'InProgress' THEN 1 ELSE 0 END) AS Running_InProgress,
    SUM(CASE WHEN InspectionStatus = 'Normal' THEN 1 ELSE 0 END) AS Inspection_Normal,
    SUM(CASE WHEN InspectionStatus = 'Overdue' THEN 1 ELSE 0 END) AS Inspection_Overdue,
    SUM(CASE WHEN MaintStatus = 'Normal' THEN 1 ELSE 0 END) AS Maint_Normal,
    SUM(CASE WHEN MaintStatus = 'Overdue' THEN 1 ELSE 0 END) AS Maint_Overdue
FROM Equipment;
