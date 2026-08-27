namespace MES.Core.DTOs.Scheduling;

/// <summary>
/// 原锁计划「待投料量汇总」DTO（全部数值单位 kg，前端 /1000 转吨 F1）
/// </summary>
public class RawMaterialLockPendingSummaryDto
{
    /// <summary>参与汇总的工单数（ScheduleStage=2 原料锁定；待投料口径排除「单一成品采购」工单）</summary>
    public int TotalOrderCount { get; set; }

    /// <summary>计划投料总重（kg，同上排除「单一成品采购」工单）</summary>
    public decimal TotalWeight { get; set; }

    /// <summary>待投料量（kg，Σ PendingCalc；排除「单一成品采购」工单）</summary>
    public decimal PendingWeight { get; set; }

    /// <summary>待购工单行数（「包含」口径 FinishPlanWeight &gt; 0，含单一成品采购）</summary>
    public int PurchaseCount { get; set; }

    /// <summary>待购总重（kg，Σ PurchaseCalc）</summary>
    public decimal PurchaseWeight { get; set; }

    /// <summary>是否有待购数据（= PurchaseCount &gt; 0，前端成购矩阵显隐）</summary>
    public bool HasPurchaseData { get; set; }

    /// <summary>待投料矩阵行标签（4：RawMaterialLockRemarkKeys 全备注中文）</summary>
    public List<string> MatrixRowLabels { get; set; } = new();

    /// <summary>待投料矩阵列标签（5：UrgencyLevelKeys 排除 EPaused 中文）</summary>
    public List<string> MatrixColumnLabels { get; set; } = new();

    /// <summary>待投料矩阵行（4 行 × 5 列）</summary>
    public List<PendingMatrixRowDto> MatrixRows { get; set; } = new();

    /// <summary>待投料矩阵 5 列合计（每列一个，与 MatrixColumnLabels 对齐）</summary>
    public List<PendingMatrixTotalsDto> MatrixColumnTotals { get; set; } = new();

    /// <summary>待投料矩阵全表合计</summary>
    public PendingMatrixTotalsDto MatrixGrandTotals { get; set; } = new();

    /// <summary>理论待投料截日桶标签（7：绝对日期 ≤今日/区间/≥尾，桶边界走 DateBucket 配置）</summary>
    public List<string> CutoffBucketLabels { get; set; } = new();

    /// <summary>理论待投料截日行（4：完善计划/执行计划/外购成品/合计）</summary>
    public List<CutoffRowDto> CutoffRows { get; set; } = new();
}

/// <summary>待投料矩阵行</summary>
public class PendingMatrixRowDto
{
    /// <summary>5 列单元格（列 = UrgencyLevelKeys 排除 EPaused）</summary>
    public List<PendingMatrixCellDto> Cells { get; set; } = new();

    /// <summary>行工单数</summary>
    public int RowCount { get; set; }

    /// <summary>行待投料重（kg）</summary>
    public decimal RowPendingWeight { get; set; }

    /// <summary>行待购工单行数</summary>
    public int RowPurchaseCount { get; set; }

    /// <summary>行待购重（kg）</summary>
    public decimal RowPurchaseWeight { get; set; }
}

/// <summary>待投料矩阵单元格（备注 × 计划性）</summary>
public class PendingMatrixCellDto
{
    public int Count { get; set; }
    public decimal PendingWeight { get; set; }
    public int PurchaseCount { get; set; }
    public decimal PurchaseWeight { get; set; }
}

/// <summary>待投料矩阵列/全表合计</summary>
public class PendingMatrixTotalsDto
{
    public int Count { get; set; }
    public decimal PendingWeight { get; set; }
    public int PurchaseCount { get; set; }
    public decimal PurchaseWeight { get; set; }
}

/// <summary>理论待投料截日行</summary>
public class CutoffRowDto
{
    /// <summary>类别（完善计划/执行计划/外购成品/合计）</summary>
    public string Category { get; set; } = "";

    /// <summary>全期合计（kg）</summary>
    public decimal Total { get; set; }

    /// <summary>7 桶值（kg，与 CutoffBucketLabels 对应）</summary>
    public List<decimal> Buckets { get; set; } = new();
}
