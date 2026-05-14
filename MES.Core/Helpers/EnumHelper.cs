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
                                   ("Pending", "待修正"),
                                   ("Cancelled", "已取消"));

        Register<MaterialPlanStatus>(("NotPlanned", "未计划"),
                                      ("Partial", "部分"),
                                      ("TheoreticalSatisfied", "理论满足"),
                                      ("Satisfied", "满足"),
                                      ("Excess", "超量"));

        Register<InventoryPlanStatus>(("Planned", "已计划"),
                                       ("Confirmed", "已确认"),
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
                                 ("Hard", "硬态"));

        Register<SettlementMethod>(("Theoretical", "理算"),
                                    ("Weighing", "过磅"),
                                    ("WeighingNegative", "过磅-负"));

        Register<SalesOrderStatus>(("Pending", "待处理"),
                                    ("Confirmed", "已确认"),
                                    ("Cancelled", "已取消"));

        Register<MaterialName>(("SeamlessPipe", "无缝管"),
                                ("WeldedPipe", "焊管"));

        Register<ReworkType>(("EmptyDrawing", "空拉改制"),
                              ("FewerPass", "少道次改制"),
                              ("ManualSelect", "人工选择改制"));

        Register<RawMaterialType>(("SemiFinished", "荒管"),
                                   ("SemiProduct", "半成品"));

        Register<FinishedProductType>(("Critical", "临界成品"),
                                       ("Order", "订单成品"));

        Register<MaterialCategory>(("RoundBar", "原材料"),
                                    ("RoughTube", "二级原料"),
                                    ("SemiProduct", "半成品"),
                                    ("OrderFinished", "订单成品"),
                                    ("StockFinished", "非订单成品"),
                                    ("CriticalFinished", "临界成品"),
                                    ("DefectRoundBar", "不合格圆棒"),
                                    ("DefectRoughTube", "不合格荒管"),
                                    ("DefectSemiProduct", "不合格中间品"),
                                    ("DefectFinished", "不合格成品"),
                                    ("Scrap", "废料"),
                                    ("Surplus", "余料"));

        Register<OutboundType>(("ProductionPick", "生产领料"),
                                ("SalesOut", "销售出库"),
                                ("ReturnOut", "退货出库"),
                                ("SubcontractOut", "委外加工"),
                                ("ScrapOut", "报废出库"),
                                ("TransferOut", "移库出库"),
                                ("InventoryLoss", "盘亏出库"),
                                ("SampleOut", "样品出库"),
                                ("OtherOut", "其他出库"));

        Register<CustomerStatus>(("Active", "启用"),
                                  ("Inactive", "停用"));

        Register<RequirementType>(("Normal", "常规"),
                                   ("Special", "特殊"));

        Register<NotificationType>(("NewMaterial", "新物料确认"),
                                    ("DeleteBlocked", "删除拦截"),
                                    ("OutboundAlert", "出库预警"));

        Register<NotificationChangeType>(("Deleted", "订单已删除"),
                                          ("ItemChanged", "项次已变更"));

        Register<BatchStatus>(("None", "未产"),
                               ("InProgress", "在产"),
                               ("Completed", "完成"),
                               ("Suspended", "挂起"),
                               ("Cancelled", "作废"));

        Register<PurchaseOrderStatus>(("Open", "未到货"),
                                       ("Partial", "部分到货"),
                                       ("Completed", "已完成"),
                                       ("Cancelled", "已取消"));

        Register<SubcontractOrderStatus>(("Sent", "已发出未收回"),
                                          ("PartialReturned", "部分收回"),
                                          ("Completed", "已完成"),
                                          ("Cancelled", "已取消"));

        Register<SectionOutsourceStatus>(("PendingRecovery", "待回收"),
                                          ("Recovered", "已回收"),
                                          ("InProgress", "在轧"));
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
        if (name == null) return value.ToString();

        return _enumToDisplay.TryGetValue(enumType, out var dict) && dict.TryGetValue(name, out var display)
            ? display
            : name;
    }

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
    public static T? TryParse<T>(string text) where T : struct, Enum
    {
        try { return Parse<T>(text); }
        catch { return null; }
    }
}
