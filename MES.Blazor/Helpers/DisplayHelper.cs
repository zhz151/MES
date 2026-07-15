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
    /// 列表页整型化：将可空 decimal 强制显示为整数（§10.7 支数/米数/重量/批次数）
    /// </summary>
    public static string FormatDecimalAsInt(decimal value) => ((int)value).ToString();

    /// <summary>
    /// 列表页整型化：将可空 decimal? 强制显示为整数
    /// </summary>
    public static string FormatNullableDecimalAsInt(decimal? value) => value.HasValue ? ((int)value.Value).ToString() : "";

    /// <summary>
    /// 格式化可空日期值
    /// </summary>
    public static string FormatNullableDate(DateTime? value) => value?.ToString("yyyy-MM-dd") ?? "";

    // ========== 枚举文本（统一委托给 EnumHelper） ==========

    /// <summary>获取长度状态中文文本</summary>
    public static string GetLengthStatusText(LengthStatus status) => EnumHelper.GetDisplayName(status);

    /// <summary>获取长度状态中文文本（字符串版本）</summary>
    public static string GetLengthStatusText(string? lengthStatus) => EnumHelper.GetDisplayName<LengthStatus>(lengthStatus);

    /// <summary>获取交货状态中文文本</summary>
    public static string GetDeliveryStateText(DeliveryState state) => EnumHelper.GetDisplayName(state);

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

    /// <summary>获取制造物品中文文本（字符串版本）</summary>
    public static string GetManufacturingItemText(string? item) => EnumHelper.GetDisplayName<ManufacturingItem>(item);

    /// <summary>获取制造物品中文文本（枚举版本）</summary>
    public static string GetManufacturingItemText(ManufacturingItem item) => EnumHelper.GetDisplayName(item);

    /// <summary>获取成品检验项目中文文本</summary>
    public static string GetInspectionItemText(InspectionItem item) => EnumHelper.GetDisplayName(item);

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
    public static string GetSubcontractProcessStatusText(SubcontractProcessStatus status) => EnumHelper.GetDisplayName(status);

    /// <summary>获取委外加工单状态中文文本</summary>
    public static string GetSubcontractOrderStatusText(SubcontractOrderStatus status) => EnumHelper.GetDisplayName(status);

    /// <summary>获取出库类型中文文本（枚举版本）</summary>
    public static string GetOutboundTypeText(OutboundType type) => EnumHelper.GetDisplayName(type);

    /// <summary>获取出库类型中文文本（字符串版本）</summary>
    public static string GetOutboundTypeText(string? type) => EnumHelper.GetDisplayName<OutboundType>(type);

    /// <summary>获取用料计划状态中文文本</summary>
    public static string GetMaterialPlanStatusText(MaterialPlanStatus status) => EnumHelper.GetDisplayName(status);

    /// <summary>获取原料类型中文文本（枚举版本）</summary>
    public static string GetRawMaterialTypeText(RawMaterialType type) => EnumHelper.GetDisplayName(type);

    /// <summary>获取原料类型中文文本（字符串版本）</summary>
    public static string GetRawMaterialTypeText(string? type) => EnumHelper.GetDisplayName<RawMaterialType>(type);

    /// <summary>获取要求类型中文文本</summary>
    public static string GetRequirementTypeText(RequirementType type) => EnumHelper.GetDisplayName(type);

    /// <summary>获取库存计划状态中文文本</summary>
    public static string GetInventoryPlanStatusText(InventoryPlanStatus status) => EnumHelper.GetDisplayName(status);

    /// <summary>获取改制类型中文文本</summary>
    public static string GetReworkTypeText(ReworkType type) => EnumHelper.GetDisplayName(type);

    /// <summary>获取成品类型中文文本</summary>
    public static string GetFinishedProductTypeText(FinishedProductType type) => EnumHelper.GetDisplayName(type);

    /// <summary>获取物料类别中文文本（枚举版本）</summary>
    public static string GetMaterialCategoryText(MaterialCategory category) => EnumHelper.GetDisplayName(category);

    /// <summary>获取物料类别中文文本（字符串版本）</summary>
    public static string GetMaterialCategoryText(string? category) => EnumHelper.GetDisplayName<MaterialCategory>(category);

    /// <summary>获取客户状态中文文本</summary>
    public static string GetCustomerStatusText(CustomerStatus status) => EnumHelper.GetDisplayName(status);

    /// <summary>获取通知类型中文文本</summary>
    public static string GetNotificationTypeText(NotificationType type) => EnumHelper.GetDisplayName(type);

    /// <summary>获取通知变更类型中文文本</summary>
    public static string GetNotificationChangeTypeText(NotificationChangeType type) => EnumHelper.GetDisplayName(type);

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
    public static string GetPipeCategoryText(PipeCategory category) => EnumHelper.GetDisplayName(category);

    /// <summary>获取工段状态中文文本</summary>
    public static string GetSectionStatusText(SectionStatus status) => EnumHelper.GetDisplayName(status);

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
    public static string GetInboundSourceText(string? inboundSource)
    {
        return inboundSource switch
        {
            "Purchase" => "外购",
            "Subcontract" => "委外",
            "ReturnIn" => "退货入库",
            "ProductionInbound" => "生产入库",
            "InspectionInbound" => "检验入库",
            "TransferIn" => "移库入库",
            "Other" => "其它",
            _ => inboundSource ?? ""
        };
    }

    /// <summary>
    /// 获取技术要求中文文本（数据库字符串字段）
    /// </summary>
    public static string GetTechnicalRequirementsText(string? technicalRequirements)
    {
        return technicalRequirements switch
        {
            "Normal" => "普通",
            "Special" => "特殊",
            _ => technicalRequirements ?? ""
        };
    }

    /// <summary>
    /// 获取技术要求中文文本（RequirementType 枚举）
    /// </summary>
    public static string GetTechnicalRequirementsText(RequirementType technicalRequirements)
    {
        return technicalRequirements switch
        {
            RequirementType.Normal => "普通",
            RequirementType.Special => "特殊",
            _ => technicalRequirements.ToString()
        };
    }

    /// <summary>
    /// 获取有效流转状态中文文本（int 字段）
    /// </summary>
    public static string GetFlowStatusText(int status)
    {
        return status switch
        {
            0 => "未投料",
            1 => "部分",
            2 => "满足",
            _ => "未知"
        };
    }

    /// <summary>
    /// 获取有效主号状态中文文本（int 字段）
    /// </summary>
    public static string GetMainNoFlowStatusText(int status)
    {
        return status switch
        {
            0 => "未计划",
            1 => "部分",
            2 => "满足",
            _ => "未知"
        };
    }

    /// <summary>
    /// 获取产品检验类型中文文本（数据库字符串字段）
    /// </summary>
    public static string GetProductInspectionTypeText(string? type)
    {
        return type switch
        {
            "Critical" => "回厂复检",
            "Order" => "不需再检验",
            _ => type ?? ""
        };
    }

    /// <summary>冷轧完工要求中文显示（数据库字符串字段）</summary>
    public static string GetCompletionTypeText(string? ct) => ct switch
    {
        "All" => "全量",
        "Urgent" or "Partial1" => "特急单",
        "Partial2" => "急单",
        "Partial3" => "含B顺",
        _ => "",
    };

    /// <summary>冷轧排程类型中文显示（数据库字符串字段）</summary>
    public static string GetRollTypeText(string? rollType) => rollType switch
    {
        "All" or "Subsequent" => "全量",
        "Urgent" or "Partial1" => "特急单",
        "Partial2" => "急单",
        "Partial3" => "含B顺",
        _ => "",
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
            "Completed" => Color.Success,
            "Suspended" => Color.Warning,
            "Cancelled" => Color.Error,
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
            BatchStatus.Completed => Color.Success,
            BatchStatus.Suspended => Color.Warning,
            BatchStatus.Cancelled => Color.Error,
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
            _ => Color.Default
        };
    }

    // ========== 筛选选项辅助（从 EnumHelper 生成） ==========

    /// <summary>
    /// 从 EnumHelper 生成列筛选下拉选项列表，确保筛选文本与显示文本一致
    /// </summary>
    public static List<EnumOption> GetEnumFilterOptions<T>() where T : struct, Enum
        => Enum.GetValues<T>()
               .Select(v => new EnumOption(v.ToString(), EnumHelper.GetDisplayName(v)))
               .ToList();
}
