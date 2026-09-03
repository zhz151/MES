namespace MES.Shared.Constants;

/// <summary>
/// API 端点路径常量 — 集中管理所有 "api/xxx" 字符串，
/// 避免 30+ 个 Service 文件各自声明 private const string BaseUrl。
/// </summary>
public static class ApiEndpoints
{
    // ===== Auth =====
    public const string AuthLogin = "api/auth/login";
    public const string AuthRefreshToken = "api/auth/refresh-token";

    // ===== Batch 批次 =====
    public const string Batch = "api/batch";
    public const string ProductionRecord = "api/production-record";
    public const string SectionOutsource = "api/section-outsource";
    public const string Pickling = "api/pickling";

    // ===== Order 订单 =====
    public const string Order = "api/order";
    public const string Customer = "api/customer";

    // ===== WorkOrder 工单 =====
    public const string WorkOrder = "api/workorder";
    public const string WorkOrderExecution = "api/workorder-execution";
    // 工单需求调整（实体/服务/Controller 均属 WorkOrder 上下文）
    public const string OrderDemandAdjustment = "api/workorder-demand-adjustment";
    public const string FixedLengthWorkOrder = "api/fixed-length-work-order";
    public const string MaterialPlan = "api/material-plan";
    public const string Notification = "api/notification";

    // ===== Quality 质量 =====
    public const string Certificate = "api/certificate";
    public const string MaterialReceiveCheck = "api/material-receive-check";
    public const string FinalInspection = "api/final-inspection";
    public const string FurnaceRegistration = "api/furnace-registration";
    public const string ProcessInspection = "api/process-inspection";
    public const string QualityProcessTracking = "api/quality-process-tracking";
    public const string Ncr = "api/ncr";
    public const string ChemicalAnalysis = "api/chemical-analysis";
    public const string HardnessTest = "api/hardness-test";
    public const string GrainSizeTest = "api/grain-size-test";
    public const string FlatteningTest = "api/flattening-test";
    public const string FlaringTest = "api/flaring-test";
    public const string IntergranularCorrosionTest = "api/intergranular-corrosion-test";
    public const string MetallographicTest = "api/metallographic-test";
    public const string PittingCorrosionTest = "api/pitting-corrosion-test";
    public const string TensileTest = "api/tensile-test";

    // ===== Equipment 设备 =====
    public const string Equipment = "api/equipment";
    public const string InspectionRecord = "api/inspection-record";
    public const string MaintenanceOrder = "api/maintenance-order";
    public const string RepairOrder = "api/repair-order";

    // ===== Material 物料 =====
    public const string PurchaseOrder = "api/purchase-order";
    public const string Subcontract = "api/subcontract";
    public const string Supplier = "api/supplier";

    // ===== Warehouse 仓库 =====
    public const string Warehouse = "api/warehouse";
    public const string Inventory = "api/inventory";
    public const string PendingDelivery = "api/pending-delivery";

    // ===== Other =====
    public const string DataExchange = "api/data-exchange";
    public const string Scan = "api/scan";

    // ===== Scheduling 排程 =====
    public const string SectionParagraphFlowAnalysis = "api/section-paragraph-flow-analysis";
    public const string WorkOrderSchedule = "api/workorder-schedule";
    public const string BatchPlan = "api/batch-plan";
    public const string ColdRollPlan = "api/cold-roll-plan";
    public const string ColdRollSpecSchedule = "api/cold-roll-spec-schedule";
    public const string ColdRollCapacity = "api/cold-roll-capacity";
    public const string ColdRollMachineConfig = "api/cold-roll-machine-config";
    public const string ColdRollMachineGroupConfig = "api/cold-roll-machine-group-config";
    public const string FinalInspectionPlan = "api/final-inspection-plan";
    public const string BatchPlanSchedule = "api/batch-plan-schedule";
    public const string ProductionOverview = "api/production-overview";
    public const string RawMaterialLockPlan = "api/raw-material-lock-plan";

    // ===== User Management =====
    public const string Users = "api/users";

    // ===== Configuration 配置 =====
    public const string ProcessDefinition = "api/process-definition";
    public const string EnumDisplayDefinition = "api/enum-display-definition";
    public const string DictValueDefinition = "api/dict-value-definition";
    public const string StandardWorkDay = "api/standard-work-day";
    public const string StandardWorkDayDeliveryState = "api/standard-work-day-delivery-state";
    public const string ConfigParameter = "api/config-parameter";
    public const string ProcessCardColumnDefinition = "api/process-card-column-definition";
    public const string ProcessCardStyleDefinition = "api/process-card-style-definition";
    public const string CertificatePrintSetting = "api/certificate-print-setting";
    public const string CertificatePrintColumnDefinition = "api/certificate-print-column-definition";
    public const string DailyOutputEstimate = "api/daily-output-estimate";
    public const string DailyProductionCapacity = "api/daily-production-capacity";
    public const string SectionParagraphConfigSettings = "api/section-paragraph-config-settings";
    public const string Workstation = "api/workstation";
    public const string Employee = "api/employee";

    // ===== Payroll 工资结算 =====
    public const string Attendance = "api/attendance";
    public const string PieceRateProductionCategory = "api/piece-rate-category";
    public const string PieceRateFinalInspectionCategory = "api/piece-rate-final-inspection-category";
    public const string PayrollDailyWage = "api/payroll-daily-wage";
    public const string PayrollCollective = "api/payroll-collective";
    public const string PayrollAttendance = "api/payroll-attendance";
    public const string PayrollMiscWork = "api/payroll-misc-work";
    public const string PayrollAllowance = "api/payroll-allowance";
    public const string PayrollMonthlySummary = "api/payroll-monthly-summary";

    // ===== StandardRegister 标准号 =====
    public const string StandardRegister = "api/standard-register";
    public const string GradeChemicalComposition = "api/grade-chemical-composition";
    public const string GradePhysicalProperty = "api/grade-physical-property";
    public const string SubStandardQuickView = "api/sub-standard-quick-view";
    public const string StandardInspectionRequirement = "api/standard-inspection-requirement";
    public const string FactoryInspectionRequirement = "api/factory-inspection-requirement";
    public const string ChemicalComposition = "api/chemical-composition";
    public const string ChemicalValidationRule = "api/chemical-validation-rule";
    public const string GradeMapping = "api/grade-mapping";

    // ===== 通用默认排序字段 =====
    public const string DefaultSortBy = "CreatedTime";
}
