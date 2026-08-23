namespace MES.Core.DTOs.Warehouse;

/// <summary>
/// 物料进出存报表结果：行=库房×物料类型（库房合并单元格），列=期初 + 12月×（入/出/结）+ 全年合计。
/// 结存为真实库存余额（全口径）。同一数据集支撑 4 个报表切换：入库/出库/库存/物料进出存（仅展示列不同）。
/// </summary>
public class MonthlyStockSummaryResultDto
{
    /// <summary>统计年份（窗口为本年 1月1日 ~ 12月31日；期初=上年末结存）</summary>
    public int Year { get; set; }

    /// <summary>月份标签，长度恒 12，索引 0=1月…11=12月（"yyyy-MM"）</summary>
    public List<string> MonthLabels { get; set; } = new();

    /// <summary>明细行（库房×物料类型，已按库房固定顺序+物料类型固定顺序排序；无合计行）。供 库存/物料进出存 报表使用。</summary>
    public List<MonthlyStockRowDto> Rows { get; set; } = new();

    /// <summary>入库报表粒度行（库房×物料类型×入库来源，来源固定顺序，全 0 来源行隐藏）。仅累计入。</summary>
    public List<MonthlyStockRowDto> InboundSourceRows { get; set; } = new();

    /// <summary>出库报表粒度行（库房×物料类型×出库类型，类型固定顺序，全 0 类型行隐藏）。仅累计出。</summary>
    public List<MonthlyStockRowDto> OutboundTypeRows { get; set; } = new();
}

/// <summary>物料进出存行（库房×物料类型维度）。</summary>
public class MonthlyStockRowDto
{
    /// <summary>库房名（如 原料库/成品库/在制品库/次品库）</summary>
    public string WarehouseName { get; set; } = string.Empty;

    /// <summary>物料类型（MaterialType 枚举名，前端经 GetMaterialTypeText 转中文）</summary>
    public string MaterialType { get; set; } = string.Empty;

    /// <summary>入库来源（InboundSource 枚举名；入库报表粒度行专用，全口径行为 null/空）</summary>
    public string? InboundSource { get; set; }

    /// <summary>出库类型（OutboundType 枚举名；出库报表粒度行专用，全口径行为 null/空）</summary>
    public string? OutboundType { get; set; }

    /// <summary>期初结存重量(kg)=截至上年末的全口径结存（入−出）</summary>
    public decimal OpeningWeight { get; set; }

    /// <summary>各月入/出/结，长度恒 12，索引 0=1月…11=12月</summary>
    public List<MonthlyStockMonthValueDto> Months { get; set; } = new();

    /// <summary>全年合计：入（kg）</summary>
    public decimal TotalIn { get; set; }

    /// <summary>全年合计：出（kg）</summary>
    public decimal TotalOut { get; set; }

    /// <summary>年末结存（真实全口径，kg）=期初+总入−总出</summary>
    public decimal ClosingWeight { get; set; }
}

/// <summary>单月入/出/结：入/出为当月全口径流量，结=真实月末结存（全口径，期初+总入−总出逐月递推）。</summary>
public class MonthlyStockMonthValueDto
{
    /// <summary>当月入重量(kg)（全口径，不按来源拆分）</summary>
    public decimal In { get; set; }

    /// <summary>当月出重量(kg)（全口径，不按类型拆分）</summary>
    public decimal Out { get; set; }

    /// <summary>真实月末结存重量(kg)（全口径）</summary>
    public decimal Closing { get; set; }
}
