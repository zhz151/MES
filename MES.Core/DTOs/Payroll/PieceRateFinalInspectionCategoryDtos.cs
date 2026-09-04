using MES.Core.Models;

namespace MES.Core.DTOs.Payroll;

// 成检计件类别（2026-09-03 引入）——DTO 全集：
// 类别 = 成检项目(InspectionItem 单选) + 基准价 + 结算单位；无工序/产类/作业阶段约束（无 PieceRate*Key 子表）。
// 维度系数在子表 PieceRateFinalInspectionCategoryTier（无例外价/绝对价）。
// 维度 Key 域见 PieceRateInspectionDimensionKeys。

/// <summary>类别查询参数</summary>
public class PieceRateFinalInspectionCategoryQueryParams : QueryParams
{
    /// <summary>按成检项目精确过滤（InspectionItem 枚举名）</summary>
    public string? ItemKey { get; set; }

    /// <summary>按单位精确过滤（英文 Key，PieceRateUnitKeys）</summary>
    public string? Unit { get; set; }

    /// <summary>按启停过滤（null=全部）</summary>
    public bool? IsActive { get; set; }
}

/// <summary>类别列表行</summary>
public class PieceRateFinalInspectionCategoryListItemDto
{
    public int Id { get; set; }

    /// <summary>成检项目（InspectionItem 枚举名）</summary>
    public string ItemKey { get; set; } = string.Empty;

    /// <summary>成检项目中文（EnumHelper）</summary>
    public string ItemKeyChinese { get; set; } = string.Empty;

    public decimal BasePrice { get; set; }

    public string Unit { get; set; } = string.Empty;

    public string UnitChinese { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    /// <summary>启用档行数</summary>
    public int TierCount { get; set; }

    public string? Remark { get; set; }

    public DateTimeOffset UpdatedTime { get; set; }

    public DateTimeOffset CreatedTime { get; set; }
}

/// <summary>维档行（详情展示）</summary>
public class PieceRateFinalInspectionCategoryTierDto
{
    public int Id { get; set; }

    public string DimensionKey { get; set; } = string.Empty;

    public string? DimensionKeyChinese { get; set; }

    /// <summary>区间原文或等值取值文本</summary>
    public string? RangeText { get; set; }

    public decimal? MinValue { get; set; }

    public decimal? MaxValue { get; set; }

    public int? MinInt { get; set; }

    public int? MaxInt { get; set; }

    public string? MatchValue { get; set; }

    public decimal Ratio { get; set; }

    public bool IsActive { get; set; }
}

/// <summary>类别详情（含维档全量，供编辑页）</summary>
public class PieceRateFinalInspectionCategoryDetailDto : PieceRateFinalInspectionCategoryListItemDto
{
    public List<PieceRateFinalInspectionCategoryTierDto> Tiers { get; set; } = new();
}

/// <summary>保存请求（创建/更新合一：维档整组替换）</summary>
public class PieceRateFinalInspectionCategorySaveRequest
{
    /// <summary>成检项目（InspectionItem 中文或英文名，服务端归一为枚举名）</summary>
    public string ItemKey { get; set; } = string.Empty;

    public decimal BasePrice { get; set; }

    /// <summary>结算单位英文 Key（PieceRateUnitKeys）</summary>
    public string Unit { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public string? Remark { get; set; }

    /// <summary>维档整组（保存时按此整体替换现有档行）</summary>
    public List<PieceRateFinalInspectionCategoryTierSaveRequest> Tiers { get; set; } = new();
}

/// <summary>维档行保存请求</summary>
public class PieceRateFinalInspectionCategoryTierSaveRequest
{
    /// <summary>维度英文 Key（PieceRateInspectionDimensionKeys）</summary>
    public string DimensionKey { get; set; } = string.Empty;

    /// <summary>区间原文（如 "D&gt;219"、"1-10"）或等值取值文本（等值维直接填 MatchValue 亦可）</summary>
    public string? RangeText { get; set; }

    /// <summary>等值维取值（长度状态 Fixed/Range/NonFixed / 特殊牌号 / 特殊状态 / 特殊设备号）</summary>
    public string? MatchValue { get; set; }

    public decimal Ratio { get; set; } = 1;

    public bool IsActive { get; set; } = true;
}

/// <summary>类别编辑页选项源</summary>
public class PieceRateFinalInspectionCategoryOptionsDto
{
    /// <summary>成检项目（InspectionItem 全 9 项）</summary>
    public List<PieceRateCategoryOptionItemDto> Items { get; set; } = new();

    /// <summary>结算单位</summary>
    public List<PieceRateCategoryOptionItemDto> Units { get; set; } = new();

    /// <summary>长度状态三档（定尺/范围尺/非定尺）</summary>
    public List<PieceRateCategoryOptionItemDto> LengthStatuses { get; set; } = new();

    /// <summary>特殊制造状态（DeliveryState 全集）</summary>
    public List<PieceRateCategoryOptionItemDto> States { get; set; } = new();

    /// <summary>工厂牌号（特殊牌号候选）</summary>
    public List<string> Grades { get; set; } = new();
}

/// <summary>试算匹配请求（一条成检记录 → 命中类别单价）</summary>
public class PieceRateFinalInspectionMatchRequest
{
    /// <summary>成检项目（InspectionItem 中文或英文名）</summary>
    public string ItemKey { get; set; } = string.Empty;

    /// <summary>长度状态（Fixed=定尺/Range=范围尺/NonFixed=非定尺，中文或英文）</summary>
    public string? LengthStatus { get; set; }

    /// <summary>特殊制造状态（DeliveryState Key）</summary>
    public string? SpecialState { get; set; }

    /// <summary>外径（标称值 mm）</summary>
    public decimal? OuterDiameter { get; set; }

    /// <summary>壁厚（标称值 mm）</summary>
    public decimal? WallThickness { get; set; }

    /// <summary>长度（mm；Fixed=实际定尺长，Range/NonFixed 取数缺省按 6000 折算，全长度状态可命中 Length 档）</summary>
    public decimal? Length { get; set; }

    /// <summary>检验支数（批次实际检验支数 Quantity）</summary>
    public int? InspectionCount { get; set; }

    /// <summary>检验重量（kg；元/吨类别按重量折算整行金额，留空=该单位无法折算）</summary>
    public decimal? WeightKg { get; set; }

    /// <summary>工厂牌号（特殊牌号）</summary>
    public string? PlantGrade { get; set; }

    /// <summary>设备名称（特殊设备号）</summary>
    public string? EquipmentName { get; set; }
}

/// <summary>试算匹配结果（null=未定价）</summary>
public class PieceRateFinalInspectionMatchResultDto
{
    public int CategoryId { get; set; }

    /// <summary>成检项目（InspectionItem 枚举名）</summary>
    public string ItemKey { get; set; } = string.Empty;

    /// <summary>成检项目中文</summary>
    public string ItemKeyChinese { get; set; } = string.Empty;

    public decimal BasePrice { get; set; }

    public decimal TotalRatio { get; set; }

    public decimal UnitPrice { get; set; }

    public string Unit { get; set; } = string.Empty;

    public string UnitChinese { get; set; } = string.Empty;

    /// <summary>模拟计件工资 = 整行计价额（结算单价 × 数量折算，AmountForUnit 口径；未按写名人头均分）。
    /// null=未定价或缺数量/长度输入无法折算。</summary>
    public decimal? SimulatedAmount { get; set; }

    /// <summary>命中的维档清单</summary>
    public List<PieceRateProductionMatchTierHitDto> Hits { get; set; } = new();

    public string? Remark { get; set; }
}

/// <summary>模拟测算候选成检记录查询（2026-09-04 全局任意记录：跨期按成检项目 / 关键字检索）</summary>
public class FinalInspectionPriceTrialRecordQuery : QueryParams
{
    /// <summary>成检项目过滤（InspectionItem 枚举名；空=全部）</summary>
    public string? ItemKey { get; set; }
}

/// <summary>模拟测算候选成检记录行（点选后按 Id 计价，与月结采集同映射单源）</summary>
public class FinalInspectionPriceTrialRecordDto
{
    public int Id { get; set; }

    public DateTime InspectionDate { get; set; }

    /// <summary>成检项目（InspectionItem 枚举名）</summary>
    public string ItemKey { get; set; } = string.Empty;

    /// <summary>成检项目中文</summary>
    public string ItemKeyChinese { get; set; } = string.Empty;

    /// <summary>生产编号（记录自带）</summary>
    public string BatchNo { get; set; } = string.Empty;

    /// <summary>规格（所属批次）</summary>
    public string? Specification { get; set; }

    /// <summary>长度状态（所属批次；LengthStatus 枚举名）</summary>
    public string? LengthStatusKey { get; set; }

    /// <summary>长度状态中文</summary>
    public string? LengthStatusChinese { get; set; }

    /// <summary>定尺长度（批次长度状态=定尺时填写）</summary>
    public string? FixedLength { get; set; }

    /// <summary>检验支数</summary>
    public int? Quantity { get; set; }

    /// <summary>理论检验重量(kg)</summary>
    public int? Weight { get; set; }

    public string? EquipmentName { get; set; }

    public string? Operator { get; set; }
}
