using MES.Core.Enums;
using MudBlazor;

namespace MES.Blazor.Helpers;

/// <summary>
/// 显示帮助类，提供格式化、枚举文本转换等通用方法
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

    /// <summary>
    /// 获取长度状态中文文本
    /// </summary>
    public static string GetLengthStatusText(LengthStatus status)
    {
        return status switch
        {
            LengthStatus.Fixed => "定尺",
            LengthStatus.Range => "范围尺",
            LengthStatus.NonFixed => "非定尺",
            _ => status.ToString()
        };
    }

    /// <summary>
    /// 获取交货状态中文文本
    /// </summary>
    public static string GetDeliveryStateText(DeliveryState state)
    {
        return state switch
        {
            DeliveryState.SolutionAnnealedAndPickled => "固溶酸洗",
            DeliveryState.SolutionAnnealedAndPickledUTube => "固溶酸洗-U型管",
            DeliveryState.SolutionAnnealedAndPickledExternalPolished => "固溶酸洗-外抛光",
            DeliveryState.SolutionAnnealedAndPickledInternalPolished => "固溶酸洗-内抛光",
            DeliveryState.SolutionAnnealedAndPickledBothPolished => "固溶酸洗-内外抛光",
            DeliveryState.SolutionAnnealedAndPickledCoiled => "固溶酸洗-盘管",
            DeliveryState.Bright => "光亮",
            DeliveryState.BrightUTube => "光亮-U型管",
            DeliveryState.BrightCoiled => "光亮-盘管",
            DeliveryState.Hard => "硬态",
            _ => state.ToString()
        };
    }

    /// <summary>
    /// 获取交货状态中文文本（字符串版本）
    /// </summary>
    public static string GetDeliveryStateText(string? deliveryState)
    {
        return deliveryState switch
        {
            "SolutionAnnealedAndPickled" => "固溶酸洗",
            "SolutionAnnealedAndPickledUTube" => "固溶酸洗-U型管",
            "SolutionAnnealedAndPickledExternalPolished" => "固溶酸洗-外抛光",
            "SolutionAnnealedAndPickledInternalPolished" => "固溶酸洗-内抛光",
            "SolutionAnnealedAndPickledBothPolished" => "固溶酸洗-内外抛光",
            "SolutionAnnealedAndPickledCoiled" => "固溶酸洗-盘管",
            "Bright" => "光亮",
            "BrightUTube" => "光亮-U型管",
            "BrightCoiled" => "光亮-盘管",
            "Hard" => "硬态",
            _ => deliveryState ?? ""
        };
    }

    /// <summary>
    /// 获取物料名称中文文本
    /// </summary>
    public static string GetMaterialNameText(MaterialName materialName)
    {
        return materialName switch
        {
            MaterialName.SeamlessPipe => "无缝管",
            MaterialName.WeldedPipe => "焊管",
            _ => materialName.ToString()
        };
    }

    /// <summary>
    /// 获取物料名称中文文本（字符串版本）
    /// </summary>
    public static string GetMaterialNameText(string? materialName)
    {
        return materialName switch
        {
            "SeamlessPipe" => "无缝管",
            "WeldedPipe" => "焊管",
            _ => materialName ?? ""
        };
    }

    /// <summary>
    /// 获取结算方式中文文本
    /// </summary>
    public static string GetSettlementMethodText(SettlementMethod method)
    {
        return method switch
        {
            SettlementMethod.Theoretical => "理算",
            SettlementMethod.Weighing => "过磅",
            SettlementMethod.WeighingNegative => "过磅-负",
            _ => method.ToString()
        };
    }

    /// <summary>
    /// 获取结算方式中文文本（字符串版本）
    /// </summary>
    public static string GetSettlementMethodText(string? method)
    {
        return method switch
        {
            "Theoretical" => "理算",
            "Weighing" => "过磅",
            "WeighingNegative" => "过磅-负",
            _ => method ?? ""
        };
    }

    /// <summary>
    /// 格式化公差显示（例：-0.5/+0.5）
    /// </summary>
    public static string FormatTolerance(decimal negative, decimal positive)
    {
        return $"-{negative.ToString("G29")}/+{positive.ToString("G29")}";
    }

    /// <summary>
    /// 获取订单状态中文文本
    /// </summary>
    public static string GetSalesOrderStatusText(SalesOrderStatus status)
    {
        return status switch
        {
            SalesOrderStatus.Pending => "待处理",
            SalesOrderStatus.Confirmed => "已确认",
            _ => "未知"
        };
    }

    /// <summary>
    /// 获取订单状态颜色
    /// </summary>
    public static Color GetSalesOrderStatusColor(SalesOrderStatus status)
    {
        return status switch
        {
            SalesOrderStatus.Pending => Color.Warning,
            SalesOrderStatus.Confirmed => Color.Success,
            _ => Color.Default
        };
    }

    // ========== 采购订单状态 ==========

    /// <summary>
    /// 获取采购订单状态中文文本
    /// </summary>
    public static string GetPurchaseOrderStatusText(PurchaseOrderStatus status)
    {
        return status switch
        {
            PurchaseOrderStatus.Open => "已下单",
            PurchaseOrderStatus.Partial => "部分到货",
            PurchaseOrderStatus.Completed => "已完成",
            _ => "未知"
        };
    }

    /// <summary>
    /// 获取采购订单状态颜色
    /// </summary>
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

    /// <summary>
    /// 获取工单状态对应的颜色
    /// </summary>
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

    /// <summary>
    /// 获取工单状态中文文本
    /// </summary>
    public static string GetWorkOrderStatusText(WorkOrderStatus status)
    {
        return status switch
        {
            WorkOrderStatus.NotGenerated => "未编制",
            WorkOrderStatus.Confirmed => "已确定",
            WorkOrderStatus.Pending => "待修正",
            _ => "未知"
        };
    }

    // ========== 批次状态 ==========

    /// <summary>
    /// 获取批次状态对应的颜色
    /// </summary>
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

    /// <summary>
    /// 获取批次状态中文文本
    /// </summary>
    public static string GetBatchStatusText(string status)
    {
        return status switch
        {
            "None" => "未产",
            "InProgress" => "在产",
            "Completed" => "完成",
            "Suspended" => "挂起",
            "Cancelled" => "作废",
            _ => status
        };
    }

    /// <summary>
    /// 获取批次状态对应的颜色（枚举版本）
    /// </summary>
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

    /// <summary>
    /// 获取批次状态中文文本（枚举版本）
    /// </summary>
    public static string GetBatchStatusText(BatchStatus status)
    {
        return status switch
        {
            BatchStatus.None => "未产",
            BatchStatus.InProgress => "在产",
            BatchStatus.Completed => "完成",
            BatchStatus.Suspended => "挂起",
            BatchStatus.Cancelled => "作废",
            _ => status.ToString()
        };
    }

    // ========== 工段委外状态 ==========

    /// <summary>
    /// 获取工段委外状态颜色
    /// </summary>
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

    /// <summary>
    /// 获取工段委外状态中文文本
    /// </summary>
    public static string GetSectionOutsourceStatusText(string status)
    {
        return status switch
        {
            "PendingRecovery" => "待回收",
            "Recovered" => "已回收",
            "InProgress" => "在轧",
            _ => status
        };
    }

    /// <summary>
    /// 获取工段委外状态颜色（枚举版本）
    /// </summary>
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

    /// <summary>
    /// 获取工段委外状态中文文本（枚举版本）
    /// </summary>
    public static string GetSectionOutsourceStatusText(SectionOutsourceStatus status)
    {
        return status switch
        {
            SectionOutsourceStatus.PendingRecovery => "待回收",
            SectionOutsourceStatus.Recovered => "已回收",
            SectionOutsourceStatus.InProgress => "在轧",
            _ => status.ToString()
        };
    }

    // ========== 生产类型 ==========

    /// <summary>
    /// 获取生产类型中文文本
    /// </summary>
    public static string GetProductionTypeText(string? productionType)
    {
        return productionType switch
        {
            "RoughTube" => "荒管生产",
            "InProcess" => "在制生产",
            "Inventory" => "库存",
            "OutsourcedPurchased" => "外购",
            "Rework" => "返整",
            "Subcontract" => "委外生产",
            "ExternalProcessing" => "对外加工",
            _ => productionType ?? ""
        };
    }

    /// <summary>
    /// 获取生产类型中文文本（枚举版本）
    /// </summary>
    public static string GetProductionTypeText(ProductionType productionType)
    {
        return productionType switch
        {
            ProductionType.RoughTube => "荒管生产",
            ProductionType.InProcess => "在制生产",
            ProductionType.Inventory => "库存",
            ProductionType.OutsourcedPurchased => "外购",
            ProductionType.Rework => "返整",
            ProductionType.Subcontract => "委外生产",
            ProductionType.ExternalProcessing => "对外加工",
            _ => productionType.ToString()
        };
    }

    // ========== 入库来源 ==========

    /// <summary>
    /// 获取入库来源中文文本
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

    // ========== 制造物品 ==========

    /// <summary>
    /// 获取制造物品中文文本
    /// </summary>
    public static string GetManufacturingItemText(string? item)
    {
        return item switch
        {
            "OrderFinishedProduct" => "订单成品",
            "PreparedMaterial" => "备料成品",
            "SurplusStock" => "余库料",
            "IntermediateProduct" => "中间品",
            "SpecialDeliveryStatus" => "特定交态成品",
            _ => item ?? ""
        };
    }

    /// <summary>
    /// 获取制造物品中文文本（枚举版本）
    /// </summary>
    public static string GetManufacturingItemText(ManufacturingItem item)
    {
        return item switch
        {
            ManufacturingItem.OrderFinishedProduct => "订单成品",
            ManufacturingItem.PreparedMaterial => "备料成品",
            ManufacturingItem.SurplusStock => "余库料",
            ManufacturingItem.IntermediateProduct => "中间品",
            ManufacturingItem.SpecialDeliveryStatus => "特定交态成品",
            _ => item.ToString()
        };
    }

    // ========== 长度状态（字符串版本） ==========

    /// <summary>
    /// 获取长度状态中文文本（字符串版本）
    /// </summary>
    public static string GetLengthStatusText(string? lengthStatus)
    {
        return lengthStatus switch
        {
            "Fixed" => "定尺",
            "Range" => "范围尺",
            "NonFixed" => "非定尺",
            _ => lengthStatus ?? ""
        };
    }

    // ========== 成品检验项目 ==========

    /// <summary>
    /// 获取成品检验项目中文文本
    /// </summary>
    public static string GetInspectionItemText(InspectionItem item)
    {
        return item switch
        {
            InspectionItem.PMIInspection => "PMI检验",
            InspectionItem.VisualInspection => "表检",
            InspectionItem.Dimension => "尺寸",
            InspectionItem.Endoscopy => "内窥",
            InspectionItem.HydrostaticPressure => "水压",
            InspectionItem.UnderwaterPneumatic => "水下气压",
            InspectionItem.EddyCurrent => "涡流",
            InspectionItem.Ultrasonic => "超声波",
            InspectionItem.PortColoring => "端口着色",
            _ => item.ToString()
        };
    }

    // ========== 技术要求 ==========

    /// <summary>
    /// 获取技术要求中文文本
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

    // ========== 设备管理枚举 ==========

    /// <summary>
    /// 获取设备生命周期状态中文文本
    /// </summary>
    public static string GetLifecycleStatusText(string? status)
    {
        return status switch
        {
            "Active" => "在用",
            "Standby" => "备用",
            "Scrapped" => "报废",
            _ => status ?? ""
        };
    }

    /// <summary>
    /// 获取设备生命周期状态中文文本（枚举版本）
    /// </summary>
    public static string GetLifecycleStatusText(LifecycleStatus status)
    {
        return status switch
        {
            LifecycleStatus.Active => "在用",
            LifecycleStatus.Standby => "备用",
            LifecycleStatus.Scrapped => "报废",
            _ => status.ToString()
        };
    }

    /// <summary>
    /// 获取设备生命周期状态颜色
    /// </summary>
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

    /// <summary>
    /// 获取设备生命周期状态颜色（枚举版本）
    /// </summary>
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

    /// <summary>
    /// 获取设备作用类型中文文本
    /// </summary>
    public static string GetUsageTypeText(string? usageType)
    {
        return usageType switch
        {
            "Primary" => "主生产",
            "Secondary" => "辅生产",
            "Other" => "其它",
            _ => usageType ?? ""
        };
    }

    /// <summary>
    /// 获取设备作用类型中文文本（枚举版本）
    /// </summary>
    public static string GetUsageTypeText(UsageType usageType)
    {
        return usageType switch
        {
            UsageType.Primary => "主生产",
            UsageType.Secondary => "辅生产",
            UsageType.Other => "其它",
            _ => usageType.ToString()
        };
    }

    /// <summary>
    /// 获取设备运行状态中文文本
    /// </summary>
    public static string GetRunningStatusText(string? status)
    {
        return status switch
        {
            "Normal" => "正常",
            "Pending" => "待维修",
            "InProgress" => "维修中",
            _ => status ?? ""
        };
    }

    /// <summary>
    /// 获取设备运行状态中文文本（枚举版本）
    /// </summary>
    public static string GetRunningStatusText(RunningStatus status)
    {
        return status switch
        {
            RunningStatus.Normal => "正常",
            RunningStatus.Pending => "待维修",
            RunningStatus.InProgress => "维修中",
            _ => status.ToString()
        };
    }

    /// <summary>
    /// 获取设备运行状态颜色
    /// </summary>
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

    /// <summary>
    /// 获取设备运行状态颜色（枚举版本）
    /// </summary>
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

    /// <summary>
    /// 获取设备任务状态（点检/保养状况）中文文本
    /// </summary>
    public static string GetEquipmentTaskStatusText(string? status)
    {
        return status switch
        {
            "NotApplicable" => "不适用",
            "Pending" => "待执行",
            "Normal" => "正常",
            "Overdue" => "逾期",
            _ => status ?? ""
        };
    }

    /// <summary>
    /// 获取设备任务状态颜色
    /// </summary>
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

    /// <summary>
    /// 获取设备任务状态中文文本（枚举版本）
    /// </summary>
    public static string GetEquipmentTaskStatusText(EquipmentTaskStatus status)
    {
        return status switch
        {
            EquipmentTaskStatus.NotApplicable => "不适用",
            EquipmentTaskStatus.Pending => "待执行",
            EquipmentTaskStatus.Normal => "正常",
            EquipmentTaskStatus.Overdue => "逾期",
            _ => status.ToString()
        };
    }

    /// <summary>
    /// 获取设备任务状态颜色（枚举版本）
    /// </summary>
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

    /// <summary>
    /// 获取维修工单状态中文文本
    /// </summary>
    public static string GetRepairOrderStatusText(string? status)
    {
        return status switch
        {
            "Pending" => "待维修",
            "InProgress" => "维修中",
            "Completed" => "完成",
            _ => status ?? ""
        };
    }

    /// <summary>
    /// 获取维修工单状态颜色
    /// </summary>
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

    /// <summary>
    /// 获取维修类别颜色
    /// </summary>
    public static Color GetRepairCategoryColor(string? category)
    {
        return category switch
        {
            "换模" => Color.Primary,
            "外协维修" => Color.Warning,
            _ => Color.Default
        };
    }

    /// <summary>
    /// 获取维修工单状态中文文本（枚举版本）
    /// </summary>
    public static string GetRepairOrderStatusText(RepairOrderStatus status)
    {
        return status switch
        {
            RepairOrderStatus.Pending => "待维修",
            RepairOrderStatus.InProgress => "维修中",
            RepairOrderStatus.Completed => "完成",
            _ => status.ToString()
        };
    }

    /// <summary>
    /// 获取维修工单状态颜色（枚举版本）
    /// </summary>
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

    /// <summary>
    /// 获取优先级别中文文本
    /// </summary>
    public static string GetPriorityText(string? priority)
    {
        return priority switch
        {
            "Normal" => "普通",
            "Urgent" => "紧急",
            "Emergency" => "特急",
            _ => priority ?? ""
        };
    }

    /// <summary>
    /// 获取优先级别颜色
    /// </summary>
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

    /// <summary>
    /// 获取优先级别中文文本（枚举版本）
    /// </summary>
    public static string GetPriorityText(RepairPriority priority)
    {
        return priority switch
        {
            RepairPriority.Normal => "普通",
            RepairPriority.Urgent => "紧急",
            RepairPriority.Emergency => "特急",
            _ => priority.ToString()
        };
    }

    /// <summary>
    /// 获取优先级别颜色（枚举版本）
    /// </summary>
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

    /// <summary>
    /// 获取保养/点检任务状态中文文本
    /// </summary>
    public static string GetTaskOrderStatusText(string? status)
    {
        return status switch
        {
            "Pending" => "待执行",
            "Completed" => "已完成",
            "Overdue" => "已逾期",
            _ => status ?? ""
        };
    }

    /// <summary>
    /// 获取保养/点检任务状态中文文本（枚举版本）
    /// </summary>
    public static string GetTaskOrderStatusText(TaskOrderStatus status)
    {
        return status switch
        {
            TaskOrderStatus.Pending => "待执行",
            TaskOrderStatus.Completed => "已完成",
            TaskOrderStatus.Overdue => "已逾期",
            _ => status.ToString()
        };
    }

    // ========== 布尔值 ==========

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

    // ========== 委外加工单状态 ==========

    /// <summary>
    /// 获取委外加工明细状态中文文本
    /// </summary>
    public static string GetSubcontractProcessStatusText(SubcontractProcessStatus status)
    {
        return status switch
        {
            SubcontractProcessStatus.Pending => "待回收",
            SubcontractProcessStatus.PartialReturned => "部分回收",
            SubcontractProcessStatus.Completed => "已完成",
            _ => "未知"
        };
    }

    /// <summary>
    /// 获取委外加工单状态中文文本
    /// </summary>
    public static string GetSubcontractOrderStatusText(SubcontractOrderStatus status)
    {
        return status switch
        {
            SubcontractOrderStatus.Sent => "已发出",
            SubcontractOrderStatus.PartialReturned => "部分收回",
            SubcontractOrderStatus.Completed => "已完成",
            _ => "未知"
        };
    }

    /// <summary>
    /// 获取委外加工单状态颜色
    /// </summary>
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

    // ========== 出库类型 ==========

    /// <summary>
    /// 获取出库类型中文文本
    /// </summary>
    public static string GetOutboundTypeText(OutboundType type)
    {
        return type switch
        {
            OutboundType.SalesOut => "销售出库",
            OutboundType.SubcontractOut => "委外出库",
            OutboundType.ReturnOut => "退货出库",
            OutboundType.ScrapOut => "报废出库",
            OutboundType.ProductionPick => "生产领用",
            OutboundType.InspectionPick => "检验领用",
            OutboundType.TransferOut => "移库出库",
            OutboundType.OtherOut => "其他出库",
            _ => "未知"
        };
    }

    /// <summary>
    /// 获取出库类型中文文本（字符串版本）
    /// </summary>
    public static string GetOutboundTypeText(string? type)
    {
        return type switch
        {
            "SalesOut" => "销售出库",
            "SubcontractOut" => "委外出库",
            "ReturnOut" => "退货出库",
            "ScrapOut" => "报废出库",
            "ProductionPick" => "生产领用",
            "InspectionPick" => "检验领用",
            "TransferOut" => "移库出库",
            "OtherOut" => "其他出库",
            _ => type ?? ""
        };
    }

    // ========== 用料计划状态 ==========

    /// <summary>
    /// 获取用料计划状态中文文本
    /// </summary>
    public static string GetMaterialPlanStatusText(MaterialPlanStatus status)
    {
        return status switch
        {
            MaterialPlanStatus.NotPlanned => "未计划",
            MaterialPlanStatus.Partial => "部分",
            MaterialPlanStatus.TheoreticalSatisfied => "理论满足",
            MaterialPlanStatus.Satisfied => "满足",
            MaterialPlanStatus.Excess => "超量",
            _ => "未知"
        };
    }

    // ========== 有效流转状态 ==========

    /// <summary>
    /// 获取有效流转状态中文文本
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
    /// 获取有效主号状态中文文本
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

    // ========== 原料类型 ==========

    /// <summary>
    /// 获取原料类型中文文本
    /// </summary>
    public static string GetRawMaterialTypeText(RawMaterialType type)
    {
        return type switch
        {
            RawMaterialType.SemiFinished => "荒管",
            RawMaterialType.SemiProduct => "半成品",
            RawMaterialType.RoundBar => "圆棒",
            _ => "未知"
        };
    }

    /// <summary>
    /// 获取原料类型中文文本（字符串版本）
    /// </summary>
    public static string GetRawMaterialTypeText(string? type)
    {
        return type switch
        {
            "SemiFinished" => "荒管",
            "SemiProduct" => "半成品",
            "RoundBar" => "圆棒",
            _ => type ?? ""
        };
    }

    // ========== 产品要求类型 ==========

    /// <summary>
    /// 获取要求类型中文文本
    /// </summary>
    public static string GetRequirementTypeText(RequirementType type)
    {
        return type switch
        {
            RequirementType.Normal => "常规",
            RequirementType.Special => "特殊",
            _ => "未知"
        };
    }

    // ========== 产品检验类型 ==========
    public static string GetProductInspectionTypeText(string? type)
    {
        return type switch
        {
            "Critical" => "回厂复检",
            "Order" => "不需再检验",
            _ => type ?? ""
        };
    }

    /// <summary>
    /// 获取保养/点检任务状态颜色
    /// </summary>
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

    /// <summary>
    /// 获取保养/点检任务状态颜色（枚举版本）
    /// </summary>
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

    // ========== 冷轧排程 ==========
    /// <summary>冷轧完工要求中文显示</summary>
    public static string GetCompletionTypeText(string? ct) => ct switch
    {
        "All" => "全量",
        "Urgent" or "Partial1" => "特急单",
        "Partial2" => "急单",
        "Partial3" => "含B顺",
        _ => "",
    };

    /// <summary>冷轧排程类型中文显示</summary>
    public static string GetRollTypeText(string? rollType) => rollType switch
    {
        "All" or "Subsequent" => "全量",
        "Urgent" or "Partial1" => "特急单",
        "Partial2" => "急单",
        "Partial3" => "含B顺",
        _ => "",
    };
}
