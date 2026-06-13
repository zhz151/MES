namespace MES.Shared.Constants;

/// <summary>
/// API 端点路径常量 — 集中管理所有 "api/xxx" 字符串，
/// 避免 30+ 个 Service 文件各自声明 private const string BaseUrl。
/// </summary>
public static class ApiEndpoints
{
    // ===== Auth =====
    public const string AuthBase = "api/auth";
    public const string AuthLogin = "api/auth/login";
    public const string AuthRefreshToken = "api/auth/refresh-token";

    // ===== Batch 批次 =====
    public const string Batch = "api/batch";
    public const string ProductionRecord = "api/production-record";
    public const string SectionOutsource = "api/section-outsource";
    public const string Pickling = "api/pickling";

    // ===== Order 订单 =====
    public const string Order = "api/order";

    // ===== WorkOrder 工单 =====
    public const string WorkOrder = "api/workorder";
    public const string WorkOrderExecution = "api/workorder-execution";
    public const string MaterialPlan = "api/material-plan";
    public const string StandardProcessCycle = "api/standard-process-cycle";

    // ===== Quality 质量 =====
    public const string ChemicalComposition = "api/chemical-composition";
    public const string ChemicalValidationRule = "api/chemical-validation-rule";
    public const string FinalInspection = "api/final-inspection";
    public const string FurnaceRegistration = "api/furnace-registration";
    public const string GradeMapping = "api/grade-mapping";
    public const string ProcessInspection = "api/process-inspection";
    public const string QualityProcessTracking = "api/quality-process-tracking";
    public const string Ncr = "api/ncr";

    // ===== Equipment 设备 =====
    public const string Equipment = "api/equipment";
    public const string InspectionRecord = "api/inspection-record";
    public const string MaintenanceOrder = "api/maintenance-order";
    public const string RepairOrder = "api/repair-order";

    // ===== Material 物料 =====
    public const string Material = "api/material";
    public const string PurchaseOrder = "api/purchase-order";
    public const string Subcontract = "api/subcontract";
    public const string Supplier = "api/supplier";

    // ===== Warehouse 仓库 =====
    public const string Warehouse = "api/warehouse";
    public const string Inventory = "api/inventory";

    // ===== Other =====
    public const string Customer = "api/customer";
    public const string DataExchange = "api/data-exchange";
    public const string Notification = "api/notification";
    public const string ProductionOverview = "api/production-overview";
    public const string Standard = "api/standard";
    public const string RawMaterialLockPlan = "api/raw-material-lock-plan";
    public const string OrderDemandAdjustment = "api/order-demand-adjustment";
    public const string Scan = "api/scan";
    public const string Workstation = "api/workstation";

    // ===== Scheduling 排程 =====
    public const string SectionProductionStatus = "api/section-production-status";
    public const string SectionFlowAnalysis = "api/section-flow-analysis";
    public const string WorkOrderSchedule = "api/workorder-schedule";
    public const string BatchPlan = "api/batch-plan";
    public const string ColdRollPlan = "api/cold-roll-plan";
    public const string ColdRollSpecSchedule = "api/cold-roll-spec-schedule";
    public const string FinalInspectionPlan = "api/final-inspection-plan";
    public const string BatchPlanSchedule = "api/batch-plan-schedule";
    public const string BatchPlanTarget = "api/batch-plan-target";

    // ===== Configuration 配置 =====
    public const string StandardWorkDay = "api/standard-work-day";
    public const string StandardWorkDayDeliveryState = "api/standard-work-day-delivery-state";
    public const string ConfigParameter = "api/config-parameter";
    public const string DailyOutputEstimate = "api/daily-output-estimate";

    // ===== 通用默认排序字段 =====
    public const string DefaultSortBy = "CreatedTime";
}
