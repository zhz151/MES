using MES.Core.Constants;
using MES.Core.Enums;
using MES.Core.Helpers;
using MES.Blazor.Services;
using MudBlazor;

namespace MES.Blazor.Helpers;

/// <summary>
/// 显示帮助类，提供格式化、颜色映射等通用方法
/// 枚举中文显示统一委托给 EnumHelper（单一事实源）
/// </summary>
public static class DisplayHelper
{
    /// <summary>
    /// 格式化 decimal 值，去除末尾无效零
    /// </summary>
    public static string FormatDecimal(decimal value) => value.ToString("G29");

    /// <summary>
    /// 格式化可空 decimal 值，去除末尾无效零
    /// </summary>
    public static string FormatNullableDecimal(decimal? value) => value?.ToString("G29") ?? "";

    /// <summary>
    /// 格式化规格（外径*壁厚），去除数值末尾无效零
    /// </summary>
    public static string FormatSpecification(string? specification)
    {
        if (string.IsNullOrEmpty(specification)) return "";
        var parts = specification.Split('*');
        if (parts.Length != 2) return specification;
        var od = decimal.TryParse(parts[0], out var odValue) ? odValue.ToString("G29") : parts[0];
        var wt = decimal.TryParse(parts[1], out var wtValue) ? wtValue.ToString("G29") : parts[1];
        return $"{od}*{wt}";
    }

    /// <summary>
    /// 格式化可空 int 值
    /// </summary>
    public static string FormatNullableInt(int? value) => value?.ToString() ?? "";

    /// <summary>
    /// 格式化可空 int 值：null 或 0 显示空（业务量字段 0 与未填等价，如合格量/缺陷量/加工量）
    /// </summary>
    public static string FormatNullableIntZeroAsEmpty(int? value) => value is > 0 ? value.Value.ToString() : "";

    /// <summary>
    /// 格式化可空 decimal 值：null 或 0 显示空
    /// </summary>
    public static string FormatNullableDecimalZeroAsEmpty(decimal? value) => value is > 0 ? value.Value.ToString("G29") : "";

    /// <summary>
    /// 列表页整型化：将可空 decimal 强制显示为整数（§10.7 支数/米数/重量/批次数）
    /// </summary>
    public static string FormatDecimalAsInt(decimal value) => ((int)value).ToString();

    /// <summary>
    /// 列表页整型化：将可空 decimal? 强制显示为整数
    /// </summary>
    public static string FormatNullableDecimalAsInt(decimal? value) => value.HasValue ? ((int)value.Value).ToString() : "";

    /// <summary>
    /// 列表页整型化：将可空 decimal? 强制显示为整数，null 或 0 显示空
    /// </summary>
    public static string FormatNullableDecimalAsIntZeroAsEmpty(decimal? value) => value is > 0 ? ((int)value.Value).ToString() : "";

    /// <summary>
    /// 格式化可空日期值
    /// </summary>
    public static string FormatNullableDate(DateTime? value) => value?.ToString("yyyy-MM-dd") ?? "";

    // ========== 枚举文本（统一委托给 EnumHelper） ==========

    /// <summary>获取长度状态中文文本</summary>
    public static string GetLengthStatusText(LengthStatus status) => EnumHelper.GetDisplayName(status);

    /// <summary>获取长度状态中文文本（字符串版本）</summary>
    public static string GetLengthStatusText(string? lengthStatus) => EnumHelper.GetDisplayName<LengthStatus>(lengthStatus);

    /// <summary>
    /// 获取工单长度状态中文文本（定尺仅对"多种"情形附加标记，仅工单维度页面使用）
    /// Fixed 且 最小长度≠最大长度 → "定尺（多）"；其余定尺（单种/长度缺失）→ "定尺"
    /// </summary>
    public static string GetWorkOrderLengthStatusText(LengthStatus status, decimal? minLength, decimal? maxLength)
    {
        if (status == LengthStatus.Fixed)
        {
            if (minLength.HasValue && maxLength.HasValue && minLength.Value != maxLength.Value)
                return "定尺（多）";
            return "定尺";
        }
        return GetLengthStatusText(status);
    }

    /// <summary>获取交货状态中文文本</summary>
    public static string GetDeliveryStateText(DeliveryState state) => EnumHelper.GetDisplayName(state);

    /// <summary>获取交货状态中文文本（可空版本）</summary>
    public static string GetDeliveryStateText(DeliveryState? state) => state.HasValue ? EnumHelper.GetDisplayName(state.Value) : "-";

    /// <summary>获取交货状态中文文本（字符串版本）</summary>
    public static string GetDeliveryStateText(string? deliveryState) => EnumHelper.GetDisplayName<DeliveryState>(deliveryState);

    /// <summary>获取钢管制造类别中文文本</summary>
    public static string GetPipeManufacturingTypeText(PipeManufacturingType type) => EnumHelper.GetDisplayName(type);

    /// <summary>获取钢管制造类别中文文本（字符串版本）</summary>
    public static string GetPipeManufacturingTypeText(string? type) => EnumHelper.GetDisplayName<PipeManufacturingType>(type);

    /// <summary>获取结算方式中文文本</summary>
    public static string GetSettlementMethodText(SettlementMethod method) => EnumHelper.GetDisplayName(method);

    /// <summary>获取结算方式中文文本（字符串版本）</summary>
    public static string GetSettlementMethodText(string? method) => EnumHelper.GetDisplayName<SettlementMethod>(method);

    /// <summary>获取订单状态中文文本</summary>
    public static string GetSalesOrderStatusText(SalesOrderStatus status) => EnumHelper.GetDisplayName(status);

    /// <summary>获取采购订单状态中文文本</summary>
    public static string GetPurchaseOrderStatusText(PurchaseOrderStatus status) => EnumHelper.GetDisplayName(status);

    /// <summary>获取工单状态中文文本</summary>
    public static string GetWorkOrderStatusText(WorkOrderStatus status) => EnumHelper.GetDisplayName(status);

    /// <summary>获取批次状态中文文本（字符串版本）</summary>
    public static string GetBatchStatusText(string status) => EnumHelper.GetDisplayName<BatchStatus>(status);

    /// <summary>获取批次状态中文文本（枚举版本）</summary>
    public static string GetBatchStatusText(BatchStatus status) => EnumHelper.GetDisplayName(status);

    /// <summary>获取工段委外状态中文文本（字符串版本）</summary>
    public static string GetSectionOutsourceStatusText(string status) => EnumHelper.GetDisplayName<SectionOutsourceStatus>(status);

    /// <summary>获取工段委外状态中文文本（枚举版本）</summary>
    public static string GetSectionOutsourceStatusText(SectionOutsourceStatus status) => EnumHelper.GetDisplayName(status);

    /// <summary>获取生产类型中文文本（字符串版本）</summary>
    public static string GetProductionTypeText(string? productionType) => EnumHelper.GetDisplayName<ProductionType>(productionType);

    /// <summary>获取生产类型中文文本（枚举版本）</summary>
    public static string GetProductionTypeText(ProductionType productionType) => EnumHelper.GetDisplayName(productionType);

    /// <summary>获取成品检验项目中文文本</summary>
    public static string GetInspectionItemText(InspectionItem item) => EnumHelper.GetDisplayName(item);

    /// <summary>获取成检类型中文文本</summary>
    public static string GetInspectionTypeText(InspectionType? type) => type.HasValue ? EnumHelper.GetDisplayName(type.Value) : "-";

    /// <summary>获取成检类型中文文本（字符串版本）</summary>
    public static string GetInspectionTypeText(string? type) => EnumHelper.GetDisplayName<InspectionType>(type);

    /// <summary>获取设备生命周期状态中文文本（字符串版本）</summary>
    public static string GetLifecycleStatusText(string? status) => EnumHelper.GetDisplayName<LifecycleStatus>(status);

    /// <summary>获取设备生命周期状态中文文本（枚举版本）</summary>
    public static string GetLifecycleStatusText(LifecycleStatus status) => EnumHelper.GetDisplayName(status);

    /// <summary>获取设备作用类型中文文本（字符串版本）</summary>
    public static string GetUsageTypeText(string? usageType) => EnumHelper.GetDisplayName<UsageType>(usageType);

    /// <summary>获取设备作用类型中文文本（枚举版本）</summary>
    public static string GetUsageTypeText(UsageType usageType) => EnumHelper.GetDisplayName(usageType);

    /// <summary>获取设备运行状态中文文本（字符串版本）</summary>
    public static string GetRunningStatusText(string? status) => EnumHelper.GetDisplayName<RunningStatus>(status);

    /// <summary>获取设备运行状态中文文本（枚举版本）</summary>
    public static string GetRunningStatusText(RunningStatus status) => EnumHelper.GetDisplayName(status);

    /// <summary>获取维修优先级中文文本（字符串版本）</summary>
    public static string GetRepairPriorityText(string? priority) => EnumHelper.GetDisplayName<RepairPriority>(priority);

    /// <summary>获取维修优先级中文文本（枚举版本）</summary>
    public static string GetRepairPriorityText(RepairPriority priority) => EnumHelper.GetDisplayName(priority);

    /// <summary>获取设备任务状态中文文本（字符串版本）</summary>
    public static string GetEquipmentTaskStatusText(string? status) => EnumHelper.GetDisplayName<EquipmentTaskStatus>(status);

    /// <summary>获取设备任务状态中文文本（枚举版本）</summary>
    public static string GetEquipmentTaskStatusText(EquipmentTaskStatus status) => EnumHelper.GetDisplayName(status);

    /// <summary>获取维修工单状态中文文本（字符串版本）</summary>
    public static string GetRepairOrderStatusText(string? status) => EnumHelper.GetDisplayName<RepairOrderStatus>(status);

    /// <summary>获取维修工单状态中文文本（枚举版本）</summary>
    public static string GetRepairOrderStatusText(RepairOrderStatus status) => EnumHelper.GetDisplayName(status);

    /// <summary>获取优先级别中文文本（字符串版本）</summary>
    public static string GetPriorityText(string? priority) => EnumHelper.GetDisplayName<RepairPriority>(priority);

    /// <summary>获取优先级别中文文本（枚举版本）</summary>
    public static string GetPriorityText(RepairPriority priority) => EnumHelper.GetDisplayName(priority);

    /// <summary>获取保养/点检任务状态中文文本（字符串版本）</summary>
    public static string GetTaskOrderStatusText(string? status) => EnumHelper.GetDisplayName<TaskOrderStatus>(status);

    /// <summary>获取保养/点检任务状态中文文本（枚举版本）</summary>
    public static string GetTaskOrderStatusText(TaskOrderStatus status) => EnumHelper.GetDisplayName(status);

    /// <summary>获取委外加工明细状态中文文本</summary>
    public static string GetSubcontractProcessStatusText(SubcontractOrderStatus status) => EnumHelper.GetDisplayName(status);

    /// <summary>获取委外加工单状态中文文本</summary>
    public static string GetSubcontractOrderStatusText(SubcontractOrderStatus status) => EnumHelper.GetDisplayName(status);

    /// <summary>获取出库类型中文文本（枚举版本）</summary>
    public static string GetOutboundTypeText(OutboundType type) => EnumHelper.GetDisplayName(type);

    /// <summary>获取出库类型中文文本（字符串版本）</summary>
    public static string GetOutboundTypeText(string? type) => EnumHelper.GetDisplayName<OutboundType>(type);

    /// <summary>获取用料计划状态中文文本</summary>
    public static string GetMaterialPlanStatusText(MaterialPlanStatus status) => EnumHelper.GetDisplayName(status);

    /// <summary>获取物料类型中文文本</summary>
    public static string GetMaterialTypeText(MaterialType type) => EnumHelper.GetDisplayName(type);

    /// <summary>获取物料类型中文文本（可空枚举版本）</summary>
    public static string GetMaterialTypeText(MaterialType? type) => type.HasValue ? EnumHelper.GetDisplayName(type.Value) : "-";

    /// <summary>获取物料类型中文文本（字符串版本）</summary>
    public static string GetMaterialTypeText(string? type) => EnumHelper.GetDisplayName<MaterialType>(type);

    /// <summary>获取要求类型中文文本</summary>
    public static string GetRequirementTypeText(RequirementType type) => EnumHelper.GetDisplayName(type);

    /// <summary>获取库存计划状态中文文本</summary>
    public static string GetInventoryPlanStatusText(InventoryPlanStatus status) => EnumHelper.GetDisplayName(status);

    /// <summary>获取改制类型中文文本</summary>
    public static string GetReworkTypeText(ReworkType type) => EnumHelper.GetDisplayName(type);

    /// <summary>获取成品类型中文文本</summary>
    public static string GetFinishedProductTypeText(FinishedProductType type) => EnumHelper.GetDisplayName(type);

    /// <summary>获取客户状态中文文本</summary>
    public static string GetCustomerStatusText(CustomerStatus status) => EnumHelper.GetDisplayName(status);

    /// <summary>获取通知类型中文文本</summary>
    public static string GetNotificationTypeText(NotificationType type) => EnumHelper.GetDisplayName(type);

    /// <summary>获取处理方式中文文本</summary>
    public static string GetDisposalMethodText(DisposalMethod method) => EnumHelper.GetDisplayName(method);

    /// <summary>获取NCR状态中文文本</summary>
    public static string GetNcrStatusText(NcrStatus status) => EnumHelper.GetDisplayName(status);

    /// <summary>获取酸洗状态中文文本</summary>
    public static string GetPicklingStatusText(PicklingStatus status) => EnumHelper.GetDisplayName(status);

    /// <summary>获取责任类别中文文本</summary>
    public static string GetResponsibilityCategoryText(ResponsibilityCategory category) => EnumHelper.GetDisplayName(category);

    /// <summary>获取严重级别中文文本</summary>
    public static string GetSeverityLevelText(SeverityLevel level) => EnumHelper.GetDisplayName(level);

    /// <summary>获取验证结果中文文本</summary>
    public static string GetVerifyResultText(VerifyResult result) => EnumHelper.GetDisplayName(result);

    /// <summary>获取管类类别中文文本</summary>
    public static string GetPipeCategoryText(MaterialType category) => EnumHelper.GetDisplayName(category);

    /// <summary>获取工段状态中文文本</summary>
    public static string GetSectionStatusText(SectionStatus status) => EnumHelper.GetDisplayName(status);

    /// <summary>获取投料类型中文文本</summary>
    public static string GetInputTypeText(BatchInputType type) => EnumHelper.GetDisplayName(type);

    // ========== 非枚举文本（保持独立映射） ==========

    /// <summary>
    /// 获取布尔值中文显示
    /// </summary>
    public static string GetYesNoText(bool value) => value ? "是" : "否";

    /// <summary>
    /// 获取工段完工状态中文文本
    /// </summary>
    public static string GetSectionCompletedText(bool? completed) => completed switch
    {
        true => "完工",
        false => "生产中",
        null => ""
    };

    /// <summary>
    /// 获取入库来源中文文本（数据库字符串字段）
    /// </summary>
    public static string GetInboundSourceText(string? inboundSource) => EnumHelper.GetDisplayName<InboundSource>(inboundSource);

    /// <summary>
    /// 获取入库来源中文文本（枚举版本）
    /// </summary>
    public static string GetInboundSourceText(InboundSource inboundSource) => EnumHelper.GetDisplayName(inboundSource);

    /// <summary>获取入库来源中文文本（可空枚举版本）</summary>
    public static string GetInboundSourceText(InboundSource? inboundSource) => inboundSource.HasValue ? EnumHelper.GetDisplayName(inboundSource.Value) : "-";

    /// <summary>
    /// 获取技术要求中文文本（数据库字符串字段）
    /// </summary>
    public static string GetTechnicalRequirementsText(string? technicalRequirements)
        => EnumHelper.GetDisplayName<RequirementType>(technicalRequirements);

    /// <summary>
    /// 获取技术要求中文文本（RequirementType 枚举）
    /// </summary>
    public static string GetTechnicalRequirementsText(RequirementType technicalRequirements)
        => EnumHelper.GetDisplayName(technicalRequirements);

    /// <summary>
    /// 获取有效流转状态中文文本（int 字段）
    /// </summary>
    public static string GetFlowStatusText(int status) => IntStatusDisplayHelper.GetInputStatusText(status);

    /// <summary>
    /// 获取有效主号状态中文文本（int 字段）
    /// </summary>
    public static string GetMainNoFlowStatusText(int status) => IntStatusDisplayHelper.GetMainNoFlowStatusText(status);

    /// <summary>
    /// 获取排程关注阶段中文文本（int 字段，主号关注 5 档：0=主号暂停 1=主号完成 2=原料锁定 3=生产执行 4=成品检验）
    /// </summary>
    public static string GetScheduleStageText(int stage) => IntStatusDisplayHelper.GetScheduleStageText(stage);

    /// <summary>
    /// 获取排程计划覆盖档位中文文本（WorkOrderPlan.ScheduleStage，4 档：0=主号完成 1=原料锁定 2=生产执行 3=成品检验）
    /// </summary>
    public static string GetPlanScheduleStageText(int stage) => IntStatusDisplayHelper.GetPlanScheduleStageText(stage);

    /// <summary>冷轧完工要求中文显示（数据库字符串字段，Model B 层级档位，急+ > 急 > 急-）</summary>
    public static string GetCompletionTypeText(string? ct) => ct switch
    {
        "CrOnly" => "急+",
        "Urgent" or "Partial1" => "急+/急",
        "Partial2" => "急+/急/急-",
        "Partial3" => "急+/急/急-/顺",
        "All" => "全量",
        _ => "",
    };

    /// <summary>冷轧排程类型中文显示（数据库字符串字段，Model B 层级档位，急+ > 急 > 急-）</summary>
    public static string GetRollTypeText(string? rollType) => rollType switch
    {
        "CrOnly" => "急+",
        "Urgent" or "Partial1" => "急+/急",
        "Partial2" => "急+/急/急-",
        "Partial3" => "急+/急/急-/顺",
        "All" or "Subsequent" => "全量",
        _ => "",
    };

    // ========== 状态筛选选项与颜色 ==========

    /// <summary>原始投料/有效流转/主号投料状态筛选选项（int 字段）</summary>
    public static List<EnumOption> GetFlowStatusOptions()
        => IntStatusDisplayHelper.GetInputStatusOptions()
                                 .Select(o => new EnumOption(o.Value, o.DisplayName))
                                 .ToList();

    /// <summary>有效主号状态筛选选项（int 字段，0=未计划）</summary>
    public static List<EnumOption> GetMainNoFlowStatusOptions()
        => IntStatusDisplayHelper.GetMainNoFlowStatusOptions()
                                 .Select(o => new EnumOption(o.Value, o.DisplayName))
                                 .ToList();

    /// <summary>排程主号关注筛选选项（summary 5 档）</summary>
    public static List<EnumOption> GetScheduleStageOptions()
        => IntStatusDisplayHelper.GetScheduleStageOptions()
                                 .Select(o => new EnumOption(o.Value, o.DisplayName))
                                 .ToList();

    /// <summary>计划覆盖档位筛选选项（4 档）</summary>
    public static List<EnumOption> GetPlanScheduleStageOptions()
        => IntStatusDisplayHelper.GetPlanScheduleStageOptions()
                                 .Select(o => new EnumOption(o.Value, o.DisplayName))
                                 .ToList();

    /// <summary>入库状态筛选选项（3 档：工单/订单级）</summary>
    public static List<EnumOption> GetWarehousingStatusOptions()
        => IntStatusDisplayHelper.GetWarehousingStatusOptions()
                                 .Select(o => new EnumOption(o.Value, o.DisplayName))
                                 .ToList();

    /// <summary>主号入库状态筛选选项（4 档）</summary>
    public static List<EnumOption> GetMainNoWarehousingStatusOptions()
        => IntStatusDisplayHelper.GetMainNoWarehousingStatusOptions()
                                 .Select(o => new EnumOption(o.Value, o.DisplayName))
                                 .ToList();

    /// <summary>用料计划状态筛选选项（int 字段，MaterialPlanStatus 4 档：0=未计划 1=部分 2=满足 3=超量）</summary>
    public static List<EnumOption> GetMaterialPlanStatusOptions()
        => IntStatusDisplayHelper.GetMaterialPlanStatusOptions()
                                 .Select(o => new EnumOption(o.Value, o.DisplayName))
                                 .ToList();

    /// <summary>用料计划执行状况筛选选项（int 字段，G4~G10 七种用料 5 档：0=无计划 1=未执行 2=部分 3=已完成 4=异常）</summary>
    public static List<EnumOption> GetPlanExecutionStatusOptions()
        => IntStatusDisplayHelper.GetPlanExecutionStatusOptions()
                                 .Select(o => new EnumOption(o.Value, o.DisplayName))
                                 .ToList();

    /// <summary>主号计划执行状态筛选选项（int 字段，4 档：0=无计划 1=未执行 2=执行中 3=计划落实）</summary>
    public static List<EnumOption> GetMainNoPlanExecutionStatusOptions()
        => IntStatusDisplayHelper.GetMainNoPlanExecutionStatusOptions()
                                 .Select(o => new EnumOption(o.Value, o.DisplayName))
                                 .ToList();

    /// <summary>到料实投一致性筛选选项（int 字段，5 档：0=一致 1=待投 2=疑问-到料少投 3=疑问-到料超投 4=错误-无料已投）</summary>
    public static List<EnumOption> GetPlanInputConsistencyOptions()
        => IntStatusDisplayHelper.GetPlanInputConsistencyOptions()
                                 .Select(o => new EnumOption(o.Value, o.DisplayName))
                                 .ToList();

    /// <summary>排程主号关注颜色（summary 5 档）</summary>
    public static Color GetScheduleStageColor(int stage) => stage switch
    {
        0 => Color.Error,       // 主号暂停
        1 => Color.Success,     // 主号完成（闭环）
        2 => Color.Warning,     // 原料锁定（待料）
        3 => Color.Info,        // 生产执行
        4 => Color.Primary,     // 成品检验
        _ => Color.Default
    };

    /// <summary>紧急性颜色（字典 UrgencyLevelKey，全系统统一出口）</summary>
    public static Color GetUrgencyColor(string? urgency) => urgency switch
    {
        UrgencyLevelKeys.APlusUrgent => Color.Error,     // A+急
        UrgencyLevelKeys.AUrgent => Color.Warning,       // A急
        UrgencyLevelKeys.BOrder => Color.Info,           // B顺
        _ => Color.Default                               // C缓 / D缓 / E停 / 空
    };

    /// <summary>投料状态颜色（int 字段 4 档：0=未投料 1=部分 2=满足 3=超量，超量视为满足）</summary>
    public static Color GetInputStatusColor(int status) => status switch
    {
        0 => Color.Default,
        1 => Color.Warning,
        2 or 3 => Color.Success,
        _ => Color.Default
    };

    /// <summary>冷轧完工要求筛选选项（Model B 层级档位，急+ > 急 > 急-）</summary>
    public static List<EnumOption> GetCompletionTypeOptions() => new()
    {
        new("CrOnly", "急+"), new("Urgent", "急+/急"), new("Partial2", "急+/急/急-"),
        new("Partial3", "急+/急/急-/顺"), new("All", "全量"), new("None", "-")
    };

    /// <summary>冷轧排程类型筛选选项（Model B 层级档位，急+ > 急 > 急-）</summary>
    public static List<EnumOption> GetRollTypeOptions() => new()
    {
        new("CrOnly", "急+"), new("Urgent", "急+/急"), new("Partial2", "急+/急/急-"),
        new("Partial3", "急+/急/急-/顺"), new("All", "全量"), new("None", "-")
    };

    /// <summary>排程档位筛选选项（批次实际档位，V5.26 档位序 急+ &gt; 急 &gt; 急- &gt; 顺 &gt; 带 &gt; 略，对应 ScheduleTierDisplay）</summary>
    public static List<EnumOption> GetScheduleTierOptions() => new()
    {
        new("急+", "急+"), new("急", "急"), new("急-", "急-"),
        new("顺", "顺"), new("带", "带"), new("略", "略")
    };

    /// <summary>批次计划薄表等级筛选选项（档位序：急+/急/急-/一般/略，对应 PlanFlowLevelDisplay 五档）</summary>
    public static List<EnumOption> GetPlanFlowLevelOptions() => new()
    {
        new("急+", "急+"), new("急", "急"), new("急-", "急-"),
        new("一般", "一般"), new("略", "略")
    };

    // ========== 公差格式化 ==========

    /// <summary>
    /// 格式化公差显示（例：-0.5/+0.5）
    /// </summary>
    public static string FormatTolerance(decimal negative, decimal positive)
    {
        return $"-{negative.ToString("G29")}/+{positive.ToString("G29")}";
    }

    // ========== 颜色映射 ==========

    /// <summary>获取订单状态颜色</summary>
    public static Color GetSalesOrderStatusColor(SalesOrderStatus status)
    {
        return status switch
        {
            SalesOrderStatus.Pending => Color.Warning,
            SalesOrderStatus.Confirmed => Color.Success,
            _ => Color.Default
        };
    }

    /// <summary>获取采购订单状态颜色</summary>
    public static Color GetPurchaseOrderStatusColor(PurchaseOrderStatus status)
    {
        return status switch
        {
            PurchaseOrderStatus.Open => Color.Info,
            PurchaseOrderStatus.Partial => Color.Warning,
            PurchaseOrderStatus.Completed => Color.Success,
            PurchaseOrderStatus.OverReceived => Color.Error,
            _ => Color.Default
        };
    }

    /// <summary>获取工单状态对应的颜色</summary>
    public static Color GetWorkOrderStatusColor(WorkOrderStatus status)
    {
        return status switch
        {
            WorkOrderStatus.NotGenerated => Color.Default,
            WorkOrderStatus.Confirmed => Color.Success,
            WorkOrderStatus.Pending => Color.Warning,
            _ => Color.Default
        };
    }

    /// <summary>获取批次状态对应的颜色（字符串版本）</summary>
    public static Color GetBatchStatusColor(string status)
    {
        return status switch
        {
            "None" => Color.Default,
            "InProgress" => Color.Info,
            "InFinalInspection" => Color.Warning,
            "Completed" => Color.Success,
            "Suspended" => Color.Warning,
            _ => Color.Default
        };
    }

    /// <summary>获取批次状态对应的颜色（枚举版本）</summary>
    public static Color GetBatchStatusColor(BatchStatus status)
    {
        return status switch
        {
            BatchStatus.None => Color.Default,
            BatchStatus.InProgress => Color.Info,
            BatchStatus.InFinalInspection => Color.Warning,
            BatchStatus.Completed => Color.Success,
            BatchStatus.Suspended => Color.Warning,
            _ => Color.Default
        };
    }

    /// <summary>获取工段委外状态颜色（字符串版本）</summary>
    public static Color GetSectionOutsourceStatusColor(string status)
    {
        return status switch
        {
            "PendingRecovery" => Color.Warning,
            "Recovered" => Color.Success,
            "InProgress" => Color.Info,
            "Virtual" => Color.Default,
            _ => Color.Default
        };
    }

    /// <summary>获取工段委外状态颜色（枚举版本）</summary>
    public static Color GetSectionOutsourceStatusColor(SectionOutsourceStatus status)
    {
        return status switch
        {
            SectionOutsourceStatus.PendingRecovery => Color.Warning,
            SectionOutsourceStatus.Recovered => Color.Success,
            SectionOutsourceStatus.InProgress => Color.Info,
            SectionOutsourceStatus.Virtual => Color.Default,
            _ => Color.Default
        };
    }

    /// <summary>获取设备生命周期状态颜色（字符串版本）</summary>
    public static Color GetLifecycleStatusColor(string? status)
    {
        return status switch
        {
            "Active" => Color.Success,
            "Standby" => Color.Warning,
            "Scrapped" => Color.Error,
            _ => Color.Default
        };
    }

    /// <summary>获取设备生命周期状态颜色（枚举版本）</summary>
    public static Color GetLifecycleStatusColor(LifecycleStatus status)
    {
        return status switch
        {
            LifecycleStatus.Active => Color.Success,
            LifecycleStatus.Standby => Color.Warning,
            LifecycleStatus.Scrapped => Color.Error,
            _ => Color.Default
        };
    }

    /// <summary>获取设备运行状态颜色（字符串版本）</summary>
    public static Color GetRunningStatusColor(string? status)
    {
        return status switch
        {
            "Normal" => Color.Success,
            "Pending" => Color.Warning,
            "InProgress" => Color.Info,
            _ => Color.Default
        };
    }

    /// <summary>获取设备运行状态颜色（枚举版本）</summary>
    public static Color GetRunningStatusColor(RunningStatus status)
    {
        return status switch
        {
            RunningStatus.Normal => Color.Success,
            RunningStatus.Pending => Color.Warning,
            RunningStatus.InProgress => Color.Info,
            _ => Color.Default
        };
    }

    /// <summary>获取设备任务状态颜色（字符串版本）</summary>
    public static Color GetEquipmentTaskStatusColor(string? status)
    {
        return status switch
        {
            "NotApplicable" => Color.Default,
            "Pending" => Color.Warning,
            "Normal" => Color.Success,
            "Overdue" => Color.Error,
            _ => Color.Default
        };
    }

    /// <summary>获取设备任务状态颜色（枚举版本）</summary>
    public static Color GetEquipmentTaskStatusColor(EquipmentTaskStatus status)
    {
        return status switch
        {
            EquipmentTaskStatus.NotApplicable => Color.Default,
            EquipmentTaskStatus.Pending => Color.Warning,
            EquipmentTaskStatus.Normal => Color.Success,
            EquipmentTaskStatus.Overdue => Color.Error,
            _ => Color.Default
        };
    }

    /// <summary>获取维修工单状态颜色（字符串版本）</summary>
    public static Color GetRepairOrderStatusColor(string? status)
    {
        return status switch
        {
            "Pending" => Color.Warning,
            "InProgress" => Color.Info,
            "Completed" => Color.Success,
            _ => Color.Default
        };
    }

    /// <summary>获取维修工单状态颜色（枚举版本）</summary>
    public static Color GetRepairOrderStatusColor(RepairOrderStatus status)
    {
        return status switch
        {
            RepairOrderStatus.Pending => Color.Warning,
            RepairOrderStatus.InProgress => Color.Info,
            RepairOrderStatus.Completed => Color.Success,
            _ => Color.Default
        };
    }

    /// <summary>获取维修类别颜色</summary>
    public static Color GetRepairCategoryColor(string? category)
    {
        return category switch
        {
            "换模" => Color.Primary,
            "外协维修" => Color.Warning,
            _ => Color.Default
        };
    }

    /// <summary>获取优先级别颜色（字符串版本）</summary>
    public static Color GetPriorityColor(string? priority)
    {
        return priority switch
        {
            "Emergency" => Color.Error,
            "Urgent" => Color.Warning,
            "Normal" => Color.Default,
            _ => Color.Default
        };
    }

    /// <summary>获取优先级别颜色（枚举版本）</summary>
    public static Color GetPriorityColor(RepairPriority priority)
    {
        return priority switch
        {
            RepairPriority.Emergency => Color.Error,
            RepairPriority.Urgent => Color.Warning,
            RepairPriority.Normal => Color.Default,
            _ => Color.Default
        };
    }

    /// <summary>获取保养/点检任务状态颜色（字符串版本）</summary>
    public static Color GetTaskOrderStatusColor(string? status)
    {
        return status switch
        {
            "Pending" => Color.Warning,
            "Completed" => Color.Success,
            "Overdue" => Color.Error,
            _ => Color.Default
        };
    }

    /// <summary>获取保养/点检任务状态颜色（枚举版本）</summary>
    public static Color GetTaskOrderStatusColor(TaskOrderStatus status)
    {
        return status switch
        {
            TaskOrderStatus.Pending => Color.Warning,
            TaskOrderStatus.Completed => Color.Success,
            TaskOrderStatus.Overdue => Color.Error,
            _ => Color.Default
        };
    }

    /// <summary>获取委外加工单状态颜色</summary>
    public static Color GetSubcontractOrderStatusColor(SubcontractOrderStatus status)
    {
        return status switch
        {
            SubcontractOrderStatus.Sent => Color.Info,
            SubcontractOrderStatus.PartialReturned => Color.Warning,
            SubcontractOrderStatus.Completed => Color.Success,
            SubcontractOrderStatus.OverReceived => Color.Error,
            _ => Color.Default
        };
    }

    // ========== 枚举选项辅助（从 EnumHelper 生成，按 DisplayOrder 排序） ==========

    /// <summary>
    /// 从 EnumHelper 生成列筛选下拉选项列表，确保筛选文本与显示文本一致；
    /// 顺序按配置表 DisplayOrder（未配置则静态注册顺序）
    /// </summary>
    public static List<EnumOption> GetEnumFilterOptions<T>() where T : struct, Enum
        => EnumHelper.GetDisplayOptions<T>()
                     .Select(o => new EnumOption(o.Value, o.DisplayName))
                     .ToList();

    /// <summary>
    /// 表单下拉选项（Value=枚举值名、Display=中文），按 DisplayOrder 排序。
    /// 页面循环：@foreach (var opt in DisplayHelper.GetEnumOptions&lt;MyEnum&gt;())
    /// </summary>
    public static List<EnumOption> GetEnumOptions<T>() where T : struct, Enum
        => EnumHelper.GetDisplayOptions<T>()
                     .Select(o => new EnumOption(o.Value, o.DisplayName))
                     .ToList();

    // ========== string 类型枚举映射（非 C# enum，string 存储） ==========

    /// <summary>
    /// 数据来源类型中文显示：SCAN→扫码, MANUAL→手动（委托 Core 统一出口）
    /// </summary>
    public static string GetDataSourceText(string? dataSource) => StringEnumDisplayHelper.GetDataSourceText(dataSource);

    /// <summary>
    /// 数据来源列筛选下拉选项（SCAN→扫码, MANUAL→手动），统一出口
    /// </summary>
    public static List<EnumOption> GetDataSourceOptions()
        => new() { new("SCAN", "扫码"), new("MANUAL", "手动") };

    // ========== 定尺切割长度匹配标识（CutLengthMatchType） ==========

    /// <summary>
    /// 定尺切割长度匹配标识中文（委托 Core 统一出口）；null（不适用）→ 空串
    /// </summary>
    public static string GetCutLengthMatchText(CutLengthMatchType? match) => CutLengthMatchHelper.GetText(match);

    /// <summary>
    /// 定尺切割长度匹配标识 MudChip 颜色：完全匹配=绿 / 主号匹配=橙 / 不适用=默认
    /// </summary>
    public static Color GetCutLengthMatchColor(CutLengthMatchType? match) => match switch
    {
        CutLengthMatchType.FullMatch => Color.Success,
        CutLengthMatchType.MainNoMatch => Color.Warning,
        _ => Color.Default
    };

    /// <summary>
    /// 定尺切割长度匹配标识列筛选下拉选项（Value=枚举名、Display=中文）
    /// </summary>
    public static List<EnumOption> GetCutLengthMatchOptions()
        => new() { new(nameof(CutLengthMatchType.FullMatch), "完全匹配"), new(nameof(CutLengthMatchType.MainNoMatch), "主号匹配") };

    /// <summary>
    /// 报工模板类型中文显示（字符串版本）
    /// </summary>
    public static string GetReportTypeText(string? reportType) => EnumHelper.GetDisplayName<ReportTemplateType>(reportType);

    /// <summary>
    /// 报工模板类型中文显示（枚举版本）
    /// </summary>
    public static string GetReportTypeText(ReportTemplateType type) => EnumHelper.GetDisplayName(type);

    /// <summary>
    /// 班次中文显示（字符串版本）
    /// </summary>
    public static string GetShiftTypeText(string? shift) => EnumHelper.GetDisplayName<ShiftType>(shift);

    /// <summary>
    /// 班次中文显示（枚举版本）
    /// </summary>
    public static string GetShiftTypeText(ShiftType shift) => EnumHelper.GetDisplayName(shift);

    /// <summary>
    /// 班次中文显示（可空枚举版本）
    /// </summary>
    public static string GetShiftTypeText(ShiftType? shift) => shift.HasValue ? EnumHelper.GetDisplayName(shift.Value) : "";

    /// <summary>
    /// 产类中文显示（数据库字符串字段，存稳定英文 Key）：RoughTube→荒管, InProgress→在制, Finished→成品。
    /// 空值/未知默认显示"在制"（与存量 `?? "在制"` 口径一致）。
    /// </summary>
    public static string GetProductStatusText(string? productStatus)
        => DictValueDisplayHelper.GetText(DictValueDefaults.ProductStatus, productStatus) ?? "在制";

    /// <summary>
    /// 组合归类表产类中文显示：AllStatus（不限定产类）→"全部"；其余走 <see cref="GetProductStatusText"/>。
    /// 组合表产类列使用本方法（"All" 不在字典表，直接走 GetProductStatusText 会错误回退"在制"）。
    /// </summary>
    public static string GetCombinationProductStatusText(string? productStatus)
        => string.Equals(productStatus, ProductStatuses.AllStatus, StringComparison.OrdinalIgnoreCase)
            ? "全部"
            : GetProductStatusText(productStatus);

    /// <summary>产类颜色映射（字符串字段存稳定英文 Key）</summary>
    public static Color GetProductStatusColor(string? productStatus) => productStatus switch
    {
        ProductStatuses.RoughTube => Color.Primary,
        ProductStatuses.Finished => Color.Success,
        _ => Color.Default
    };
}
