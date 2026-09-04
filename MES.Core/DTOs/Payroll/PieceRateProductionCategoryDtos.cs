using MES.Core.Models;

namespace MES.Core.DTOs.Payroll;

// 生产计件类别（2026-09-02 重构引入）——DTO 全集：
// 类别 = 工段(必选单选) × 工序/产类/作业阶段(可空多选，空=全选) + 基准价 + 结算单位；
// 维度系数在子表 PieceRateProductionCategoryTier（无例外价/绝对价）。

/// <summary>类别查询参数</summary>
public class PieceRateProductionCategoryQueryParams : QueryParams
{
    /// <summary>按工段精确过滤（英文 Key，SectionKeys）</summary>
    public string? SectionKey { get; set; }

    /// <summary>按单位精确过滤（英文 Key，PieceRateUnitKeys）</summary>
    public string? Unit { get; set; }

    /// <summary>按启停过滤（null=全部）</summary>
    public bool? IsActive { get; set; }
}

/// <summary>下拉选项项</summary>
public class PieceRateCategoryOptionItemDto
{
    /// <summary>稳定英文 Key</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>中文显示名</summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>类别列表行</summary>
public class PieceRateProductionCategoryListItemDto
{
    public int Id { get; set; }

    /// <summary>工段（英文 Key，SectionKeys）</summary>
    public string SectionKey { get; set; } = string.Empty;

    /// <summary>工段中文</summary>
    public string SectionKeyChinese { get; set; } = string.Empty;

    /// <summary>工序约束 Key 集（空=全选工序）</summary>
    public List<string> ProcessKeys { get; set; } = new();

    /// <summary>产类约束 Key 集（空=全选产类）</summary>
    public List<string> ProductStatusKeys { get; set; } = new();

    /// <summary>作业阶段约束 Key 集（空=全选阶段含普通报工）</summary>
    public List<string> StageKeys { get; set; } = new();

    /// <summary>自动组合名（§3.2，不落库）</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>基准价（元/结算单位）</summary>
    public decimal BasePrice { get; set; }

    /// <summary>结算单位（英文 Key，PieceRateUnitKeys）</summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>结算单位中文</summary>
    public string UnitChinese { get; set; } = string.Empty;

    /// <summary>当前启用</summary>
    public bool IsActive { get; set; }

    /// <summary>维度档数（启用档行）</summary>
    public int TierCount { get; set; }

    public string? Remark { get; set; }

    public DateTimeOffset UpdatedTime { get; set; }

    public DateTimeOffset CreatedTime { get; set; }
}

/// <summary>类别维度档行</summary>
public class PieceRateProductionCategoryTierDto
{
    public int Id { get; set; }

    /// <summary>维度英文 Key（PieceRateDimensionKeys）</summary>
    public string DimensionKey { get; set; } = string.Empty;

    /// <summary>维度中文</summary>
    public string? DimensionKeyChinese { get; set; }

    /// <summary>区间原文（区间维）</summary>
    public string? RangeText { get; set; }

    public decimal? MinValue { get; set; }

    public decimal? MaxValue { get; set; }

    public int? MinInt { get; set; }

    public int? MaxInt { get; set; }

    /// <summary>等值维取值（特殊牌号/特殊制造状态/特殊设备号）</summary>
    public string? MatchValue { get; set; }

    /// <summary>加价系数（命中即乘）</summary>
    public decimal Ratio { get; set; }

    /// <summary>当前启用</summary>
    public bool IsActive { get; set; }
}

/// <summary>类别详情（含维度档全量，供编辑页）</summary>
public class PieceRateProductionCategoryDetailDto : PieceRateProductionCategoryListItemDto
{
    /// <summary>维度档行（全部维度，含停用行——编辑页需展示）</summary>
    public List<PieceRateProductionCategoryTierDto> Tiers { get; set; } = new();
}

/// <summary>类别保存请求（创建/更新共用；Id&gt;0=更新由路由传入）</summary>
public class PieceRateProductionCategorySaveRequest
{
    /// <summary>工段（英文 Key，必填单选）</summary>
    public string SectionKey { get; set; } = null!;

    /// <summary>工序约束（空=全选工序）</summary>
    public List<string> ProcessKeys { get; set; } = new();

    /// <summary>产类约束（空=全选产类）</summary>
    public List<string> ProductStatusKeys { get; set; } = new();

    /// <summary>作业阶段约束（空=全选阶段含普通报工）</summary>
    public List<string> StageKeys { get; set; } = new();

    /// <summary>基准价（必填 &gt;0）</summary>
    public decimal BasePrice { get; set; }

    /// <summary>结算单位（英文 Key，必填）</summary>
    public string Unit { get; set; } = null!;

    /// <summary>当前启用</summary>
    public bool IsActive { get; set; } = true;

    public string? Remark { get; set; }

    /// <summary>维度档行（全量意图：保存时整组替换）</summary>
    public List<PieceRateProductionCategoryTierSaveRequest> Tiers { get; set; } = new();
}

/// <summary>类别维度档保存行</summary>
public class PieceRateProductionCategoryTierSaveRequest
{
    /// <summary>维度英文 Key（PieceRateDimensionKeys）</summary>
    public string DimensionKey { get; set; } = null!;

    /// <summary>区间维区间原文（如 "D&gt;54"、"54≥D&gt;41"、"6-8"；服务端解析边界）</summary>
    public string? RangeText { get; set; }

    /// <summary>等值维取值（特殊牌号/特殊制造状态/特殊设备号）</summary>
    public string? MatchValue { get; set; }

    /// <summary>加价系数（命中即乘；&gt;0）</summary>
    public decimal Ratio { get; set; } = 1;

    /// <summary>当前启用</summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>类别选项源（编辑页下拉/多选数据）</summary>
public class PieceRateProductionCategoryOptionsDto
{
    /// <summary>启用工段（StandardWorkDay IsEnabled）</summary>
    public List<PieceRateCategoryOptionItemDto> Sections { get; set; } = new();

    /// <summary>启用工序（ProcessDefinition IsEnabled）</summary>
    public List<PieceRateCategoryOptionItemDto> Processes { get; set; } = new();

    /// <summary>产类（固定三值）</summary>
    public List<PieceRateCategoryOptionItemDto> ProductStatuses { get; set; } = new();

    /// <summary>作业阶段（PieceRateStageKeys）</summary>
    public List<PieceRateCategoryOptionItemDto> Stages { get; set; } = new();

    /// <summary>结算单位（PieceRateUnitKeys）</summary>
    public List<PieceRateCategoryOptionItemDto> Units { get; set; } = new();

    /// <summary>特殊制造状态（PieceRateStateKeys）</summary>
    public List<PieceRateCategoryOptionItemDto> States { get; set; } = new();

    /// <summary>特殊牌号候选（StandardGradeMapping.PlantGrade 去重）</summary>
    public List<string> Grades { get; set; } = new();
}

/// <summary>匹配输入（一条报工 / 试算）；返回 null = 未定价（命中不到启用类别）</summary>
public class PieceRateProductionMatchRequest
{
    /// <summary>工段（英文 Key，SectionKeys，来自记录 SectionName）</summary>
    public string SectionName { get; set; } = null!;

    /// <summary>工序（英文 Key，ProcessKeys，来自记录 ProcessName）</summary>
    public string? ProcessName { get; set; }

    /// <summary>产类（英文 Key，ProductStatuses）</summary>
    public string? ProductStatus { get; set; }

    /// <summary>作业阶段（PieceRateStageKeys：InTank/OutTank）；空=普通报工无阶段</summary>
    public string? Stage { get; set; }

    /// <summary>特殊制造状态（英文 Key，PieceRateStateKeys：Bright 光亮）；空=普通状态</summary>
    public string? SpecialState { get; set; }

    public decimal? OuterDiameter { get; set; }

    public decimal? WallThickness { get; set; }

    public decimal? Length { get; set; }

    public decimal? CutRate { get; set; }

    /// <summary>定尺种数（整数）</summary>
    public int? FixedLengthCount { get; set; }

    /// <summary>特殊牌号（工厂牌号 PlantGrade）</summary>
    public string? PlantGrade { get; set; }

    /// <summary>特殊设备号（报工 EquipmentName 文本）</summary>
    public string? EquipmentName { get; set; }

    /// <summary>报工备注自由文本（仅 ColdDrawType 冷拔类型维按关键词包含命中使用；空=该维系数1）</summary>
    public string? Remark { get; set; }
}

/// <summary>命中的维度档（展示用）</summary>
public class PieceRateProductionMatchTierHitDto
{
    /// <summary>维度英文 Key</summary>
    public string DimensionKey { get; set; } = string.Empty;

    /// <summary>维度中文</summary>
    public string? DimensionKeyChinese { get; set; }

    /// <summary>命中的区间原文或取值</summary>
    public string? RangeText { get; set; }

    /// <summary>该维系数（连乘因子）</summary>
    public decimal Ratio { get; set; }
}

/// <summary>匹配结果：单价 = 类别.BasePrice × 命中维档 Ratio 连乘</summary>
public class PieceRateProductionMatchResultDto
{
    public int CategoryId { get; set; }

    public string SectionKey { get; set; } = string.Empty;

    public string SectionKeyChinese { get; set; } = string.Empty;

    /// <summary>自动组合名</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>类别基准价</summary>
    public decimal BasePrice { get; set; }

    /// <summary>总系数（命中维档 Ratio 连乘）</summary>
    public decimal TotalRatio { get; set; }

    /// <summary>结算单价 = BasePrice × TotalRatio</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>结算单位（英文 Key）</summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>结算单位中文</summary>
    public string UnitChinese { get; set; } = string.Empty;

    /// <summary>模拟整行计件额（元；按记录计价命中后折算=结算单价×数量维，未按写名人头均分）。
    /// null=未定价或缺数量输入无法折算（手工 match-price 无计量字段恒 null）。</summary>
    public decimal? SimulatedAmount { get; set; }

    /// <summary>命中维档明细</summary>
    public List<PieceRateProductionMatchTierHitDto> Hits { get; set; } = new();

    public string? Remark { get; set; }
}

// ==================== 模拟测算（按产量记录点选计价，2026-09-04） ====================

/// <summary>模拟测算候选记录产量源（对应 PieceRateCollector 采集的 4 类产量源表）</summary>
public enum PieceRateProductionTrialSource
{
    /// <summary>生产记录（普通报工，无作业阶段；切行 Cut 接定尺/光亮）</summary>
    ProductionRecord,

    /// <summary>去油/酸洗入缸（Stage=InTank）</summary>
    PicklingIn,

    /// <summary>去油/酸洗完工（Stage=OutTank）</summary>
    PicklingOut,

    /// <summary>过程检验（无作业阶段；操作人=检验员）</summary>
    ProcessInspection
}

/// <summary>产量源中文显示（枚举显示一律中文，禁止直出 .ToString()）</summary>
public static class PieceRateProductionTrialSourceExtensions
{
    public static string ToChinese(this PieceRateProductionTrialSource source)
        => source switch
        {
            PieceRateProductionTrialSource.ProductionRecord => "生产记录",
            PieceRateProductionTrialSource.PicklingIn => "去油酸洗入缸",
            PieceRateProductionTrialSource.PicklingOut => "去油酸洗完工",
            PieceRateProductionTrialSource.ProcessInspection => "过程检验",
            _ => source.ToString()
        };
}

/// <summary>模拟测算候选产量记录查询（全局任意记录：跨期按产量源 / 关键字检索）</summary>
public class PieceRateProductionTrialRecordQuery : QueryParams
{
    /// <summary>产量源过滤（PieceRateProductionTrialSource 枚举名；空=全部 4 源合一排序，跨表需合并——建议必选单源）</summary>
    public string? Source { get; set; }
}

/// <summary>模拟测算候选产量记录行（点选后按 Id 计价，与月结采集同 Mapper 映射单源）。
/// 字段按 4 源语义归一并列，未命中源列留空：BatchNo=批次号（PicklingOut/ProcessInspection 自冗余、
/// 其余经所属批次投影）、Specification=制造规格（空回退所属批次）、Quantity/Weight=记录量、
/// Operator=操作人（ProcessInspection 为检验员 Inspector）、Stage=作业阶段（入缸/完工有、其余空）。</summary>
public class PieceRateProductionTrialRecordDto
{
    public int Id { get; set; }

    /// <summary>产量源（PieceRateProductionTrialSource 枚举名）</summary>
    public string SourceKey { get; set; } = string.Empty;

    /// <summary>产量源中文</summary>
    public string SourceChinese { get; set; } = string.Empty;

    /// <summary>记录日期（各源日期列：ExecDate/InDate/CompleteDate/InspectionDate）</summary>
    public DateTime RecordDate { get; set; }

    /// <summary>批次号</summary>
    public string? BatchNo { get; set; }

    /// <summary>工段（SectionKeys 归一的英文 Key）</summary>
    public string SectionKey { get; set; } = string.Empty;

    /// <summary>工段中文</summary>
    public string SectionKeyChinese { get; set; } = string.Empty;

    public string? ProcessName { get; set; }

    /// <summary>产类（英文 Key，ProductStatuses）</summary>
    public string? ProductStatus { get; set; }

    /// <summary>产类中文</summary>
    public string? ProductStatusChinese { get; set; }

    /// <summary>作业阶段（PieceRateStageKeys 英文 Key；空=普通报工无阶段）</summary>
    public string? StageKey { get; set; }

    /// <summary>作业阶段中文</summary>
    public string? StageChinese { get; set; }

    /// <summary>规格（记录制造规格，空回退所属批次规格）</summary>
    public string? Specification { get; set; }

    /// <summary>数量（支数）</summary>
    public int? Quantity { get; set; }

    /// <summary>重量(kg)</summary>
    public decimal? Weight { get; set; }

    /// <summary>操作人（各源操作列：Operator/Inspector）</summary>
    public string? Operator { get; set; }

    public string? EquipmentName { get; set; }

    public string? Remark { get; set; }
}
