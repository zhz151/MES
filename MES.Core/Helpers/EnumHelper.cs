using MES.Core.Enums;

namespace MES.Core.Helpers;

/// <summary>
/// 枚举值 ↔ 中文显示名双向映射工具
/// 用于 Excel 导入导出时的枚举转换
/// </summary>
public static class EnumHelper
{
    private static readonly Dictionary<Type, Dictionary<string, string>> _enumToDisplay;
    private static readonly Dictionary<Type, Dictionary<string, string>> _displayToEnum;

    static EnumHelper()
    {
        _enumToDisplay = new Dictionary<Type, Dictionary<string, string>>();
        _displayToEnum = new Dictionary<Type, Dictionary<string, string>>();

        Register<WorkOrderStatus>(("NotGenerated", "未编制"),
                                   ("Confirmed", "已确定"),
                                   ("Pending", "待修正"));

        Register<MaterialPlanStatus>(("NotPlanned", "未计划"),
                                      ("Partial", "部分"),
                                      ("TheoreticalSatisfied", "理论满足"),
                                      ("Satisfied", "满足"),
                                      ("Excess", "超量"));

        Register<InventoryPlanStatus>(("Planned", "已计划"),
                                       ("Confirmed", "已确认"),
                                       ("Completed", "已完成"),
                                       ("Cancelled", "已取消"));

        Register<LengthStatus>(("Fixed", "定尺"),
                                ("Range", "范围尺"),
                                ("NonFixed", "非定尺"));

        Register<DeliveryState>(("SolutionAnnealedAndPickled", "固溶酸洗"),
                                 ("SolutionAnnealedAndPickledUTube", "固溶酸洗-U型管"),
                                 ("SolutionAnnealedAndPickledExternalPolished", "固溶酸洗-外抛光"),
                                 ("SolutionAnnealedAndPickledInternalPolished", "固溶酸洗-内抛光"),
                                 ("SolutionAnnealedAndPickledBothPolished", "固溶酸洗-内外抛光"),
                                 ("SolutionAnnealedAndPickledCoiled", "固溶酸洗-盘管"),
                                 ("Bright", "光亮"),
                                 ("BrightUTube", "光亮-U型管"),
                                 ("BrightCoiled", "光亮-盘管"),
                                 ("Hard", "硬态"),
                                 ("SolidSolutionStraightening", "固溶矫直"));

        Register<SettlementMethod>(("Theoretical", "理算"),
                                    ("Weighing", "过磅"),
                                    ("WeighingNegative", "过磅-负"));

        Register<SalesOrderStatus>(("Pending", "待处理"),
                                    ("Confirmed", "已确认"),
                                    ("Cancelled", "已取消"));

        Register<PipeManufacturingType>(("SeamlessPipe", "无缝管"),
                                         ("WeldedPipe", "焊管"));

        Register<ReworkType>(("EmptyDrawing", "空拉改制"),
                              ("FewerPass", "少道次改制"),
                              ("ManualSelect", "人工选择改制"));

        Register<FinishedProductType>(("Critical", "临界成品"),
                                       ("Order", "订单成品"),
                                       ("SpecialDeliveryStatus", "订成-非交付态"));

        Register<ProductionType>(("RoughTube", "荒管生产"),
                                  ("InProcess", "在制生产"),
                                  ("Inventory", "库存"),
                                  ("OutsourcedPurchased", "外购"),
                                  ("Rework", "返整"),
                                  ("Subcontract", "委外生产"),
                                  ("ExternalProcessing", "对外加工"));

        Register<OutboundType>(("ProductionPick", "生产领用"),
                                ("SalesOut", "销售出库"),
                                ("ReturnOut", "退货出库"),
                                ("SubcontractOut", "委外出库"),
                                ("ScrapOut", "报废出库"),
                                ("InspectionPick", "检验领用"),
                                ("TransferOut", "移库出库"),
                                ("OtherOut", "其他出库"));

        Register<InboundSource>(("Purchase", "外购"),
                                 ("Subcontract", "委外"),
                                 ("ReturnIn", "退货入库"),
                                 ("ProductionInbound", "生产入库"),
                                 ("InspectionInbound", "检验入库"),
                                 ("TransferIn", "移库入库"),
                                 ("Other", "其它"));

        Register<CustomerStatus>(("Active", "启用"),
                                  ("Inactive", "停用"));

        Register<RequirementType>(("Normal", "普通"),
                                   ("Special", "特殊"));

        Register<NotificationType>(("NewMaterial", "新物料确认"),
                                    ("DeleteBlocked", "删除拦截"),
                                    ("OutboundAlert", "出库预警"),
                                    ("WorkOrderDeleted", "工单已删除"),
                                    ("OrderDeleted", "订单已删除"),
                                    ("OrderChanged", "订单已变更"),
                                    ("WorkOrderChanged", "工单内容已变更"),
                                    ("BatchPlanAutoCompleted", "批次变更自动完成"),
                                    ("InboundMismatchAlert", "入库状态不一致"));

        Register<BatchStatus>(("None", "未产"),
                               ("InProgress", "在产"),
                               ("InFinalInspection", "成检"),
                               ("Completed", "完成"),
                               ("Suspended", "暂停"));

        Register<PurchaseOrderStatus>(("Open", "已下单"),
                                       ("Partial", "部分到货"),
                                       ("Completed", "已完成"));

        Register<SubcontractOrderStatus>(("Sent", "已发出"),
                                          ("PartialReturned", "部分收回"),
                                          ("Completed", "已完成"));

        Register<SectionOutsourceStatus>(("PendingRecovery", "待回收"),
                                          ("Recovered", "已回收"),
                                          ("InProgress", "在轧"));

        Register<RepairPriority>(("Normal", "普通"),
                                  ("Urgent", "紧急"),
                                  ("Emergency", "特急"));

        Register<LifecycleStatus>(("Active", "在用"),
                                   ("Standby", "备用"),
                                   ("Scrapped", "报废"));

        Register<UsageType>(("Primary", "主生产设备"),
                             ("Secondary", "辅生产设备"),
                             ("Other", "其它"));

        Register<RunningStatus>(("Normal", "正常"),
                                 ("Pending", "待维修"),
                                 ("InProgress", "维修中"));

        Register<RepairOrderStatus>(("Pending", "待维修"),
                                     ("InProgress", "维修中"),
                                     ("Completed", "完成"));

        Register<EquipmentTaskStatus>(("NotApplicable", "不适用"),
                                       ("Pending", "待执行"),
                                       ("Normal", "正常"),
                                       ("Overdue", "逾期"));

        Register<TaskOrderStatus>(("Pending", "待执行"),
                                   ("Completed", "已完成"),
                                   ("Overdue", "已逾期"));

        Register<InspectionItem>(("PMIInspection", "PMI检验"),
                                  ("VisualInspection", "表检"),
                                  ("Dimension", "尺寸"),
                                  ("Endoscopy", "内窥"),
                                  ("HydrostaticPressure", "水压"),
                                  ("UnderwaterPneumatic", "水下气压"),
                                  ("EddyCurrent", "涡流"),
                                  ("Ultrasonic", "超声波"),
                                  ("PortColoring", "端口着色"));

        Register<InspectionType>(("PreInspection", "预成检"),
                                  ("FormalInspection", "正式成检"));

        Register<DisposalMethod>(("Rework", "返整"),
                                  ("WarehouseEntry", "入库"),
                                  ("Scrap", "报废"));

        Register<NcrStatus>(("Pending", "待处理"),
                             ("Processing", "处理中"),
                             ("Closed", "已关闭"));

        Register<PicklingStatus>(("Soaking", "浸泡中"),
                                  ("Completed", "已完工"));

        Register<ResponsibilityCategory>(("ProductionInternal", "生产-厂内"),
                                          ("ProductionOutsource", "生产-外协"),
                                          ("MaterialTubeBlank", "原料-荒管"),
                                          ("MaterialPurchased", "原料-外购成品"),
                                          ("MaterialSurplus", "原料-余库料"));

        Register<SeverityLevel>(("Critical", "严重"),
                                 ("General", "一般"));

        Register<VerifyResult>(("Passed", "通过"),
                                ("NeedsRectification", "需整改"),
                                ("NotApplicable", "不适用"));


        Register<SectionStatus>(("Completed", "已完成"),
                                 ("InProgress", "进行中"),
                                 ("Outsource", "委外中"),
                                 ("Next", "待执行"),
                                 ("Pending", "待处理"));

        Register<ShiftType>(("DayShift", "白班"),
                             ("MiddleShift", "中班"),
                             ("NightShift", "夜班"));

        Register<MaterialType>(("Finished", "备料成品"),
                                ("OrderFinished", "订单成品"),
                                ("CriticalFinished", "临界成品"),
                                ("Surplus", "余库料"),
                                ("SemiFinished", "半成品"),
                                ("DefectSemi", "次品半成品"),
                                ("DefectFinished", "次品成品"),
                                ("RoughTube", "荒管"),
                                ("RoundBar", "圆棒"),
                                ("DefectRoundBar", "次品圆棒"),
                                ("DefectRoughTube", "次品荒管"),
                                ("Scrap", "报废品"),
                                ("SpecialDeliveryStatus", "订成-非交付态"),
                                ("WorkInProgress", "在制品"),
                                ("DefectWIP", "次品在制"));

        Register<ReportTemplateType>(("ProductionRecord", "普通报工"),
                                      ("PicklingInRecord", "入缸"),
                                      ("PicklingOutRecord", "出缸完工"),
                                      ("SectionOutsource", "工段委外"),
                                      ("OutsourceRecovery", "委外回收"),
                                      ("ProcessInspection", "过程检验"),
                                      ("FinalInspection", "成品检验"),
                                      ("MaterialReceiveCheck", "成检到料"));

        Register<BatchInputType>(("Warehouse", "仓库投料"),
                                 ("SplitFromNumber", "编号拆分"),
                                 ("Other", "其它"));

        Register<CutDoubtType>(("QuantityMismatch", "疑问-数量"),
                                ("MissingRecords", "疑问-缺少"),
                                ("Normal", "正常"));
    }

    private static void Register<T>(params (string value, string display)[] mappings) where T : Enum
    {
        var type = typeof(T);
        var toDisplay = new Dictionary<string, string>();
        var toEnum = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (value, display) in mappings)
        {
            toDisplay[value] = display;
            toEnum[display] = value;      // 中文名 → 枚举值名
            toEnum[value] = value;        // 英文名 → 枚举值名（容错）
        }

        _enumToDisplay[type] = toDisplay;
        _displayToEnum[type] = toEnum;
    }

    /// <summary>
    /// 获取枚举值的中文显示名（泛型版本）
    /// </summary>
    public static string GetDisplayName<T>(T value) where T : Enum
    {
        var name = Enum.GetName(typeof(T), value);
        if (name == null) return value.ToString();

        return _enumToDisplay.TryGetValue(typeof(T), out var dict) && dict.TryGetValue(name, out var display)
            ? display
            : name;
    }

    /// <summary>
    /// 获取枚举值的中文显示名（非泛型版本，需传入枚举类型）
    /// </summary>
    public static string GetDisplayName(Type enumType, object value)
    {
        var name = Enum.GetName(enumType, value);
        if (name == null) return value.ToString() ?? "";

        return _enumToDisplay.TryGetValue(enumType, out var dict) && dict.TryGetValue(name, out var display)
            ? display
            : name;
    }

    /// <summary>
    /// 获取枚举值的中文显示名（字符串版本，适用于 DTO 中存储为字符串的枚举）
    /// </summary>
    public static string GetDisplayName<T>(string? enumName) where T : struct, Enum
        => Enum.TryParse<T>(enumName ?? "", true, out var result) ? GetDisplayName(result) : (enumName ?? "");

    /// <summary>
    /// 获取枚举值的中文显示名（字符串版本，非泛型，需传入枚举类型和字符串值）
    /// </summary>
    public static string GetDisplayName(Type enumType, string? enumName)
        => Enum.TryParse(enumType, enumName ?? "", true, out var result) && result is Enum e
            ? GetDisplayName(enumType, e) : (enumName ?? "");

    /// <summary>
    /// 将中文名（或英文名）解析为枚举值（非泛型版本，返回 object）
    /// </summary>
    public static object Parse(string text, Type enumType)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("值不能为空", nameof(text));

        if (_displayToEnum.TryGetValue(enumType, out var dict) && dict.TryGetValue(text.Trim(), out var enumName))
            return Enum.Parse(enumType, enumName);

        try { return Enum.Parse(enumType, text.Trim(), ignoreCase: true); }
        catch { }

        var validValues = _enumToDisplay.TryGetValue(enumType, out var displayDict)
            ? string.Join("、", displayDict.Values)
            : string.Join(", ", Enum.GetNames(enumType));

        throw new ArgumentException($"无法识别值 \"{text}\"，可用值: {validValues}");
    }

    /// <summary>
    /// 将中文名（或英文名）解析为枚举值
    /// </summary>
    public static T Parse<T>(string text) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("值不能为空", nameof(text));

        // 1. 尝试通过显示名/枚举名查找
        if (_displayToEnum.TryGetValue(typeof(T), out var dict) && dict.TryGetValue(text.Trim(), out var enumName))
            return Enum.Parse<T>(enumName);

        // 2. 尝试直接解析枚举名（大小写不敏感）
        if (Enum.TryParse<T>(text.Trim(), ignoreCase: true, out var result))
            return result;

        // 3. 失败时提示可用值
        var validValues = _enumToDisplay.TryGetValue(typeof(T), out var displayDict)
            ? string.Join("、", displayDict.Values)
            : string.Join(", ", Enum.GetNames<T>());

        throw new ArgumentException($"无法识别值 \"{text}\"，可用值: {validValues}");
    }

    /// <summary>
    /// 尝试将中文名解析为枚举值，失败返回 null
    /// </summary>
    public static T? TryParse<T>(string? text) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        try { return Parse<T>(text); }
        catch { return null; }
    }
}
