using System.Collections.Concurrent;
using MES.Core.DTOs.Configuration;
using MES.Core.Enums;

namespace MES.Core.Helpers;

/// <summary>
/// 枚举值 ↔ 中文显示名双向映射工具
/// 用于 Excel 导入导出时的枚举转换
/// 支持配置表覆盖：ApplyEnumOverrides 注入后，显示名/反向解析以配置表优先，静态字典兜底
/// （EnumKey 约定为枚举类型名 typeof(T).Name，如 "BatchStatus"）。
/// </summary>
public static class EnumHelper
{
    private static readonly Dictionary<Type, Dictionary<string, string>> _enumToDisplay;
    private static readonly Dictionary<Type, Dictionary<string, string>> _displayToEnum;

    /// <summary>配置表覆盖：EnumKey(枚举类型名) → Value → DisplayName（ConcurrentDictionary 保证读写并发安全）</summary>
    private static readonly ConcurrentDictionary<string, Dictionary<string, string>> _displayOverrides = new(StringComparer.Ordinal);

    /// <summary>配置表覆盖反向：EnumKey → DisplayName → Value（导入反向解析用）</summary>
    private static readonly ConcurrentDictionary<string, Dictionary<string, string>> _displayOverrideReverse = new(StringComparer.Ordinal);

    /// <summary>配置表排序覆盖：EnumKey → Value → DisplayOrder（下拉/筛选选项按此排序）</summary>
    private static readonly ConcurrentDictionary<string, Dictionary<string, int>> _displayOrders = new(StringComparer.Ordinal);

    /// <summary>枚举定义说明兜底：EnumKey(枚举类型名) → 枚举 XML 注释说明（枚举显示配置表 Remark 为空时显示）</summary>
    private static readonly Dictionary<string, string> _enumRemarks = new(StringComparer.Ordinal)
    {
        ["InboundSource"] = "入库来源",
        ["MaterialType"] = "物料类型（对应库存批次 MaterialType / 生产批次 SourceMaterialType）",
        ["PipeManufacturingType"] = "钢管制造类别",
        ["InspectionType"] = "成检类型",
        ["CutDoubtType"] = "成切存疑类型（疑问-数量 / 疑问-缺少 / 正常）",
        ["BatchInputType"] = "批次投料类型",
        ["InspectionRequirementStage"] = "技术要求检验项阶段（终=仅终检；预=仅预检；预+终=预检与终检均需；-=不要求）",
        ["ReportTemplateType"] = "报工模板类型（决定报工写入哪张表及使用哪个表单模板）",
        ["ShiftType"] = "班次",
        ["EquipmentTaskStatus"] = "设备点检/保养状况（物化存储到设备表）",
        ["NotificationType"] = "通知类型",
        ["PicklingStatus"] = "去油/酸洗入缸状态",
        ["LifecycleStatus"] = "设备生命周期状态",
        ["PurchaseOrderStatus"] = "采购订单状态",
        ["OutboundType"] = "出库类型",
        ["DisposalMethod"] = "不合格品处置方式",
        ["RepairOrderStatus"] = "维修工单状态（由字段完整度自动推导）",
        ["ProductionType"] = "生产类型",
        ["MaterialPlanStatus"] = "用料计划状态（4档，已取消理论满足并入满足）",
        ["RepairPriority"] = "维修优先级",
        ["InventoryPlanStatus"] = "库存使用计划状态",
        ["ReworkType"] = "库料生产改制类型",
        ["FinishedProductType"] = "成品类型（外购成品计划）",
        ["NcrStatus"] = "NCR 不合格品报告状态",
        ["InspectionItem"] = "成品检验项目",
        ["RunningStatus"] = "设备运行状态（由维修记录自动驱动）",
        ["SalesOrderStatus"] = "订单状态（三态）",
        ["BatchStatus"] = "批次状态",
        ["SectionOutsourceStatus"] = "工段委外状态",
        ["SectionStatus"] = "工段可视化状态",
        ["SeverityLevel"] = "事故严重程度",
        ["SubcontractOrderStatus"] = "委外加工单状态",
        ["WorkOrderStatus"] = "工单状态（3态，不含已取消——工单物理删除）",
        ["VerifyResult"] = "纠正预防措施验证结论",
        ["TaskOrderStatus"] = "点检/保养工单共用状态",
        ["UsageType"] = "设备作用类型（使用分类）"
    };

    static EnumHelper()
    {
        _enumToDisplay = new Dictionary<Type, Dictionary<string, string>>();
        _displayToEnum = new Dictionary<Type, Dictionary<string, string>>();

        Register<WorkOrderStatus>(("NotGenerated", "未编制"),
                                   ("Confirmed", "已确定"),
                                   ("Pending", "待修正"));

        Register<MaterialPlanStatus>(("NotPlanned", "未计划"),
                                      ("Partial", "部分"),
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
                                ("OtherOut", "其它出库"));

        Register<InboundSource>(("Purchase", "外购"),
                                 ("Subcontract", "委外"),
                                 ("ProductionInbound", "生产入库"),
                                 ("InspectionInbound", "检验入库"),
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
                                       ("Completed", "已完成"),
                                       ("OverReceived", "超量到货"));

        Register<SubcontractOrderStatus>(("Sent", "已发出"),
                                          ("PartialReturned", "部分收回"),
                                          ("Completed", "已完成"),
                                          ("OverReceived", "超量到货"));

        Register<SectionOutsourceStatus>(("PendingRecovery", "待回收"),
                                          ("Recovered", "已回收"),
                                          ("InProgress", "在轧"),
                                          ("Virtual", "略"));

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

        Register<InspectionType>(("PreInspection", "预检"),
                                  ("FormalInspection", "终检"));

        Register<InspectionRequirementStage>(("None", "-"),
                                              ("FinalOnly", "终"),
                                              ("PreOnly", "预"),
                                              ("PreAndFinal", "预+终"));

        Register<DisposalMethod>(("Rework", "返整"),
                                  ("WarehouseEntry", "入库"),
                                  ("Scrap", "报废"));

        Register<NcrStatus>(("Pending", "待处理"),
                             ("Processing", "处理中"),
                             ("Closed", "已关闭"));

        Register<PicklingStatus>(("Soaking", "浸泡中"),
                                  ("Completed", "已完工"));

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

        // 配置表覆盖优先
        if (_displayOverrides.TryGetValue(typeof(T).Name, out var overrides)
            && overrides.TryGetValue(name, out var overrideDisplay))
            return overrideDisplay;

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

        // 配置表覆盖优先
        if (_displayOverrides.TryGetValue(enumType.Name, out var overrides)
            && overrides.TryGetValue(name, out var overrideDisplay))
            return overrideDisplay;

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

        // 配置表覆盖反向优先（改名后的新中文可反查）
        if (_displayOverrideReverse.TryGetValue(enumType.Name, out var reverse)
            && reverse.TryGetValue(text.Trim(), out var overrideName))
            return Enum.Parse(enumType, overrideName);

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

        // 0. 配置表覆盖反向优先（改名后的新中文可反查）
        if (_displayOverrideReverse.TryGetValue(typeof(T).Name, out var reverse)
            && reverse.TryGetValue(text.Trim(), out var overrideName))
            return Enum.Parse<T>(overrideName);

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

    /// <summary>
    /// 注入某枚举的配置表覆盖（Value → DisplayName）。整体替换该 EnumKey 的覆盖字典。
    /// 前端 Blazor WASM 启动时与后端 API 启动时调用，显示名/反向解析以配置表优先。
    /// </summary>
    public static void ApplyEnumOverrides(string enumKey, IReadOnlyDictionary<string, string> valueToDisplay)
    {
        var display = new Dictionary<string, string>(valueToDisplay, StringComparer.Ordinal);
        var reverse = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kvp in valueToDisplay)
        {
            if (!string.IsNullOrEmpty(kvp.Value))
                reverse[kvp.Value] = kvp.Key;
        }
        _displayOverrides[enumKey] = display;
        _displayOverrideReverse[enumKey] = reverse;
    }

    /// <summary>
    /// 清除全部枚举的配置表覆盖（回退静态字典）。配置表被清空/重建时调用。
    /// </summary>
    public static void ClearEnumOverrides()
    {
        _displayOverrides.Clear();
        _displayOverrideReverse.Clear();
        _displayOrders.Clear();
    }

    /// <summary>
    /// 注入某枚举的配置表排序覆盖（Value → DisplayOrder）。与 ApplyEnumOverrides 配合使用。
    /// 前端 MainLayout 启动时从 options-map 注入；未注入的枚举按静态注册顺序。
    /// </summary>
    public static void ApplyEnumOrder(string enumKey, IReadOnlyDictionary<string, int> valueToOrder)
    {
        _displayOrders[enumKey] = new Dictionary<string, int>(valueToOrder, StringComparer.Ordinal);
    }

    /// <summary>
    /// 获取枚举的显示选项列表，按配置表 DisplayOrder 升序排序；
    /// 未注入排序的枚举按静态注册顺序返回。
    /// 用于列筛选下拉 / 表单下拉（与 DisplayHelper 配合）。
    /// </summary>
    public static List<EnumDisplayOptionDto> GetDisplayOptions<T>() where T : struct, Enum
    {
        var type = typeof(T);
        var typeName = type.Name;
        var hasOrder = _displayOrders.TryGetValue(typeName, out var orders);

        // 按静态注册顺序遍历（未配置排序时的默认顺序，Dictionary 保持插入序）
        var registered = _enumToDisplay.TryGetValue(type, out var displayDict)
            ? displayDict.Keys.ToList()
            : Enum.GetNames(type).ToList();

        // 静态注册序作为未配置值的稳定 tiebreak（避免配置部分值后未配置项被字母序打乱）
        var pending = new List<(string Value, string Display, int Order, int Index)>(registered.Count);
        for (var i = 0; i < registered.Count; i++)
        {
            var name = registered[i];
            var display = GetDisplayName(type, name);
            var order = hasOrder && orders!.TryGetValue(name, out var o) ? o : int.MaxValue;
            pending.Add((name, display, order, i));
        }

        return hasOrder
            ? pending.OrderBy(o => o.Order).ThenBy(o => o.Index)
                    .Select(o => new EnumDisplayOptionDto { Value = o.Value, DisplayName = o.Display, DisplayOrder = o.Order })
                    .ToList()
            : pending.Select(o => new EnumDisplayOptionDto { Value = o.Value, DisplayName = o.Display, DisplayOrder = o.Order }).ToList();
    }

    /// <summary>
    /// 导出全部已注册枚举的静态映射：枚举类型名 → (Value → DisplayName)。
    /// 用于种子数据生成（恢复默认）。
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> GetAllMappings()
    {
        var result = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        foreach (var kvp in _enumToDisplay)
        {
            result[kvp.Key.Name] = new Dictionary<string, string>(kvp.Value, StringComparer.Ordinal);
        }
        return result;
    }

    /// <summary>指定枚举是否已注册静态映射</summary>
    public static bool IsRegistered(Type enumType) => _enumToDisplay.ContainsKey(enumType);

    /// <summary>取枚举定义说明（枚举显示配置表 Remark 为空时兜底显示；未定义返回 null）</summary>
    public static string? GetEnumRemark(string enumKey)
        => _enumRemarks.TryGetValue(enumKey, out var remark) ? remark : null;
}
