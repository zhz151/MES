namespace MES.Core.Constants;

/// <summary>
/// 工段名称常量定义及映射。所有涉及工段名称的代码必须引用此处定义的常量，禁止直接使用字符串字面量。
/// </summary>
public static class SectionDefs
{
    // ========== 15 个标准工段名称常量 ==========
    public const string ColdRollDraw = "冷轧拔";
    public const string OilPipeCut = "油管断";
    public const string Degrease = "去油";
    public const string Solution = "固溶";
    public const string Straighten = "矫直";
    public const string Cut = "断切";
    public const string ThicknessMeasure = "测壁厚";
    public const string Pickle = "酸洗";
    public const string OuterPolish = "外抛光";
    public const string InnerGrinding = "内修磨";
    public const string OuterSpotGrinding = "外点磨";
    public const string Inspection = "检验";
    public const string WeldingHead = "打焊头";
    public const string Lubrication = "润滑";
    public const string Warehouse = "入库";

    /// <summary>所有工段名称的有序列表</summary>
    public static readonly string[] All =
    [
        ColdRollDraw, OilPipeCut, Degrease, Solution, Straighten, Cut,
        ThicknessMeasure, Pickle, OuterPolish, InnerGrinding, OuterSpotGrinding,
        Inspection, WeldingHead, Lubrication, Warehouse
    ];

    // ========== ProcessGroup 属性名 → 中文名映射 ==========
    /// <summary>key=ProcessGroup 属性名, value=工段中文名</summary>
    public static readonly Dictionary<string, string> PropertyToName = new()
    {
        ["ColdRollDraw"] = ColdRollDraw,
        ["OilPipeCut"] = OilPipeCut,
        ["Degrease"] = Degrease,
        ["Solution"] = Solution,
        ["Straighten"] = Straighten,
        ["Cut"] = Cut,
        ["ThicknessMeasure"] = ThicknessMeasure,
        ["Pickle"] = Pickle,
        ["OuterPolish"] = OuterPolish,
        ["InnerGrinding"] = InnerGrinding,
        ["OuterSpotGrinding"] = OuterSpotGrinding,
        ["Inspection"] = Inspection,
        ["WeldingHead"] = WeldingHead,
        ["Lubrication"] = Lubrication,
        ["Warehouse"] = Warehouse,
    };

    /// <summary>ProcessGroup 中涉及的属性名字段列表</summary>
    public static readonly string[] PropertyNames =
    [
        "ColdRollDraw", "OilPipeCut", "Degrease", "Solution", "Straighten",
        "Cut", "ThicknessMeasure", "Pickle", "OuterPolish", "InnerGrinding",
        "OuterSpotGrinding", "Inspection", "WeldingHead", "Lubrication", "Warehouse"
    ];

    // ========== 别名映射（数据导入/修复时匹配变体名称） ==========
    /// <summary>key=别名, value=标准工段名</summary>
    public static readonly Dictionary<string, string> Aliases = new()
    {
        ["切管"] = OilPipeCut,
        ["脱脂"] = Degrease,
        ["测厚"] = ThicknessMeasure,
        ["外抛"] = OuterPolish,
        ["内磨"] = InnerGrinding,
        ["探伤"] = Inspection,
        ["焊头"] = WeldingHead,
    };

}
