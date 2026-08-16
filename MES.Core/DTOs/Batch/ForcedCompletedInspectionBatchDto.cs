namespace MES.Core.DTOs.Batch;

/// <summary>
/// 成检到料「强制完成」批次通知（批次首页实时查询）
/// 成检到料 IsForceCompleted=true 且批次仍处于「成检」（InFinalInspection）状态的批次。
/// 强制完成的本意是到料后执行有特殊情况，需在批次详情页将该批次强制完成为「完成」，
/// 实现批次状态统一后通知自动消失。
/// </summary>
public class ForcedCompletedInspectionBatchDto
{
    /// <summary>批次ID</summary>
    public int BatchId { get; set; }

    /// <summary>生产编号</summary>
    public string BatchNo { get; set; } = null!;

    /// <summary>工单号</summary>
    public string WorkOrderNo { get; set; } = null!;

    /// <summary>成检类型（FormalInspection/PreInspection，英文存储值）</summary>
    public string? InspectionType { get; set; }

    /// <summary>成检类型中文显示</summary>
    public string? InspectionTypeDisplay { get; set; }

    /// <summary>到料日期</summary>
    public DateTime ReceiveDate { get; set; }

    /// <summary>工序名称</summary>
    public string? ProcessName { get; set; }
}
